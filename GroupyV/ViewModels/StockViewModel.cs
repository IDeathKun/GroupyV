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
            set { _searchText = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasSearchText)); ApplyFilter(); }
        }

        private string _selectedFilter = "Tous";
        public string SelectedFilter
        {
            get => _selectedFilter;
            set { _selectedFilter = value; OnPropertyChanged(); ApplyFilter(); }
        }

        // Compteurs pour les badges des filtres
        private int _countTous;
        public int CountTous { get => _countTous; set { _countTous = value; OnPropertyChanged(); } }

        private int _countAlertes;
        public int CountAlertes { get => _countAlertes; set { _countAlertes = value; OnPropertyChanged(); } }

        private int _countOk;
        public int CountOk { get => _countOk; set { _countOk = value; OnPropertyChanged(); } }

        // Recherche : présence de texte (pour afficher le bouton effacer)
        public bool HasSearchText => !string.IsNullOrEmpty(SearchText);

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

        // --- TRI ---
        private string _sortColumn = "NomProduit";
        public string SortColumn
        {
            get => _sortColumn;
            set { _sortColumn = value; OnPropertyChanged(); ApplyFilter(); }
        }

        private bool _sortAscending = true;
        public bool SortAscending
        {
            get => _sortAscending;
            set { _sortAscending = value; OnPropertyChanged(); ApplyFilter(); }
        }

        // --- STATS MOUVEMENTS 30J ---
        private int _totalEntrees30j;
        public int TotalEntrees30j { get => _totalEntrees30j; set { _totalEntrees30j = value; OnPropertyChanged(); } }

        private int _totalSorties30j;
        public int TotalSorties30j { get => _totalSorties30j; set { _totalSorties30j = value; OnPropertyChanged(); } }

        private int _qteTotaleEntrees;
        public int QteTotaleEntrees { get => _qteTotaleEntrees; set { _qteTotaleEntrees = value; OnPropertyChanged(); } }

        private int _qteTotaleSorties;
        public int QteTotaleSorties { get => _qteTotaleSorties; set { _qteTotaleSorties = value; OnPropertyChanged(); } }

        private string _produitPlusMouvemente = "—";
        public string ProduitPlusMouvemente { get => _produitPlusMouvemente; set { _produitPlusMouvemente = value; OnPropertyChanged(); } }

        // --- COMMANDES ---
        public ICommand OpenAddStockCommand { get; }
        public ICommand OpenRemoveStockCommand { get; }
        public ICommand CloseDialogCommand { get; }
        public ICommand ConfirmMouvementCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand ExportCsvCommand { get; }
        public ICommand ImportCsvCommand { get; }
        public ICommand SortCommand { get; }
        public ICommand ClearSearchCommand { get; }
        public ICommand SetFilterCommand { get; }

        public StockViewModel()
        {
            OpenAddStockCommand = new RelayCommand(OpenAddStock);
            OpenRemoveStockCommand = new RelayCommand(OpenRemoveStock);
            CloseDialogCommand = new RelayCommand(_ => IsDialogOpen = false);
            ConfirmMouvementCommand = new RelayCommand(ConfirmMouvement);
            RefreshCommand = new RelayCommand(_ => LoadData());
            ExportCsvCommand = new RelayCommand(_ => ExportCsv());
            ImportCsvCommand = new RelayCommand(_ => ImportCsv());
            SortCommand = new RelayCommand(ExecuteSort);
            ClearSearchCommand = new RelayCommand(_ => SearchText = string.Empty);
            SetFilterCommand = new RelayCommand(param => { if (param is string f) SelectedFilter = f; });

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

                // Compteurs filtres
                CountTous     = stocks.Count;
                CountAlertes  = stocks.Count(s => s.IsAlerte);
                CountOk       = stocks.Count(s => !s.IsAlerte);

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

                // Stats 30j
                TotalEntrees30j  = mouvements.Count(m => m.IsEntree);
                TotalSorties30j  = mouvements.Count(m => !m.IsEntree);
                QteTotaleEntrees = mouvements.Where(m => m.IsEntree).Sum(m => m.Quantite);
                QteTotaleSorties = mouvements.Where(m => !m.IsEntree).Sum(m => m.Quantite);
                ProduitPlusMouvemente = mouvements
                    .GroupBy(m => m.Produit)
                    .OrderByDescending(g => g.Count())
                    .Select(g => g.Key)
                    .FirstOrDefault() ?? "—";
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
                "Stock OK"  => filtered.Where(s => !s.IsAlerte),
                _           => filtered
            };

            // Tri
            filtered = (SortColumn, SortAscending) switch
            {
                ("NomProduit",    true)  => filtered.OrderBy(s => s.NomProduit),
                ("NomProduit",    false) => filtered.OrderByDescending(s => s.NomProduit),
                ("StockPhysique", true)  => filtered.OrderBy(s => s.StockPhysique),
                ("StockPhysique", false) => filtered.OrderByDescending(s => s.StockPhysique),
                ("StockReserve",  true)  => filtered.OrderBy(s => s.StockReserve),
                ("StockReserve",  false) => filtered.OrderByDescending(s => s.StockReserve),
                ("StockDisponible", true)  => filtered.OrderBy(s => s.StockDisponible),
                ("StockDisponible", false) => filtered.OrderByDescending(s => s.StockDisponible),
                ("SeuilAlerte",   true)  => filtered.OrderBy(s => s.SeuilAlerte),
                ("SeuilAlerte",   false) => filtered.OrderByDescending(s => s.SeuilAlerte),
                ("PrixAchat",     true)  => filtered.OrderBy(s => s.PrixAchat),
                ("PrixAchat",     false) => filtered.OrderByDescending(s => s.PrixAchat),
                ("IsAlerte",      true)  => filtered.OrderByDescending(s => s.IsAlerte),
                ("IsAlerte",      false) => filtered.OrderBy(s => s.IsAlerte),
                _ => filtered.OrderBy(s => s.NomProduit)
            };

            StocksList = new ObservableCollection<StockItemDisplay>(filtered);
        }

        private void ExecuteSort(object param)
        {
            if (param is not string column) return;
            if (SortColumn == column)
                SortAscending = !SortAscending;
            else
            {
                SortColumn = column;
                SortAscending = true;
            }
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
                    MessageBox.Show("Aucun utilisateur connecté.", "Export", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var now = DateTime.Now;
                var dialog = new SaveFileDialog
                {
                    Title    = "Exporter l'inventaire",
                    Filter   = "Fichier CSV (*.csv)|*.csv",
                    FileName = $"inventaire_GroupyV_{now:yyyy-MM-dd}.csv"
                };
                if (dialog.ShowDialog() != true) return;

                using var db = new GroupyContext();
                var data = (from p in db.Produits.Include(p => p.Categorie)
                            join s in db.Stocks on p.IdProduit equals s.IdProduit into sg
                            from s in sg.DefaultIfEmpty()
                            where p.IdVendeur == vendeurId
                            select new { Produit = p, Stock = s })
                           .AsNoTracking()
                           .ToList()
                           .OrderBy(x => x.Produit.Categorie?.Lib ?? "")
                           .ThenBy(x => x.Produit.NomProduit ?? "")
                           .ToList();

                var nomVendeur   = UserSession.Instance.GetNomComplet();
                int totalUnites  = data.Sum(x => x.Stock?.StockPhysique ?? 0);
                int totalAlertes = data.Count(x => (x.Stock?.StockPhysique ?? 0) <= (x.Stock?.SeuilAlerte ?? 0));
                decimal valeur   = data.Sum(x => (x.Stock?.PrixAchat ?? 0) * (x.Stock?.StockPhysique ?? 0));

                // Culture française : virgule décimale, espace milliers
                var frFR = CultureInfo.GetCultureInfo("fr-FR");

                var sb = new StringBuilder();

                // Indicateur de séparateur pour Excel
                sb.AppendLine("sep=;");
                sb.AppendLine();

                // ── Bloc d'en-tête (ASCII pur — compatible Windows-1252) ─────────
                sb.AppendLine("# +==================================================+");
                sb.AppendLine("# |         GROUPYV - EXPORT INVENTAIRE               |");
                sb.AppendLine("# +==================================================+");
                sb.AppendLine($"# Vendeur           : {nomVendeur}");
                sb.AppendLine($"# Date d'export     : {now:dd/MM/yyyy} a {now:HH:mm}");
                sb.AppendLine($"# Produits exportes : {data.Count}");
                sb.AppendLine($"# Produits en alerte: {totalAlertes}");
                sb.AppendLine("#");
                sb.AppendLine("# /!\\ Colonnes modifiables a l'import :");
                sb.AppendLine("#     Stock Physique ; Stock Reserve ; Seuil Alerte ; Prix Achat (EUR) ; Emplacement");
                sb.AppendLine("# --------------------------------------------------");
                sb.AppendLine();

                // ── En-têtes de colonnes ────────────────────────────────────────
                sb.AppendLine("Produit;Categorie;Stock Physique;Stock Reserve;Disponible;Seuil Alerte;Prix Achat (EUR);Emplacement;Statut");

                // ── Données ─────────────────────────────────────────────────────
                foreach (var x in data)
                {
                    int    physique = x.Stock?.StockPhysique ?? 0;
                    int    reserve  = x.Stock?.StockReserve  ?? 0;
                    int    dispo    = physique - reserve;
                    int    seuil    = x.Stock?.SeuilAlerte   ?? 0;
                    bool   alerte   = physique <= seuil;
                    string statut   = physique == 0 ? "RUPTURE" : alerte ? "ALERTE" : "OK";
                    // "0.00" avec fr-FR = virgule décimale, sans séparateur de milliers
                    string prix     = x.Stock?.PrixAchat?.ToString("0.00", frFR) ?? "";

                    sb.AppendLine(string.Join(';', new[]
                    {
                        EscapeCsvField(x.Produit.NomProduit      ?? ""),
                        EscapeCsvField(x.Produit.Categorie?.Lib  ?? ""),
                        physique.ToString(),
                        reserve.ToString(),
                        dispo.ToString(),
                        seuil.ToString(),
                        prix,
                        EscapeCsvField(x.Stock?.Emplacement ?? ""),
                        statut
                    }));
                }

                // ── Résumé ──────────────────────────────────────────────────────
                sb.AppendLine();
                sb.AppendLine("# +==================================================+");
                sb.AppendLine("# |                    RESUME                         |");
                sb.AppendLine("# +==================================================+");
                sb.AppendLine($"# Total unites en stock  : {totalUnites}");
                sb.AppendLine($"# Produits en alerte     : {totalAlertes}");
                sb.AppendLine($"# Valeur totale estimee  : {valeur.ToString("0.00", frFR)} EUR");
                sb.AppendLine("# --------------------------------------------------");


                // Windows-1252 = encodage natif d'Excel francais
                var encoding = Encoding.GetEncoding(1252);
                File.WriteAllText(dialog.FileName, sb.ToString(), encoding);

                var result = MessageBox.Show(
                    $"Export réussi ✓\n\n" +
                    $"  • {data.Count} produit(s) exporté(s)\n" +
                    $"  • {totalUnites} unités au total\n" +
                    $"  • {totalAlertes} produit(s) en alerte\n" +
                    $"  • Valeur estimée : {valeur:N2} €\n\n" +
                    $"Ouvrir le fichier maintenant ?",
                    "Export terminé", MessageBoxButton.YesNo, MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                    System.Diagnostics.Process.Start(
                        new System.Diagnostics.ProcessStartInfo(dialog.FileName) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur lors de l'export : {ex.Message}", "Erreur export", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ImportCsv()
        {
            try
            {
                var vendeurId = UserSession.Instance.CurrentUser?.IdUser;
                if (vendeurId == null)
                {
                    MessageBox.Show("Aucun utilisateur connecté.", "Import", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var dialog = new OpenFileDialog
                {
                    Title  = "Importer un inventaire CSV",
                    Filter = "Fichier CSV (*.csv)|*.csv"
                };
                if (dialog.ShowDialog() != true) return;

                // ── Lecture & nettoyage ──────────────────────────────────────────
                var allLines = File.ReadAllLines(dialog.FileName, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                if (allLines.Length == 0)
                {
                    MessageBox.Show("Le fichier est vide.", "Import", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Détecter le délimiteur (premier ligne non-commentaire)
                char delim = ';';
                foreach (var l in allLines)
                {
                    var trimmed = l.Trim();
                    if (trimmed.StartsWith("#") || trimmed.StartsWith("sep=") || string.IsNullOrWhiteSpace(trimmed)) continue;
                    delim = trimmed.Contains(';') ? ';' : ',';
                    break;
                }

                // Filtrer : ignorer lignes vides, commentaires et directive sep=
                var dataLines = allLines
                    .Where(l => !string.IsNullOrWhiteSpace(l)
                             && !l.TrimStart().StartsWith("#")
                             && !l.TrimStart().StartsWith("sep="))
                    .ToList();

                if (dataLines.Count == 0)
                {
                    MessageBox.Show("Aucune donnée trouvée dans le fichier.", "Import", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // ── Détection du format ──────────────────────────────────────────
                var headerCols = ParseCsvLine(dataLines[0], delim)
                    .Select(h => h.Trim().ToLowerInvariant())
                    .ToArray();

                bool isNewFormat  = headerCols.Length >= 1 && headerCols[0] == "produit";
                bool isOldFormat  = headerCols.Length >= 1 && headerCols[0] == "id_stock";

                if (!isNewFormat && !isOldFormat)
                {
                    MessageBox.Show(
                        "Format de fichier non reconnu.\n\n" +
                        "Le fichier doit avoir été exporté depuis GroupyV.\n" +
                        "La première colonne doit être « Produit » (format actuel)\n" +
                        "ou « id_stock » (ancien format).",
                        "Format invalide", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                using var db = new GroupyContext();

                // ── Import nouveau format (par nom de produit) ───────────────────
                if (isNewFormat)
                {
                    // Charger tous les produits du vendeur (id + nom)
                    var produits = db.Produits
                        .Where(p => p.IdVendeur == vendeurId)
                        .Select(p => new { p.IdProduit, Nom = p.NomProduit ?? "" })
                        .ToList();

                    // Charger les stocks existants
                    var stocks = db.Stocks
                        .Where(s => produits.Select(p => p.IdProduit).Contains(s.IdProduit))
                        .ToList();

                    int updated  = 0;
                    int created  = 0;
                    int skipped  = 0;
                    var warnings = new List<string>();

                    for (int i = 1; i < dataLines.Count; i++)
                    {
                        var cols = ParseCsvLine(dataLines[i], delim);
                        if (cols.Count < 8) { skipped++; continue; }

                        string nomProduit = cols[0].Trim();
                        if (string.IsNullOrWhiteSpace(nomProduit)) { skipped++; continue; }

                        // Recherche du produit (insensible à la casse)
                        var produit = produits.FirstOrDefault(p =>
                            string.Equals(p.Nom, nomProduit, StringComparison.OrdinalIgnoreCase));

                        if (produit == null)
                        {
                            warnings.Add($"  • Ligne {i + 1} — « {nomProduit} » : produit introuvable (ignoré)");
                            skipped++;
                            continue;
                        }

                        // Parsing des colonnes modifiables
                        // Cols : 0=Produit 1=Catégorie 2=StockPhysique 3=StockRéservé
                        //        4=Disponible(ignoré) 5=SeuilAlerte 6=PrixAchat 7=Emplacement 8=Statut(ignoré)
                        if (!int.TryParse(cols[2].Trim(), out int physique)
                         || !int.TryParse(cols[3].Trim(), out int reserve)
                         || !int.TryParse(cols[5].Trim(), out int seuil))
                        {
                            warnings.Add($"  • Ligne {i + 1} — « {nomProduit} » : valeurs numériques invalides (ignoré)");
                            skipped++;
                            continue;
                        }

                        decimal? prix = null;
                        if (!string.IsNullOrWhiteSpace(cols[6]))
                        {
                            var prixStr = cols[6].Trim();
                            // Accepte virgule (fr) ET point (en) comme séparateur décimal
                            if (decimal.TryParse(prixStr, NumberStyles.Number, CultureInfo.GetCultureInfo("fr-FR"), out var p)
                             || decimal.TryParse(prixStr, NumberStyles.Number, CultureInfo.InvariantCulture, out p))
                                prix = p;
                        }

                        string emplacement = cols.Count > 7 && !string.IsNullOrWhiteSpace(cols[7])
                            ? cols[7].Trim() : null;

                        var stock = stocks.FirstOrDefault(s => s.IdProduit == produit.IdProduit);
                        if (stock == null)
                        {
                            stock = new Stock
                            {
                                IdProduit     = produit.IdProduit,
                                StockPhysique = physique,
                                StockReserve  = reserve,
                                SeuilAlerte   = seuil,
                                PrixAchat     = prix,
                                Emplacement   = emplacement
                            };
                            db.Stocks.Add(stock);
                            created++;
                        }
                        else
                        {
                            stock.StockPhysique = physique;
                            stock.StockReserve  = reserve;
                            stock.SeuilAlerte   = seuil;
                            stock.PrixAchat     = prix;
                            stock.Emplacement   = emplacement;
                            updated++;
                        }
                    }

                    db.SaveChanges();
                    LoadData();

                    var msg = new StringBuilder();
                    msg.AppendLine("Import terminé ✓\n");
                    msg.AppendLine($"  • {updated} produit(s) mis à jour");
                    msg.AppendLine($"  • {created} stock(s) créé(s)");
                    if (skipped > 0) msg.AppendLine($"  • {skipped} ligne(s) ignorée(s)");
                    if (warnings.Count > 0)
                    {
                        msg.AppendLine("\nAvertissements :");
                        foreach (var w in warnings) msg.AppendLine(w);
                    }

                    MessageBox.Show(msg.ToString(), "Import terminé",
                        MessageBoxButton.OK,
                        warnings.Count > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
                    return;
                }

                // ── Import ancien format (rétrocompatibilité par id) ─────────────
                if (isOldFormat)
                {
                    var productIdsVendeur = db.Produits
                        .Where(p => p.IdVendeur == vendeurId)
                        .Select(p => p.IdProduit)
                        .ToHashSet();

                    int imported = 0;
                    for (int i = 1; i < dataLines.Count; i++)
                    {
                        var cols = ParseCsvLine(dataLines[i], delim);
                        if (cols.Count < 7) continue;

                        if (!int.TryParse(cols[0], out int idStock)
                         || !int.TryParse(cols[1], out int idProduit)
                         || !int.TryParse(cols[2], out int stockPhysique)
                         || !int.TryParse(cols[3], out int stockReserve)
                         || !int.TryParse(cols[4], out int seuilAlerte))
                            continue;

                        if (!productIdsVendeur.Contains(idProduit)) continue;

                        decimal? prixAchat = null;
                        if (!string.IsNullOrWhiteSpace(cols[5]))
                        {
                            var paStr = cols[5].Trim();
                            if (decimal.TryParse(paStr, NumberStyles.Number, CultureInfo.GetCultureInfo("fr-FR"), out var pa)
                             || decimal.TryParse(paStr, NumberStyles.Number, CultureInfo.InvariantCulture, out pa))
                                prixAchat = pa;
                        }

                        string emplacement = string.IsNullOrWhiteSpace(cols[6]) ? null : cols[6].Trim();

                        var stock = db.Stocks.FirstOrDefault(s => s.IdStock == idStock)
                                 ?? db.Stocks.FirstOrDefault(s => s.IdProduit == idProduit);

                        if (stock == null)
                        {
                            db.Stocks.Add(new Stock
                            {
                                IdProduit     = idProduit,
                                StockPhysique = stockPhysique,
                                StockReserve  = stockReserve,
                                SeuilAlerte   = seuilAlerte,
                                PrixAchat     = prixAchat,
                                Emplacement   = emplacement
                            });
                        }
                        else
                        {
                            stock.StockPhysique = stockPhysique;
                            stock.StockReserve  = stockReserve;
                            stock.SeuilAlerte   = seuilAlerte;
                            stock.PrixAchat     = prixAchat;
                            stock.Emplacement   = emplacement;
                        }
                        imported++;
                    }

                    db.SaveChanges();
                    LoadData();
                    MessageBox.Show(
                        $"Import (ancien format) terminé ✓\n\n  • {imported} ligne(s) traitée(s)",
                        "Import terminé", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erreur lors de l'import : {ex.Message}", "Erreur import", MessageBoxButton.OK, MessageBoxImage.Error);
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
