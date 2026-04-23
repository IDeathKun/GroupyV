using GroupyV.Data;
using GroupyV.Helpers;
using GroupyV.Models;
using GroupyV.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace GroupyV.ViewModels
{
    public class StockItemDisplay : INotifyPropertyChanged
    {
        public int IdProduit { get; set; }
        public int IdStock { get; set; }
        public string NomProduit { get; set; }
        public string Categorie { get; set; }
        public string Image { get; set; }

        private int _stockPhysique;
        public int StockPhysique
        {
            get => _stockPhysique;
            set { _stockPhysique = value; OnPropertyChanged(); OnPropertyChanged(nameof(StockDisponible)); OnPropertyChanged(nameof(IsAlerte)); }
        }

        private int _stockReserve;
        public int StockReserve
        {
            get => _stockReserve;
            set { _stockReserve = value; OnPropertyChanged(); OnPropertyChanged(nameof(StockDisponible)); }
        }

        private int _seuilAlerte;
        public int SeuilAlerte
        {
            get => _seuilAlerte;
            set { _seuilAlerte = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsAlerte)); }
        }

        private decimal? _prixAchat;
        public decimal? PrixAchat
        {
            get => _prixAchat;
            set { _prixAchat = value; OnPropertyChanged(); }
        }

        private string _emplacement;
        public string Emplacement
        {
            get => _emplacement;
            set { _emplacement = value; OnPropertyChanged(); }
        }

        public int StockDisponible => StockPhysique - StockReserve;
        public bool IsAlerte => StockPhysique <= SeuilAlerte;

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class MouvementDisplay
    {
        public string Produit { get; set; }
        public string Type { get; set; }
        public int Quantite { get; set; }
        public int StockAvant { get; set; }
        public int StockApres { get; set; }
        public string Motif { get; set; }
        public string MotifDetaille { get; set; }
        public DateTime Date { get; set; }
        public bool IsEntree => Type == "entree";
    }

    public class StockViewModel : BaseViewModel
    {
        private static readonly string[] CsvHeaders =
        {
            "id_stock",
            "id_produit",
            "stock_physique",
            "stock_reserve",
            "seuil_alerte",
            "prix_achat",
            "emplacement"
        };

        // --- LISTES ---
        private ObservableCollection<StockItemDisplay> _allStocks;

        private ObservableCollection<StockItemDisplay> _stocksList;
        public ObservableCollection<StockItemDisplay> StocksList
        {
            get => _stocksList;
            set { _stocksList = value; OnPropertyChanged(); }
        }

        private ObservableCollection<MouvementDisplay> _mouvementsList;
        public ObservableCollection<MouvementDisplay> MouvementsList
        {
            get => _mouvementsList;
            set { _mouvementsList = value; OnPropertyChanged(); }
        }

        // --- KPI ---
        private int _totalProduits;
        public int TotalProduits { get => _totalProduits; set { _totalProduits = value; OnPropertyChanged(); } }

        private int _totalUnites;
        public int TotalUnites { get => _totalUnites; set { _totalUnites = value; OnPropertyChanged(); } }

        private int _alertesCount;
        public int AlertesCount { get => _alertesCount; set { _alertesCount = value; OnPropertyChanged(); } }

        private int _mouvementsJour;
        public int MouvementsJour { get => _mouvementsJour; set { _mouvementsJour = value; OnPropertyChanged(); } }

        // --- RECHERCHE & FILTRE ---
        private string _searchText;
        public string SearchText
        {
            get => _searchText;
            set { _searchText = value; OnPropertyChanged(); ApplyFilter(); }
        }

        private string _selectedFilter = "Tous";
        public string SelectedFilter
        {
            get => _selectedFilter;
            set { _selectedFilter = value; OnPropertyChanged(); ApplyFilter(); }
        }

        public List<string> Filters { get; } = new() { "Tous", "En alerte", "Stock OK" };

        // --- DIALOG MOUVEMENT ---
        private bool _isDialogOpen;
        public bool IsDialogOpen { get => _isDialogOpen; set { _isDialogOpen = value; OnPropertyChanged(); } }

        private StockItemDisplay _selectedStock;
        public StockItemDisplay SelectedStock
        {
            get => _selectedStock;
            set { _selectedStock = value; OnPropertyChanged(); }
        }

        private string _dialogType = "entree";
        public string DialogType
        {
            get => _dialogType;
            set { _dialogType = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsEntreeMode)); }
        }

        public bool IsEntreeMode => DialogType == "entree";

        private string _dialogQuantite;
        public string DialogQuantite
        {
            get => _dialogQuantite;
            set { _dialogQuantite = value; OnPropertyChanged(); }
        }

        private string _dialogMotif;
        public string DialogMotif
        {
            get => _dialogMotif;
            set { _dialogMotif = value; OnPropertyChanged(); }
        }

        private string _dialogMotifDetaille;
        public string DialogMotifDetaille
        {
            get => _dialogMotifDetaille;
            set { _dialogMotifDetaille = value; OnPropertyChanged(); }
        }

        private string _dialogPrixAchat;
        public string DialogPrixAchat
        {
            get => _dialogPrixAchat;
            set { _dialogPrixAchat = value; OnPropertyChanged(); }
        }

        private string _dialogEmplacement;
        public string DialogEmplacement
        {
            get => _dialogEmplacement;
            set { _dialogEmplacement = value; OnPropertyChanged(); }
        }

        // --- COMMANDES ---
        public ICommand OpenAddStockCommand { get; }
        public ICommand OpenRemoveStockCommand { get; }
        public ICommand CloseDialogCommand { get; }
        public ICommand ConfirmMouvementCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand ExportCsvCommand { get; }
        public ICommand ImportCsvCommand { get; }

        public StockViewModel()
        {
            OpenAddStockCommand = new RelayCommand(OpenAddStock);
            OpenRemoveStockCommand = new RelayCommand(OpenRemoveStock);
            CloseDialogCommand = new RelayCommand(_ => IsDialogOpen = false);
            ConfirmMouvementCommand = new RelayCommand(ConfirmMouvement);
            RefreshCommand = new RelayCommand(_ => LoadData());
            ExportCsvCommand = new RelayCommand(_ => ExportCsv());
            ImportCsvCommand = new RelayCommand(_ => ImportCsv());

            LoadData();
        }

        private void LoadData()
        {
            try
            {
                var vendeurId = UserSession.Instance.CurrentUser?.IdUser;
                if (vendeurId == null) return;

                using var db = new GroupyContext();

                // Charger les produits du vendeur avec leur stock (left join)
                var stocks = (from p in db.Produits
                                  .Include(p => p.Categorie)
                              join s in db.Stocks on p.IdProduit equals s.IdProduit into stockGroup
                              from s in stockGroup.DefaultIfEmpty()
                              where p.IdVendeur == vendeurId
                              select new { Produit = p, Stock = s })
                    .AsNoTracking()
                    .ToList()
                    .Select(x => new StockItemDisplay
                    {
                        IdProduit = x.Produit.IdProduit,
                        IdStock = x.Stock?.IdStock ?? 0,
                        NomProduit = x.Produit.NomProduit ?? "—",
                        Categorie = x.Produit.Categorie?.Lib ?? "—",
                        Image = x.Produit.Image,
                        StockPhysique = x.Stock?.StockPhysique ?? 0,
                        StockReserve = x.Stock?.StockReserve ?? 0,
                        SeuilAlerte = x.Stock?.SeuilAlerte ?? 0,
                        PrixAchat = x.Stock?.PrixAchat,
                        Emplacement = x.Stock?.Emplacement ?? "—"
                    })
                    .OrderBy(s => s.NomProduit)
                    .ToList();

                _allStocks = new ObservableCollection<StockItemDisplay>(stocks);
                StocksList = new ObservableCollection<StockItemDisplay>(stocks);

                // KPI
                TotalProduits = stocks.Count;
                TotalUnites = stocks.Sum(s => s.StockPhysique);
                AlertesCount = stocks.Count(s => s.IsAlerte);

                // Mouvements récents (30 derniers jours)
                var depuis = DateTime.Now.AddDays(-30);
                var mouvements = db.MouvementsStock
                    .Include(m => m.Produit)
                    .AsNoTracking()
                    .ToList()
                    .Where(m => m.IdVendeur == vendeurId && m.DateMouvement >= depuis)
                    .OrderByDescending(m => m.DateMouvement)
                    .Select(m => new MouvementDisplay
                    {
                        Produit = m.Produit?.NomProduit ?? "—",
                        Type = m.TypeMouvement,
                        Quantite = m.Quantite,
                        StockAvant = m.StockAvant,
                        StockApres = m.StockApres,
                        Motif = m.Motif,
                        MotifDetaille = m.MotifDetaille,
                        Date = m.DateMouvement
                    })
                    .ToList();

                MouvementsList = new ObservableCollection<MouvementDisplay>(mouvements);
                MouvementsJour = mouvements.Count(m => m.Date.Date == DateTime.Today);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur chargement stocks : {ex.Message}");
            }
        }

        private void ApplyFilter()
        {
            if (_allStocks == null) return;

            var filtered = _allStocks.AsEnumerable();

            // Filtre texte
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var search = SearchText.ToLower();
                filtered = filtered.Where(s =>
                    s.NomProduit.ToLower().Contains(search) ||
                    s.Categorie.ToLower().Contains(search) ||
                    (s.Emplacement ?? string.Empty).ToLower().Contains(search));
            }

            // Filtre statut
            filtered = SelectedFilter switch
            {
                "En alerte" => filtered.Where(s => s.IsAlerte),
                "Stock OK" => filtered.Where(s => !s.IsAlerte),
                _ => filtered
            };

            StocksList = new ObservableCollection<StockItemDisplay>(filtered);
        }

        private void OpenAddStock(object param)
        {
            if (param is not StockItemDisplay item) return;
            SelectedStock = item;
            DialogType = "entree";
            DialogQuantite = "";
            DialogMotif = "Réapprovisionnement";
            DialogMotifDetaille = "";
            DialogPrixAchat = item.PrixAchat?.ToString("0.##", CultureInfo.CurrentCulture) ?? "";
            DialogEmplacement = item.Emplacement == "—" ? string.Empty : item.Emplacement;
            IsDialogOpen = true;
        }

        private void OpenRemoveStock(object param)
        {
            if (param is not StockItemDisplay item) return;
            SelectedStock = item;
            DialogType = "sortie";
            DialogQuantite = "";
            DialogMotif = "Vente";
            DialogMotifDetaille = "";
            DialogPrixAchat = item.PrixAchat?.ToString("0.##", CultureInfo.CurrentCulture) ?? "";
            DialogEmplacement = item.Emplacement == "—" ? string.Empty : item.Emplacement;
            IsDialogOpen = true;
        }

        private void ConfirmMouvement(object _)
        {
            if (SelectedStock == null) return;

            if (!int.TryParse(DialogQuantite, out int qte) || qte <= 0)
            {
                MessageBox.Show("Veuillez saisir une quantité valide (> 0).", "Quantité invalide", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!string.IsNullOrWhiteSpace(DialogPrixAchat)
                && !decimal.TryParse(DialogPrixAchat, NumberStyles.Number, CultureInfo.CurrentCulture, out decimal _prixAchatValidation))
            {
                MessageBox.Show("Veuillez saisir un prix d'achat valide.", "Prix invalide", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (DialogType == "sortie" && qte > SelectedStock.StockPhysique)
            {
                MessageBox.Show($"Stock insuffisant. Stock actuel : {SelectedStock.StockPhysique}", "Stock insuffisant", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(DialogMotif))
            {
                MessageBox.Show("Veuillez saisir un motif.", "Motif requis", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var vendeurId = UserSession.Instance.CurrentUser?.IdUser;
                if (vendeurId == null) return;

                using var db = new GroupyContext();

                // Chercher ou créer l'entrée stock pour ce produit
                var stock = SelectedStock.IdStock > 0
                    ? db.Stocks.FirstOrDefault(s => s.IdStock == SelectedStock.IdStock)
                    : null;

                if (stock == null)
                {
                    // Produit sans entrée stock : en créer une
                    stock = new Stock
                    {
                        IdProduit = SelectedStock.IdProduit,
                        StockPhysique = 0,
                        StockReserve = 0,
                        SeuilAlerte = 0,
                        PrixAchat = 0,
                        Emplacement = string.Empty
                    };
                    db.Stocks.Add(stock);
                    db.SaveChanges(); // pour obtenir l'IdStock généré
                }

                int stockAvant = stock.StockPhysique;
                int stockApres = DialogType == "entree" ? stockAvant + qte : stockAvant - qte;

                if (decimal.TryParse(DialogPrixAchat, NumberStyles.Number, CultureInfo.CurrentCulture, out var prixAchat))
                    stock.PrixAchat = prixAchat;

                stock.Emplacement = string.IsNullOrWhiteSpace(DialogEmplacement) ? null : DialogEmplacement.Trim();

                // Enregistrer le mouvement
                var mouvement = new MouvementStock
                {
                    IdProduit = stock.IdProduit,
                    IdVendeur = vendeurId.Value,
                    TypeMouvement = DialogType,
                    Quantite = qte,
                    StockAvant = stockAvant,
                    StockApres = stockApres,
                    Motif = DialogMotif,
                    MotifDetaille = string.IsNullOrWhiteSpace(DialogMotifDetaille) ? null : DialogMotifDetaille,
                    DateMouvement = DateTime.Now
                };

                db.MouvementsStock.Add(mouvement);

                // Mettre à jour le stock
                stock.StockPhysique = stockApres;
                db.SaveChanges();

                IsDialogOpen = false;

                // Rafraîchir
                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur : {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportCsv()
        {
            try
            {
                var vendeurId = UserSession.Instance.CurrentUser?.IdUser;
                if (vendeurId == null)
                {
                    MessageBox.Show("Aucun utilisateur connecté.", "Export CSV", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var dialog = new SaveFileDialog
                {
                    Title = "Exporter les stocks en CSV",
                    Filter = "CSV (*.csv)|*.csv",
                    FileName = $"stocks_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
                };

                if (dialog.ShowDialog() != true) return;

                using var db = new GroupyContext();
                var rows = (from s in db.Stocks
                            join p in db.Produits on s.IdProduit equals p.IdProduit
                            where p.IdVendeur == vendeurId
                            select s)
                           .AsNoTracking()
                           .OrderBy(s => s.IdStock)
                           .ToList();

                var sb = new StringBuilder();
                sb.AppendLine(string.Join(';', CsvHeaders));

                foreach (var s in rows)
                {
                    sb.AppendLine(string.Join(';', new[]
                    {
                        s.IdStock.ToString(),
                        s.IdProduit.ToString(),
                        s.StockPhysique.ToString(),
                        s.StockReserve.ToString(),
                        s.SeuilAlerte.ToString(),
                        s.PrixAchat?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                        EscapeCsvField(s.Emplacement)
                    }));
                }

                File.WriteAllText(dialog.FileName, sb.ToString(), Encoding.UTF8);
                MessageBox.Show("Export CSV terminé.", "Export CSV", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur export CSV : {ex.Message}", "Export CSV", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ImportCsv()
        {
            try
            {
                var vendeurId = UserSession.Instance.CurrentUser?.IdUser;
                if (vendeurId == null)
                {
                    MessageBox.Show("Aucun utilisateur connecté.", "Import CSV", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var dialog = new OpenFileDialog
                {
                    Title = "Importer un CSV de stocks",
                    Filter = "CSV (*.csv)|*.csv"
                };

                if (dialog.ShowDialog() != true) return;

                var lines = File.ReadAllLines(dialog.FileName);
                if (lines.Length == 0)
                {
                    MessageBox.Show("Le fichier CSV est vide.", "Import CSV", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                char delimiter = lines[0].Contains(';') ? ';' : ',';
                var header = ParseCsvLine(lines[0], delimiter).Select(h => h.Trim().ToLowerInvariant()).ToArray();

                if (header.Length != CsvHeaders.Length || !header.SequenceEqual(CsvHeaders))
                {
                    MessageBox.Show(
                        $"Colonnes CSV incompatibles.\n\nAttendu : {string.Join(";", CsvHeaders)}",
                        "Import CSV",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                using var db = new GroupyContext();
                var productIdsVendeur = db.Produits
                    .Where(p => p.IdVendeur == vendeurId)
                    .Select(p => p.IdProduit)
                    .ToHashSet();

                int imported = 0;

                for (int i = 1; i < lines.Length; i++)
                {
                    if (string.IsNullOrWhiteSpace(lines[i])) continue;

                    var cols = ParseCsvLine(lines[i], delimiter);
                    if (cols.Count != CsvHeaders.Length)
                    {
                        MessageBox.Show($"Ligne {i + 1} invalide : nombre de colonnes incorrect.", "Import CSV", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    if (!int.TryParse(cols[0], out int idStock)
                        || !int.TryParse(cols[1], out int idProduit)
                        || !int.TryParse(cols[2], out int stockPhysique)
                        || !int.TryParse(cols[3], out int stockReserve)
                        || !int.TryParse(cols[4], out int seuilAlerte))
                    {
                        MessageBox.Show($"Ligne {i + 1} invalide : types de données incorrects.", "Import CSV", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    decimal? prixAchat = null;
                    if (!string.IsNullOrWhiteSpace(cols[5]))
                    {
                        if (!decimal.TryParse(cols[5], NumberStyles.Number, CultureInfo.InvariantCulture, out var prix))
                        {
                            MessageBox.Show($"Ligne {i + 1} invalide : prix_achat incorrect.", "Import CSV", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }
                        prixAchat = prix;
                    }

                    var emplacement = string.IsNullOrWhiteSpace(cols[6]) ? null : cols[6].Trim();

                    if (!productIdsVendeur.Contains(idProduit))
                    {
                        MessageBox.Show($"Ligne {i + 1} invalide : id_produit {idProduit} n'appartient pas à votre compte.", "Import CSV", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    var stock = db.Stocks.FirstOrDefault(s => s.IdStock == idStock);
                    if (stock == null)
                    {
                        stock = db.Stocks.FirstOrDefault(s => s.IdProduit == idProduit);
                    }

                    if (stock == null)
                    {
                        stock = new Stock
                        {
                            IdProduit = idProduit,
                            StockPhysique = stockPhysique,
                            StockReserve = stockReserve,
                            SeuilAlerte = seuilAlerte,
                            PrixAchat = prixAchat,
                            Emplacement = emplacement
                        };
                        db.Stocks.Add(stock);
                    }
                    else
                    {
                        if (stock.IdProduit != idProduit)
                        {
                            MessageBox.Show($"Ligne {i + 1} invalide : id_stock {idStock} ne correspond pas à id_produit {idProduit}.", "Import CSV", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }

                        stock.StockPhysique = stockPhysique;
                        stock.StockReserve = stockReserve;
                        stock.SeuilAlerte = seuilAlerte;
                        stock.PrixAchat = prixAchat;
                        stock.Emplacement = emplacement;
                    }

                    imported++;
                }

                db.SaveChanges();
                MessageBox.Show($"Import CSV terminé. {imported} ligne(s) traitée(s).", "Import CSV", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur import CSV : {ex.Message}", "Import CSV", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string EscapeCsvField(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            if (!value.Contains(';') && !value.Contains('"') && !value.Contains('\n') && !value.Contains('\r')) return value;
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        private static List<string> ParseCsvLine(string line, char delimiter)
        {
            var result = new List<string>();
            var current = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == delimiter && !inQuotes)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            result.Add(current.ToString());
            return result;
        }
    }
}
