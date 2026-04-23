using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GroupyV.Data;
using GroupyV.Models;
using GroupyV.Services;
using LiveCharts;
using LiveCharts.Wpf;
using Microsoft.EntityFrameworkCore;

namespace GroupyV.Views
{
    public partial class DashboardView : UserControl
    {
        // Propriétés de dépendance pour les KPIs
        public static readonly DependencyProperty TotalCAProperty = 
            DependencyProperty.Register("TotalCA", typeof(string), typeof(DashboardView), new PropertyMetadata("0,00 €"));

        public static readonly DependencyProperty CommandesEnAttenteProperty = 
            DependencyProperty.Register("CommandesEnAttente", typeof(int), typeof(DashboardView), new PropertyMetadata(0));

        public static readonly DependencyProperty StockFaibleProperty = 
            DependencyProperty.Register("StockFaible", typeof(int), typeof(DashboardView), new PropertyMetadata(0));

        public static readonly DependencyProperty SignalementsProperty = 
            DependencyProperty.Register("Signalements", typeof(int), typeof(DashboardView), new PropertyMetadata(0));

        // Propriétés de dépendance pour le header
        public static readonly DependencyProperty UserNameProperty = 
            DependencyProperty.Register("UserName", typeof(string), typeof(DashboardView), new PropertyMetadata("Utilisateur"));

        public static readonly DependencyProperty UserRoleProperty = 
            DependencyProperty.Register("UserRole", typeof(string), typeof(DashboardView), new PropertyMetadata("Connecté"));

        public static readonly DependencyProperty UserInitialsProperty = 
            DependencyProperty.Register("UserInitials", typeof(string), typeof(DashboardView), new PropertyMetadata("U"));

        public static readonly DependencyProperty SessionInfoProperty = 
            DependencyProperty.Register("SessionInfo", typeof(string), typeof(DashboardView), new PropertyMetadata("Session active"));

        // Propriétés de dépendance pour les graphiques
        public static readonly DependencyProperty ChartSeriesProperty = 
            DependencyProperty.Register("ChartSeries", typeof(SeriesCollection), typeof(DashboardView), new PropertyMetadata(new SeriesCollection()));

        public static readonly DependencyProperty ChartLabelsProperty = 
            DependencyProperty.Register("ChartLabels", typeof(string[]), typeof(DashboardView), new PropertyMetadata(new string[] { }));

        public static readonly DependencyProperty TopProduitsProperty = 
            DependencyProperty.Register("TopProduits", typeof(ObservableCollection<TopProduitDisplay>), typeof(DashboardView), 
                new PropertyMetadata(new ObservableCollection<TopProduitDisplay>()));

        public static readonly DependencyProperty StockAlertsProperty =
            DependencyProperty.Register("StockAlerts", typeof(ObservableCollection<StockAlertDisplay>), typeof(DashboardView),
                new PropertyMetadata(new ObservableCollection<StockAlertDisplay>()));

        public static readonly DependencyProperty HasStockAlertsProperty =
            DependencyProperty.Register("HasStockAlerts", typeof(bool), typeof(DashboardView), new PropertyMetadata(false));

        // Propriétés publiques
        public string TotalCA
        {
            get => (string)GetValue(TotalCAProperty);
            set => SetValue(TotalCAProperty, value);
        }

        public int CommandesEnAttente
        {
            get => (int)GetValue(CommandesEnAttenteProperty);
            set => SetValue(CommandesEnAttenteProperty, value);
        }

        public int StockFaible
        {
            get => (int)GetValue(StockFaibleProperty);
            set => SetValue(StockFaibleProperty, value);
        }

        public int Signalements
        {
            get => (int)GetValue(SignalementsProperty);
            set => SetValue(SignalementsProperty, value);
        }

        public string UserName
        {
            get => (string)GetValue(UserNameProperty);
            set => SetValue(UserNameProperty, value);
        }

        public string UserRole
        {
            get => (string)GetValue(UserRoleProperty);
            set => SetValue(UserRoleProperty, value);
        }

        public string UserInitials
        {
            get => (string)GetValue(UserInitialsProperty);
            set => SetValue(UserInitialsProperty, value);
        }

        public string SessionInfo
        {
            get => (string)GetValue(SessionInfoProperty);
            set => SetValue(SessionInfoProperty, value);
        }

        public SeriesCollection ChartSeries
        {
            get => (SeriesCollection)GetValue(ChartSeriesProperty);
            set => SetValue(ChartSeriesProperty, value);
        }

        public string[] ChartLabels
        {
            get => (string[])GetValue(ChartLabelsProperty);
            set => SetValue(ChartLabelsProperty, value);
        }

        public ObservableCollection<TopProduitDisplay> TopProduits
        {
            get => (ObservableCollection<TopProduitDisplay>)GetValue(TopProduitsProperty);
            set => SetValue(TopProduitsProperty, value);
        }

        public ObservableCollection<StockAlertDisplay> StockAlerts
        {
            get => (ObservableCollection<StockAlertDisplay>)GetValue(StockAlertsProperty);
            set => SetValue(StockAlertsProperty, value);
        }

        public bool HasStockAlerts
        {
            get => (bool)GetValue(HasStockAlertsProperty);
            set => SetValue(HasStockAlertsProperty, value);
        }

        public DashboardView()
        {
            InitializeComponent();

            // Initialiser les collections
            ChartSeries = new SeriesCollection();
            TopProduits = new ObservableCollection<TopProduitDisplay>();
            StockAlerts = new ObservableCollection<StockAlertDisplay>();

            DataContext = this;

            // Empêcher le DataTemplate/ContentPresenter d'écraser le DataContext
            DataContextChanged += (s, e) =>
            {
                if (e.NewValue != this)
                    DataContext = this;
            };

            Loaded += (s, e) => LoadAllData();
        }

        private void LoadAllData()
        {
            try
            {
                Debug.WriteLine("=== DÉBUT CHARGEMENT DASHBOARD ===");

                // 1. Charger les infos utilisateur
                LoadUserData();

                // 2. Charger les données métier
                LoadBusinessData();

                Debug.WriteLine("=== FIN CHARGEMENT DASHBOARD ===");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ERREUR GLOBALE DASHBOARD: {ex.Message}");
                Debug.WriteLine($"StackTrace: {ex.StackTrace}");
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

                using (var db = new GroupyContext())
                {
                    // Test de connexion
                    var canConnect = db.Database.CanConnect();
                    Debug.WriteLine($"✓ Connexion DB: {canConnect}");

                    if (!canConnect)
                    {
                        throw new Exception("Impossible de se connecter à la base de données");
                    }

                    // --- 1. CALCUL DU CA ---
                    CalculerCA(db, currentVendeurId.Value);

                    // --- 2. COMMANDES EN ATTENTE ---
                    CalculerCommandesEnAttente(db, currentVendeurId.Value);

                    // --- 3. ALERTE STOCK ---
                    CalculerStockFaible(db, currentVendeurId.Value);

                    // --- 4. SIGNALEMENTS ---
                    CalculerSignalements(db, currentVendeurId.Value);

                    // --- 5. TOP PRODUITS ---
                    ChargerTopProduits(db, currentVendeurId.Value);

                    // --- 6. GRAPHIQUE ---
                    InitialiserGraphique(db, currentVendeurId.Value);
                }
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

                // Charger TOUTES les préventes avec leurs relations SANS FILTRE SQL
                var toutesLesPreventes = db.Preventes
                    .Include(p => p.Produit)
                    .Include(p => p.Participations)
                    .AsNoTracking()
                    .ToList();

                Debug.WriteLine($"Total préventes dans la DB: {toutesLesPreventes.Count}");

                // Filtrer en mémoire par vendeur
                var preventesDuVendeur = toutesLesPreventes
                    .Where(p => p.Produit != null && p.Produit.IdVendeur == vendeurId)
                    .ToList();

                Debug.WriteLine($"Préventes du vendeur: {preventesDuVendeur.Count}");

                // Filtrer en mémoire les préventes terminées
                var preventesTerminees = preventesDuVendeur
                    .Where(p => p.Statut != null && p.Statut.Equals("terminée", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                Debug.WriteLine($"Préventes terminées: {preventesTerminees.Count}");

                foreach (var p in preventesTerminees)
                {
                    var nbParticipations = p.Participations?.Count ?? 0;
                    Debug.WriteLine($"  - Prévente #{p.IdPrevente}: {nbParticipations} participations × {p.PrixPrevente}€ = {nbParticipations * p.PrixPrevente}€");
                }

                // Calculer le CA total
                decimal caTotal = preventesTerminees.Sum(p => p.PrixPrevente * (p.Participations?.Count ?? 0));
                TotalCA = $"{caTotal:N2} €";

                Debug.WriteLine($"✓ CA Total: {TotalCA}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ERREUR CalculerCA: {ex.Message}");
                Debug.WriteLine($"StackTrace: {ex.StackTrace}");
                TotalCA = "Err";
            }
        }

        private void CalculerCommandesEnAttente(GroupyContext db, int vendeurId)
        {
            try
            {
                Debug.WriteLine("\n--- CALCUL COMMANDES EN ATTENTE ---");

                // Charger TOUTES les préventes SANS FILTRE SQL
                var toutesLesPreventes = db.Preventes
                    .Include(p => p.Produit)
                    .AsNoTracking()
                    .ToList();

                // Filtrer en mémoire
                var preventesDuVendeur = toutesLesPreventes
                    .Where(p => p.Produit != null && p.Produit.IdVendeur == vendeurId)
                    .ToList();

                var enCours = preventesDuVendeur
                    .Where(p => p.Statut != null && p.Statut.Equals("en cours", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                CommandesEnAttente = enCours.Count;
                Debug.WriteLine($"✓ Commandes en attente: {CommandesEnAttente}");

                foreach (var p in enCours)
                {
                    Debug.WriteLine($"  - Prévente #{p.IdPrevente}: {p.Statut}");
                }
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

                // Left-join produits → stocks (même logique que StockView)
                var produitsAvecStock = (from p in db.Produits
                                             .Include(p => p.Categorie)
                                         join s in db.Stocks on p.IdProduit equals s.IdProduit into stockGroup
                                         from s in stockGroup.DefaultIfEmpty()
                                         where p.IdVendeur == vendeurId
                                         select new { Produit = p, Stock = s })
                    .AsNoTracking()
                    .ToList();

                Debug.WriteLine($"Total produits du vendeur: {produitsAvecStock.Count}");

                var alertes = produitsAvecStock
                    .Where(x =>
                    {
                        int physique = x.Stock?.StockPhysique ?? 0;
                        int seuil = x.Stock?.SeuilAlerte ?? 0;
                        // Même logique que StockView : IsAlerte => StockPhysique <= SeuilAlerte
                        return physique <= seuil;
                    })
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
                {
                    StockAlerts.Add(alerte);
                    Debug.WriteLine($"  - {alerte.NomProduit}: Stock={alerte.StockPhysique}, Seuil={alerte.SeuilAlerte}");
                }

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

                // Charger TOUTES les préventes SANS FILTRE SQL
                var toutesLesPreventes = db.Preventes
                    .Include(p => p.Produit)
                    .Include(p => p.Participations)
                    .AsNoTracking()
                    .ToList();

                Debug.WriteLine($"Total préventes dans la DB: {toutesLesPreventes.Count}");

                // Filtrer en mémoire par vendeur
                var preventesDuVendeur = toutesLesPreventes
                    .Where(p => p.Produit != null && p.Produit.IdVendeur == vendeurId)
                    .ToList();

                Debug.WriteLine($"Préventes du vendeur: {preventesDuVendeur.Count}");

                // Filtrer les non annulées en mémoire
                var preventesNonAnnulees = preventesDuVendeur
                    .Where(p => p.Statut == null || !p.Statut.Equals("annulée", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                Debug.WriteLine($"Préventes non annulées: {preventesNonAnnulees.Count}");

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
                    {
                        var display = new TopProduitDisplay
                        {
                            Nom = item.Produit.NomProduit,
                            Ventes = item.TotalVentes,
                            CA = item.TotalRevenu
                        };
                        TopProduits.Add(display);
                        Debug.WriteLine($"  - {display.Nom}: {display.Ventes} ventes, {display.CA}€");
                    }
                }

                Debug.WriteLine($"✓ Top produits chargés: {TopProduits.Count}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ERREUR ChargerTopProduits: {ex.Message}");
                Debug.WriteLine($"StackTrace: {ex.StackTrace}");
                TopProduits.Clear();
            }
        }

        private void InitialiserGraphique(GroupyContext db, int vendeurId)
        {
            try
            {
                Debug.WriteLine("\n--- INITIALISATION GRAPHIQUE (données réelles) ---");

                // Récupérer les participations des 30 derniers jours pour ce vendeur
                var depuis = DateTime.Now.Date.AddDays(-29);

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

                Debug.WriteLine($"Participations (30j): {participations.Count}");

                // Grouper par semaine (5 semaines glissantes)
                var aujourdHui = DateTime.Now.Date;
                var labels = new List<string>();
                var ventesValues = new ChartValues<double>();
                var caValues = new ChartValues<double>();

                // Découper en 5 périodes de ~6 jours pour les 30 derniers jours
                for (int i = 4; i >= 0; i--)
                {
                    var debutSemaine = aujourdHui.AddDays(-((i + 1) * 6) + 1);
                    var finSemaine = aujourdHui.AddDays(-(i * 6));
                    if (i == 0) finSemaine = aujourdHui; // inclure aujourd'hui

                    var participationsSemaine = participations
                        .Where(p => p.DateParticipation.Value.Date >= debutSemaine
                                 && p.DateParticipation.Value.Date <= finSemaine)
                        .ToList();

                    int nbVentes = participationsSemaine.Count;
                    double caSemaine = (double)participationsSemaine
                        .Sum(p => p.Prevente?.PrixPrevente ?? 0);

                    ventesValues.Add(nbVentes);
                    caValues.Add(caSemaine);
                    labels.Add($"{debutSemaine:dd/MM}");

                    Debug.WriteLine($"  Semaine {debutSemaine:dd/MM} → {finSemaine:dd/MM}: {nbVentes} ventes, {caSemaine:N0}€");
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

                Debug.WriteLine($"✓ Graphique initialisé avec données réelles");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ERREUR InitialiserGraphique: {ex.Message}");
                Debug.WriteLine($"StackTrace: {ex.StackTrace}");

                // Fallback : graphique vide
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

    // Classe helper pour l'affichage des produits
    public class TopProduitDisplay
    {
        public string Nom { get; set; }
        public int Ventes { get; set; }
        public decimal CA { get; set; }
    }

    // Classe helper pour les alertes stock
    public class StockAlertDisplay
    {
        public string NomProduit { get; set; } = "—";
        public string Categorie { get; set; } = "—";
        public int StockPhysique { get; set; }
        public int SeuilAlerte { get; set; }
        public bool IsRupture => StockPhysique == 0;
    }
}