using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Media;
using GroupyV.Data;
using GroupyV.Services;
using LiveCharts;
using LiveCharts.Wpf;
using Microsoft.EntityFrameworkCore;

namespace GroupyV.ViewModels
{
    public class TopProduitDisplay
    {
        public string Nom { get; set; }
        public int Ventes { get; set; }
        public decimal CA { get; set; }
    }

    public class StockAlertDisplay
    {
        public string NomProduit { get; set; } = "—";
        public string Categorie { get; set; } = "—";
        public int StockPhysique { get; set; }
        public int SeuilAlerte { get; set; }
        public bool IsRupture => StockPhysique == 0;
    }

    public class DashboardViewModel : BaseViewModel
    {
        // ── KPIs ────────────────────────────────────────────────────────────────

        private string _totalCA = "0,00 €";
        public string TotalCA
        {
            get => _totalCA;
            set { _totalCA = value; OnPropertyChanged(); }
        }

        private int _commandesEnAttente;
        public int CommandesEnAttente
        {
            get => _commandesEnAttente;
            set { _commandesEnAttente = value; OnPropertyChanged(); }
        }

        private int _stockFaible;
        public int StockFaible
        {
            get => _stockFaible;
            set { _stockFaible = value; OnPropertyChanged(); }
        }

        private int _signalements;
        public int Signalements
        {
            get => _signalements;
            set { _signalements = value; OnPropertyChanged(); }
        }

        // ── Header ──────────────────────────────────────────────────────────────

        private string _userName = "Utilisateur";
        public string UserName
        {
            get => _userName;
            set { _userName = value; OnPropertyChanged(); }
        }

        private string _userRole = "Connecté";
        public string UserRole
        {
            get => _userRole;
            set { _userRole = value; OnPropertyChanged(); }
        }

        private string _userInitials = "U";
        public string UserInitials
        {
            get => _userInitials;
            set { _userInitials = value; OnPropertyChanged(); }
        }

        private string _sessionInfo = "Session active";
        public string SessionInfo
        {
            get => _sessionInfo;
            set { _sessionInfo = value; OnPropertyChanged(); }
        }

        // ── Graphique ───────────────────────────────────────────────────────────

        private SeriesCollection _chartSeries = new();
        public SeriesCollection ChartSeries
        {
            get => _chartSeries;
            set { _chartSeries = value; OnPropertyChanged(); }
        }

        private string[] _chartLabels = Array.Empty<string>();
        public string[] ChartLabels
        {
            get => _chartLabels;
            set { _chartLabels = value; OnPropertyChanged(); }
        }

        // ── Listes ──────────────────────────────────────────────────────────────

        private ObservableCollection<TopProduitDisplay> _topProduits = new();
        public ObservableCollection<TopProduitDisplay> TopProduits
        {
            get => _topProduits;
            set { _topProduits = value; OnPropertyChanged(); }
        }

        private ObservableCollection<StockAlertDisplay> _stockAlerts = new();
        public ObservableCollection<StockAlertDisplay> StockAlerts
        {
            get => _stockAlerts;
            set { _stockAlerts = value; OnPropertyChanged(); }
        }

        private bool _hasStockAlerts;
        public bool HasStockAlerts
        {
            get => _hasStockAlerts;
            set { _hasStockAlerts = value; OnPropertyChanged(); }
        }

        // ── Constructeur ────────────────────────────────────────────────────────

        public DashboardViewModel()
        {
            LoadAllData();
        }

        // ── Chargement ──────────────────────────────────────────────────────────

        private void LoadAllData()
        {
            try
            {
                Debug.WriteLine("=== DÉBUT CHARGEMENT DASHBOARD ===");
                LoadUserData();
                LoadBusinessData();
                Debug.WriteLine("=== FIN CHARGEMENT DASHBOARD ===");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ERREUR GLOBALE DASHBOARD: {ex.Message}");
                TotalCA = "Erreur";
            }
        }

        private void LoadUserData()
        {
            try
            {
                var session = UserSession.Instance;
                UserName = session.GetNomComplet();
                UserInitials = session.GetInitiales();
                UserRole = session.Role;
                SessionInfo = $"Session ouverte le {session.LoginTime:dd/MM} à {session.LoginTime:HH:mm}";
                Debug.WriteLine($"✓ Utilisateur chargé: {UserName} ({UserRole})");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ERREUR LoadUserData: {ex.Message}");
            }
        }

        private void LoadBusinessData()
        {
            try
            {
                var currentVendeurId = UserSession.Instance.CurrentUser?.IdUser;
                if (currentVendeurId == null)
                {
                    Debug.WriteLine("⚠ Aucun vendeur connecté");
                    TotalCA = "0,00 €";
                    CommandesEnAttente = 0;
                    StockFaible = 0;
                    Signalements = 0;
                    TopProduits.Clear();
                    return;
                }

                Debug.WriteLine($"🔍 ID Vendeur connecté: {currentVendeurId}");

                using var db = new GroupyContext();

                var canConnect = db.Database.CanConnect();
                Debug.WriteLine($"✓ Connexion DB: {canConnect}");

                if (!canConnect)
                    throw new Exception("Impossible de se connecter à la base de données");

                CalculerCA(db, currentVendeurId.Value);
                CalculerCommandesEnAttente(db, currentVendeurId.Value);
                CalculerStockFaible(db, currentVendeurId.Value);
                CalculerSignalements(db, currentVendeurId.Value);
                ChargerTopProduits(db, currentVendeurId.Value);
                InitialiserGraphique(db, currentVendeurId.Value);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ERREUR LoadBusinessData: {ex.Message}");
                Debug.WriteLine($"InnerException: {ex.InnerException?.Message}");
                TotalCA = "Erreur";
            }
        }

        private void CalculerCA(GroupyContext db, int vendeurId)
        {
            try
            {
                Debug.WriteLine("\n--- CALCUL CA ---");

                var toutesLesPreventes = db.Preventes
                    .Include(p => p.Produit)
                    .Include(p => p.Participations)
                    .AsNoTracking()
                    .ToList();

                var preventesDuVendeur = toutesLesPreventes
                    .Where(p => p.Produit != null && p.Produit.IdVendeur == vendeurId)
                    .ToList();

                var preventesTerminees = preventesDuVendeur
                    .Where(p => p.Statut != null && p.Statut.Equals("terminée", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                decimal caTotal = preventesTerminees.Sum(p => p.PrixPrevente * (p.Participations?.Count ?? 0));
                TotalCA = $"{caTotal:N2} €";

                Debug.WriteLine($"✓ CA Total: {TotalCA}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ERREUR CalculerCA: {ex.Message}");
                TotalCA = "Err";
            }
        }

        private void CalculerCommandesEnAttente(GroupyContext db, int vendeurId)
        {
            try
            {
                Debug.WriteLine("\n--- CALCUL COMMANDES EN ATTENTE ---");

                var toutesLesPreventes = db.Preventes
                    .Include(p => p.Produit)
                    .AsNoTracking()
                    .ToList();

                var enCours = toutesLesPreventes
                    .Where(p => p.Produit != null && p.Produit.IdVendeur == vendeurId
                             && p.Statut != null && p.Statut.Equals("en cours", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                CommandesEnAttente = enCours.Count;
                Debug.WriteLine($"✓ Commandes en attente: {CommandesEnAttente}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ERREUR CalculerCommandesEnAttente: {ex.Message}");
                CommandesEnAttente = 0;
            }
        }

        private void CalculerStockFaible(GroupyContext db, int vendeurId)
        {
            try
            {
                Debug.WriteLine("\n--- CALCUL STOCK FAIBLE ---");

                var produitsAvecStock = (from p in db.Produits
                                             .Include(p => p.Categorie)
                                         join s in db.Stocks on p.IdProduit equals s.IdProduit into stockGroup
                                         from s in stockGroup.DefaultIfEmpty()
                                         where p.IdVendeur == vendeurId
                                         select new { Produit = p, Stock = s })
                    .AsNoTracking()
                    .ToList();

                var alertes = produitsAvecStock
                    .Where(x => (x.Stock?.StockPhysique ?? 0) <= (x.Stock?.SeuilAlerte ?? 0))
                    .Select(x => new StockAlertDisplay
                    {
                        NomProduit = x.Produit.NomProduit ?? "—",
                        Categorie = x.Produit.Categorie?.Lib ?? "—",
                        StockPhysique = x.Stock?.StockPhysique ?? 0,
                        SeuilAlerte = x.Stock?.SeuilAlerte ?? 0
                    })
                    .OrderBy(a => a.StockPhysique)
                    .ToList();

                StockFaible = alertes.Count;
                StockAlerts.Clear();
                foreach (var alerte in alertes)
                    StockAlerts.Add(alerte);

                HasStockAlerts = alertes.Count > 0;
                Debug.WriteLine($"✓ Stocks faibles: {StockFaible}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ERREUR CalculerStockFaible: {ex.Message}");
                StockFaible = 0;
                HasStockAlerts = false;
            }
        }

        private void CalculerSignalements(GroupyContext db, int vendeurId)
        {
            try
            {
                Debug.WriteLine("\n--- CALCUL SIGNALEMENTS ---");

                var signalements = db.Signalements
                    .Include(sig => sig.Produit)
                    .Where(sig => sig.Produit.IdVendeur == vendeurId)
                    .ToList();

                Signalements = signalements.Count;
                Debug.WriteLine($"✓ Signalements: {Signalements}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ERREUR CalculerSignalements: {ex.Message}");
                Signalements = 0;
            }
        }

        private void ChargerTopProduits(GroupyContext db, int vendeurId)
        {
            try
            {
                Debug.WriteLine("\n--- CALCUL TOP PRODUITS ---");

                var toutesLesPreventes = db.Preventes
                    .Include(p => p.Produit)
                    .Include(p => p.Participations)
                    .AsNoTracking()
                    .ToList();

                var preventesNonAnnulees = toutesLesPreventes
                    .Where(p => p.Produit != null && p.Produit.IdVendeur == vendeurId
                             && (p.Statut == null || !p.Statut.Equals("annulée", StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                var topList = preventesNonAnnulees
                    .GroupBy(p => p.IdProduit)
                    .Select(g => new
                    {
                        Produit = g.First().Produit,
                        TotalVentes = g.Sum(p => p.Participations?.Count ?? 0),
                        TotalRevenu = g.Sum(p => (p.Participations?.Count ?? 0) * p.PrixPrevente)
                    })
                    .OrderByDescending(x => x.TotalVentes)
                    .Take(5)
                    .ToList();

                TopProduits.Clear();
                foreach (var item in topList)
                {
                    if (item.Produit != null)
                        TopProduits.Add(new TopProduitDisplay
                        {
                            Nom = item.Produit.NomProduit,
                            Ventes = item.TotalVentes,
                            CA = item.TotalRevenu
                        });
                }

                Debug.WriteLine($"✓ Top produits chargés: {TopProduits.Count}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ERREUR ChargerTopProduits: {ex.Message}");
                TopProduits.Clear();
            }
        }

        private void InitialiserGraphique(GroupyContext db, int vendeurId)
        {
            try
            {
                Debug.WriteLine("\n--- INITIALISATION GRAPHIQUE ---");

                var depuis = DateTime.Now.Date.AddDays(-29);
                var aujourdHui = DateTime.Now.Date;

                var participations = db.Participations
                    .Include(p => p.Prevente)
                        .ThenInclude(pr => pr.Produit)
                    .AsNoTracking()
                    .ToList()
                    .Where(p => p.Prevente?.Produit != null
                             && p.Prevente.Produit.IdVendeur == vendeurId
                             && p.DateParticipation.HasValue
                             && p.DateParticipation.Value.Date >= depuis)
                    .ToList();

                var labels = new System.Collections.Generic.List<string>();
                var ventesValues = new ChartValues<double>();
                var caValues = new ChartValues<double>();

                for (int i = 4; i >= 0; i--)
                {
                    var debutSemaine = aujourdHui.AddDays(-((i + 1) * 6) + 1);
                    var finSemaine = i == 0 ? aujourdHui : aujourdHui.AddDays(-(i * 6));

                    var participationsSemaine = participations
                        .Where(p => p.DateParticipation!.Value.Date >= debutSemaine
                                 && p.DateParticipation!.Value.Date <= finSemaine)
                        .ToList();

                    ventesValues.Add(participationsSemaine.Count);
                    caValues.Add((double)participationsSemaine.Sum(p => p.Prevente?.PrixPrevente ?? 0));
                    labels.Add($"{debutSemaine:dd/MM}");
                }

                ChartSeries.Clear();
                ChartSeries.Add(new LineSeries
                {
                    Title = "Ventes",
                    Values = ventesValues,
                    Stroke = new SolidColorBrush(Color.FromRgb(108, 93, 211)),
                    Fill = new SolidColorBrush(Color.FromArgb(40, 108, 93, 211)),
                    PointGeometrySize = 8,
                    StrokeThickness = 2,
                    LineSmoothness = 0.6
                });
                ChartSeries.Add(new LineSeries
                {
                    Title = "CA (€)",
                    Values = caValues,
                    Stroke = new SolidColorBrush(Color.FromRgb(69, 227, 184)),
                    Fill = new SolidColorBrush(Color.FromArgb(25, 69, 227, 184)),
                    PointGeometrySize = 8,
                    StrokeThickness = 2,
                    LineSmoothness = 0.6,
                    ScalesYAt = 1
                });

                ChartLabels = labels.ToArray();
                Debug.WriteLine("✓ Graphique initialisé");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ERREUR InitialiserGraphique: {ex.Message}");
                ChartSeries.Clear();
                ChartSeries.Add(new LineSeries
                {
                    Title = "Ventes",
                    Values = new ChartValues<double> { 0 },
                    Stroke = new SolidColorBrush(Color.FromRgb(108, 93, 211)),
                    Fill = new SolidColorBrush(Color.FromArgb(40, 108, 93, 211)),
                    PointGeometrySize = 0
                });
                ChartLabels = new[] { "—" };
            }
        }
    }
}
