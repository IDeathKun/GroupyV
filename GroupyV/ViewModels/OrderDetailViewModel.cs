using GroupyV.Data;
using GroupyV.Helpers;
using GroupyV.Models;
using GroupyV.Services;
using MaterialDesignThemes.Wpf;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GroupyV.ViewModels
{
    public class CarrierDisplay
    {
        public string Name { get; set; }
        public PackIconKind Icon { get; set; }
    }

    public class OrderDetailViewModel : INotifyPropertyChanged
    {
        // --- DONNÉES PRÉVENTE ---
        private Prevente _order;
        public Prevente Order { get => _order; set { _order = value; OnPropertyChanged(); } }

        // --- DONNÉES CLIENT (centré sur le client concerné) ---
        private string _clientNomComplet;
        public string ClientNomComplet { get => _clientNomComplet; set { _clientNomComplet = value; OnPropertyChanged(); } }

        private string _clientEmail;
        public string ClientEmail { get => _clientEmail; set { _clientEmail = value; OnPropertyChanged(); } }

        private string _clientPhone;
        public string ClientPhone { get => _clientPhone; set { _clientPhone = value; OnPropertyChanged(); } }

        private string _clientAdresse;
        public string ClientAdresse { get => _clientAdresse; set { _clientAdresse = value; OnPropertyChanged(); } }

        private string _clientInitiales;
        public string ClientInitiales { get => _clientInitiales; set { _clientInitiales = value; OnPropertyChanged(); } }

        // --- LISTE PARTICIPANTS (tous les clients de la prévente) ---
        public ObservableCollection<Participation> ParticipantsList { get; set; }

        // --- GESTION ÉTAT EXPÉDITION ---
        private Expedition _currentExpedition;
        public Expedition CurrentExpedition
        {
            get => _currentExpedition;
            set
            {
                _currentExpedition = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsShipped));
            }
        }

        public bool IsShipped => CurrentExpedition != null;

        // --- DIALOG CONFIRMATION ---
        private bool _isAnyDialogOpen;
        public bool IsAnyDialogOpen { get => _isAnyDialogOpen; set { _isAnyDialogOpen = value; OnPropertyChanged(); } }

        // --- FORMULAIRE & UI ---
        public List<CarrierDisplay> Carriers { get; set; }
        public CarrierDisplay SelectedCarrier { get; set; }
        public string AutoTrackingNumber { get; set; }
        public string WeightInput { get; set; }
        public string DimensionsInput { get; set; }

        // --- COMMANDES ---
        public ICommand GoBackCommand { get; }
        public ICommand OpenInvoiceCommand { get; }
        public ICommand OpenConfirmDialogCommand { get; }
        public ICommand ExecuteShippingCommand { get; }
        public ICommand CloseDialogCommand { get; }
        public ICommand PrintShippingLabelCommand { get; }
        public ICommand PrintAllShippingLabelsCommand { get; }
        public Action RequestGoBack { get; set; }

        private const double DefaultLabelWidthCm = 10.0;
        private const double DefaultLabelHeightCm = 15.0;

        private static readonly (double WidthCm, double HeightCm)[] SupportedLabelFormats =
        {
            (10.0, 15.0),
            (15.0, 7.0),
            (10.0, 10.0)
        };

        private static double CmToDip(double cm) => cm * 96.0 / 2.54;

        public OrderDetailViewModel(int orderId)
        {
            InitializeForm();
            LoadOrder(orderId);

            GoBackCommand = new RelayCommand(o => RequestGoBack?.Invoke());
            OpenInvoiceCommand = new RelayCommand(OpenInvoicePdf);
            CloseDialogCommand = new RelayCommand(o => IsAnyDialogOpen = false);
            PrintShippingLabelCommand = new RelayCommand(PrintShippingLabel);
            PrintAllShippingLabelsCommand = new RelayCommand(PrintAllShippingLabels);

            OpenConfirmDialogCommand = new RelayCommand(o =>
            {
                if (SelectedCarrier == null) { MessageBox.Show("Veuillez sélectionner un transporteur."); return; }
                if (string.IsNullOrWhiteSpace(WeightInput)) WeightInput = "0";
                if (string.IsNullOrWhiteSpace(DimensionsInput)) DimensionsInput = "Standard";
                IsAnyDialogOpen = true;
            });
            ExecuteShippingCommand = new RelayCommand(FinalizeShipping);
        }

        private void LoadOrder(int id)
        {
            using (var db = new GroupyContext())
            {
                // Charger toutes les préventes puis filtrer en mémoire (éviter erreur SQL)
                var allPreventes = db.Preventes
                    .Include(p => p.Produit)
                    .Include(p => p.Participations).ThenInclude(part => part.Client).ThenInclude(c => c.Utilisateur)
                    .Include(p => p.Participations).ThenInclude(part => part.Facture)
                    .AsNoTracking()
                    .ToList();

                var orderFound = allPreventes.FirstOrDefault(p => p.IdPrevente == id);

                if (orderFound != null)
                {
                    Order = orderFound;

                    // Charger les factures manquantes
                    foreach (var p in orderFound.Participations)
                    {
                        if (p.Facture == null)
                            p.Facture = db.Factures.FirstOrDefault(f => f.IdClient == p.IdClient && f.IdPrevente == p.IdPrevente);
                    }

                    ParticipantsList = new ObservableCollection<Participation>(Order.Participations);

                    // Charger les infos du premier client (client principal)
                    var firstParticipant = Order.Participations.FirstOrDefault();
                    if (firstParticipant?.Client?.Utilisateur != null)
                    {
                        var u = firstParticipant.Client.Utilisateur;
                        ClientNomComplet = $"{u.Prenom} {u.Nom}";
                        ClientEmail = u.Email;
                        ClientPhone = u.Phone;
                        ClientAdresse = u.Adresse;
                        ClientInitiales = $"{(u.Prenom?.Length > 0 ? u.Prenom[0].ToString().ToUpper() : "")}{(u.Nom?.Length > 0 ? u.Nom[0].ToString().ToUpper() : "")}";
                    }

                    // Charger l'expédition existante
                    CurrentExpedition = db.Expeditions.FirstOrDefault(e => e.IdPrevente == id);
                }
            }
        }

        private void FinalizeShipping(object obj)
        {
            IsAnyDialogOpen = false;
            try
            {
                using (var db = new GroupyContext())
                {
                    int joursEstimes = (SelectedCarrier.Name.Contains("Chronopost") || SelectedCarrier.Name.Contains("UPS")) ? 1 : 3;
                    DateTime datePrevue = DateTime.Now.AddDays(joursEstimes);

                    var newExpedition = new Expedition
                    {
                        IdPrevente = Order.IdPrevente,
                        Transporteur = SelectedCarrier.Name,
                        NumeroTracking = AutoTrackingNumber,
                        Statut = "expedie",
                        DatePreparation = DateTime.Now,
                        DateExpedition = DateTime.Now,
                        DateLivraisonPrevue = datePrevue,
                        Poids = decimal.TryParse(WeightInput, out decimal w) ? w : 0,
                        Dimensions = string.IsNullOrWhiteSpace(DimensionsInput) ? "Standard" : DimensionsInput
                    };

                    db.Expeditions.Add(newExpedition);

                    var p = db.Preventes.Include(pr => pr.Participations)
                              .FirstOrDefault(pr => pr.IdPrevente == Order.IdPrevente);
                    if (p != null) p.Statut = "terminée";

                    // --- Mise à jour du stock après expédition ---
                    if (p != null)
                    {
                        int quantiteExpediee = p.Participations?.Count ?? 0;
                        if (quantiteExpediee > 0)
                        {
                            var vendeurId = UserSession.Instance.CurrentUser?.IdUser;
                            var stock = db.Stocks.FirstOrDefault(s => s.IdProduit == p.IdProduit);

                            if (stock == null)
                            {
                                stock = new Stock
                                {
                                    IdProduit = p.IdProduit,
                                    StockPhysique = 0,
                                    StockReserve = 0,
                                    SeuilAlerte = 0,
                                    PrixAchat = 0,
                                    Emplacement = string.Empty
                                };
                                db.Stocks.Add(stock);
                                db.SaveChanges();
                            }

                            int stockAvant = stock.StockPhysique;
                            int stockApres = stockAvant - quantiteExpediee;

                            stock.StockPhysique = stockApres;

                            db.MouvementsStock.Add(new MouvementStock
                            {
                                IdProduit = p.IdProduit,
                                IdVendeur = vendeurId ?? 0,
                                TypeMouvement = "sortie",
                                Quantite = quantiteExpediee,
                                StockAvant = stockAvant,
                                StockApres = stockApres,
                                Motif = "Expédition",
                                MotifDetaille = $"Expédition prévente #{p.IdPrevente} — {SelectedCarrier.Name} — {AutoTrackingNumber}",
                                DateMouvement = DateTime.Now
                            });
                        }
                    }

                    db.SaveChanges();

                    CurrentExpedition = newExpedition;

                    MessageBox.Show($"Expédition validée !\nLivraison estimée le {datePrevue:dd/MM/yyyy}", "Succès");
                }
            }
            catch (Exception ex) { MessageBox.Show("Erreur : " + ex.Message); }
        }

        private void InitializeForm()
        {
            Carriers = new List<CarrierDisplay>
            {
                new CarrierDisplay { Name = "Colissimo", Icon = PackIconKind.PackageVariantClosed },
                new CarrierDisplay { Name = "Chronopost", Icon = PackIconKind.ClockFast },
                new CarrierDisplay { Name = "Mondial Relay", Icon = PackIconKind.MapMarkerRadius },
                new CarrierDisplay { Name = "UPS", Icon = PackIconKind.TruckFast }
            };
            string randomId = Guid.NewGuid().ToString().Substring(0, 8).ToUpper();
            AutoTrackingNumber = $"FR-{DateTime.Now.Year}-{randomId}";
        }

        /// <summary>
        /// Dossier racine du site web AMPPS où sont stockés les uploads.
        /// </summary>
        private static readonly string AmppsWebRoot = @"C:\Program Files\Ampps\www\vente_groupe";

        private void OpenInvoicePdf(object param)
        {
            if (param is not Participation participation) return;

            var facture = participation.Facture;
            string pdfPath = facture?.PdfFacture;

            if (string.IsNullOrWhiteSpace(pdfPath))
            {
                MessageBox.Show("Aucune facture PDF disponible pour ce participant.", "Facture introuvable", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Nettoyer le chemin (enlever slash de début éventuel)
            pdfPath = pdfPath.TrimStart('/').Replace('/', '\\');

            // Chercher le PDF dans le dossier AMPPS
            string fullPath = Path.Combine(AmppsWebRoot, pdfPath);

            if (!File.Exists(fullPath))
            {
                MessageBox.Show($"Fichier introuvable :\n{fullPath}", "Facture introuvable", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo(fullPath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Impossible d'ouvrir le PDF :\n{ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PrintShippingLabel(object param)
        {
            if (param is not Participation participation) return;

            try
            {
                var doc = CreateShippingLabelsDocument(new[] { participation });
                ShowPrintPreview(doc, $"Bordereau - Prévente #{Order?.IdPrevente}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Impossible de préparer le bordereau :\n{ex.Message}", "Impression", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PrintAllShippingLabels(object _)
        {
            if (ParticipantsList == null || ParticipantsList.Count == 0)
            {
                MessageBox.Show("Aucun participant à imprimer.", "Impression", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var doc = CreateShippingLabelsDocument(ParticipantsList);
                ShowPrintPreview(doc, $"Bordereaux - Prévente #{Order?.IdPrevente}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Impossible de préparer les bordereaux :\n{ex.Message}", "Impression", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowPrintPreview(FlowDocument doc, string jobName)
        {
            double labelWidth = CmToDip(DefaultLabelWidthCm);
            double labelHeight = CmToDip(DefaultLabelHeightCm);

            doc.PageWidth = labelWidth;
            doc.PageHeight = labelHeight;
            doc.PagePadding = new Thickness(8);
            doc.ColumnWidth = double.PositiveInfinity;

            var viewer = new FlowDocumentScrollViewer
            {
                Document = doc,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                IsToolBarVisible = true,
                Zoom = 130
            };

            var printButton = new Button
            {
                Content = "Imprimer",
                MinWidth = 110,
                Height = 34,
                Margin = new Thickness(0, 0, 8, 0)
            };

            var closeButton = new Button
            {
                Content = "Fermer",
                MinWidth = 110,
                Height = 34
            };

            var buttonsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };
            buttonsPanel.Children.Add(printButton);
            buttonsPanel.Children.Add(closeButton);

            var root = new DockPanel { Margin = new Thickness(12) };
            DockPanel.SetDock(buttonsPanel, Dock.Bottom);
            root.Children.Add(buttonsPanel);
            root.Children.Add(viewer);

            var previewWindow = new Window
            {
                Title = $"Aperçu avant impression ({DefaultLabelWidthCm:0.#}x{DefaultLabelHeightCm:0.#} cm)",
                Width = 900,
                Height = 700,
                MinWidth = 700,
                MinHeight = 500,
                Content = root,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Application.Current?.MainWindow
            };

            closeButton.Click += (_, __) => previewWindow.Close();
            printButton.Click += (_, __) =>
            {
                var printDialog = new PrintDialog();
                if (printDialog.ShowDialog() != true) return;

                doc.PageWidth = labelWidth;
                doc.PageHeight = labelHeight;
                doc.PagePadding = new Thickness(8);
                doc.ColumnWidth = double.PositiveInfinity;

                var paginator = ((IDocumentPaginatorSource)doc).DocumentPaginator;
                printDialog.PrintDocument(paginator, jobName);
            };

            previewWindow.ShowDialog();
        }

        private static string GenerateRandomQrPayload(Participation participation)
        {
            string randomPart = Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
            return $"GV|PREV:{participation.IdPrevente}|PART:{participation.IdParticipation}|TS:{DateTime.UtcNow:yyyyMMddHHmmss}|R:{randomPart}";
        }

        private static Image CreateQrCodeImage(string payload)
        {
            using var qrGenerator = new QRCodeGenerator();
            using var qrData = qrGenerator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
            var qrCode = new PngByteQRCode(qrData);
            byte[] bytes = qrCode.GetGraphic(10);

            using var ms = new MemoryStream(bytes);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = ms;
            bitmap.EndInit();
            bitmap.Freeze();

            return new Image
            {
                Source = bitmap,
                Width = 110,
                Height = 110,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(6, 0, 0, 0)
            };
        }

        private FlowDocument CreateShippingLabelsDocument(IEnumerable<Participation> participations)
        {
            var doc = new FlowDocument
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12,
                PagePadding = new Thickness(8)
            };

            bool first = true;
            foreach (var p in participations)
            {
                var client = p.Client;

                string transporteur = CurrentExpedition?.Transporteur ?? SelectedCarrier?.Name ?? "À définir";
                string tracking = CurrentExpedition?.NumeroTracking ?? AutoTrackingNumber ?? "À générer";
                string livraison = CurrentExpedition?.DateLivraisonPrevue.HasValue == true
                    ? CurrentExpedition.DateLivraisonPrevue.Value.ToString("dd/MM/yyyy")
                    : "À confirmer";

                string qrPayload = GenerateRandomQrPayload(p);
                var qrImage = CreateQrCodeImage(qrPayload);

                var card = new Border
                {
                    BorderBrush = Brushes.Black,
                    BorderThickness = new Thickness(1.5),
                    Padding = new Thickness(10),
                    Margin = new Thickness(0)
                };

                var cardRoot = new Grid();
                cardRoot.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                cardRoot.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                cardRoot.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var header = new Grid { Margin = new Thickness(0, 0, 0, 8) };
                header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var leftHeader = new StackPanel();
                leftHeader.Children.Add(new TextBlock
                {
                    Text = "BORDEREAU D'ENVOI",
                    FontWeight = FontWeights.Bold,
                    FontSize = 16
                });
                leftHeader.Children.Add(new TextBlock
                {
                    Text = $"Prévente #{Order?.IdPrevente} • Colis #{p.IdParticipation}",
                    FontSize = 10,
                    Foreground = Brushes.DimGray
                });

                var rightHeader = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right };
                rightHeader.Children.Add(new TextBlock
                {
                    Text = transporteur.ToUpperInvariant(),
                    FontWeight = FontWeights.Bold,
                    FontSize = 11,
                    HorizontalAlignment = HorizontalAlignment.Right
                });
                rightHeader.Children.Add(new TextBlock
                {
                    Text = $"Édition: {DateTime.Now:dd/MM/yyyy HH:mm}",
                    FontSize = 9,
                    Foreground = Brushes.DimGray,
                    HorizontalAlignment = HorizontalAlignment.Right
                });

                Grid.SetColumn(leftHeader, 0);
                Grid.SetColumn(rightHeader, 1);
                header.Children.Add(leftHeader);
                header.Children.Add(rightHeader);

                var separator = new Border
                {
                    BorderBrush = Brushes.Black,
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    Margin = new Thickness(0, 0, 0, 8)
                };

                var content = new Grid();
                content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var destination = new StackPanel { Margin = new Thickness(0, 0, 8, 0) };
                destination.Children.Add(new TextBlock
                {
                    Text = "DESTINATAIRE",
                    FontWeight = FontWeights.Bold,
                    FontSize = 10,
                    Foreground = Brushes.DimGray
                });
                destination.Children.Add(new TextBlock
                {
                    Text = $"{client?.Prenom} {client?.Nom}",
                    FontWeight = FontWeights.Bold,
                    FontSize = 15,
                    Margin = new Thickness(0, 2, 0, 2)
                });
                destination.Children.Add(new TextBlock { Text = client?.Adresse ?? "—", TextWrapping = TextWrapping.Wrap, MaxWidth = 240 });
                destination.Children.Add(new TextBlock { Text = $"Tél: {client?.Phone}", FontSize = 10 });
                destination.Children.Add(new TextBlock { Text = $"Produit: {Order?.Produit?.NomProduit ?? "—"}", FontSize = 10, Margin = new Thickness(0, 6, 0, 0) });
                destination.Children.Add(new TextBlock { Text = $"Livraison: {livraison}", FontSize = 10 });
                destination.Children.Add(new TextBlock
                {
                    Text = tracking,
                    FontWeight = FontWeights.Bold,
                    FontSize = 16,
                    Margin = new Thickness(0, 8, 0, 0)
                });

                var qrPanel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right };
                qrPanel.Children.Add(qrImage);
                qrPanel.Children.Add(new TextBlock
                {
                    Text = qrPayload,
                    Width = 110,
                    FontSize = 7,
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center,
                    Foreground = Brushes.Gray,
                    Margin = new Thickness(0, 4, 0, 0)
                });

                Grid.SetColumn(destination, 0);
                Grid.SetColumn(qrPanel, 1);
                content.Children.Add(destination);
                content.Children.Add(qrPanel);

                Grid.SetRow(header, 0);
                Grid.SetRow(separator, 1);
                Grid.SetRow(content, 2);
                cardRoot.Children.Add(header);
                cardRoot.Children.Add(separator);
                cardRoot.Children.Add(content);

                card.Child = cardRoot;

                doc.Blocks.Add(new BlockUIContainer(card)
                {
                    BreakPageBefore = !first
                });

                first = false;
            }

            return doc;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}