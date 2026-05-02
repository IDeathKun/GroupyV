using GroupyV.Data;
using GroupyV.Helpers;
using GroupyV.Models;
using GroupyV.Services;
using LiveCharts;
using LiveCharts.Wpf;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media;

namespace GroupyV.ViewModels
{
    // ── Classes d'affichage ─────────────────────────────────────────────────────

    public class PreventeEnCoursDisplay
    {
        public string NomProduit    { get; set; } = "—";
        public decimal Prix         { get; set; }
        public int NbParticipations { get; set; }
        public string Statut        { get; set; } = "en cours";
        public string Info          => $"{NomProduit}  —  {NbParticipations} part. à {Prix:N2} €";
    }

    public class ProduitStatDisplay
    {
        public int     Rang        { get; set; }
        public string  NomProduit  { get; set; } = "—";
        public string  Categorie   { get; set; } = "—";
        public int     NbVentes    { get; set; }
        public decimal CA          { get; set; }
        public int     NbPreventes { get; set; }
        public decimal PrixMoyen   { get; set; }
        public string  RangIcon    => Rang switch { 1 => "🥇", 2 => "🥈", 3 => "🥉", _ => $"#{Rang}" };
    }

    public class MouvementStatDisplay
    {
        public string   Produit   { get; set; } = "—";
        public string   Type      { get; set; } = "entree";
        public int      Quantite  { get; set; }
        public string   Motif     { get; set; } = "—";
        public DateTime Date      { get; set; }
        public bool     IsEntree  => Type == "entree";
    }

    // ── ViewModel ───────────────────────────────────────────────────────────────

    public class StatisticsViewModel : INotifyPropertyChanged
    {
        // ── KPIs ────────────────────────────────────────────────────────────────

        private string _caTotal        = "0,00 €";
        public  string CATotal         { get => _caTotal;        set { _caTotal        = value; OnPropertyChanged(); } }

        private string _caMoisEnCours  = "0,00 €";
        public  string CAMoisEnCours   { get => _caMoisEnCours;  set { _caMoisEnCours  = value; OnPropertyChanged(); } }

        private int    _totalVentes;
        public  int    TotalVentes     { get => _totalVentes;    set { _totalVentes    = value; OnPropertyChanged(); } }

        private decimal _panierMoyen;
        public  decimal PanierMoyen    { get => _panierMoyen;    set { _panierMoyen    = value; OnPropertyChanged(); } }

        private double _tauxConversion;
        public  double TauxConversion  { get => _tauxConversion; set { _tauxConversion = value; OnPropertyChanged(); } }

        private int _totalClients;
        public  int TotalClients       { get => _totalClients;   set { _totalClients   = value; OnPropertyChanged(); } }

        private int _totalProduits;
        public  int TotalProduits      { get => _totalProduits;  set { _totalProduits  = value; OnPropertyChanged(); } }

        private int _nbPreventesEnCours;
        public  int NbPreventesEnCours { get => _nbPreventesEnCours; set { _nbPreventesEnCours = value; OnPropertyChanged(); } }

        // ── Graphique CA ────────────────────────────────────────────────────────

        // Mode : "SixMois" | "ParAnnee" | "MultiAnnees"
        private string _caViewMode = "SixMois";
        public  string CAViewMode
        {
            get => _caViewMode;
            set
            {
                _caViewMode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsModeSixMois));
                OnPropertyChanged(nameof(IsModeParAnnee));
                OnPropertyChanged(nameof(IsModeMultiAnnees));
                OnPropertyChanged(nameof(ShowYearNav));
                RefreshCAChart();
            }
        }

        public bool IsModeSixMois    => _caViewMode == "SixMois";
        public bool IsModeParAnnee   => _caViewMode == "ParAnnee";
        public bool IsModeMultiAnnees => _caViewMode == "MultiAnnees";
        public bool ShowYearNav      => _caViewMode == "ParAnnee";

        private int _selectedYear = DateTime.Now.Year;
        public  int SelectedYear
        {
            get => _selectedYear;
            set { _selectedYear = value; OnPropertyChanged(); if (_caViewMode == "ParAnnee") RefreshCAChart(); }
        }

        private ObservableCollection<int> _availableYears = new();
        public  ObservableCollection<int> AvailableYears
        {
            get => _availableYears;
            set { _availableYears = value; OnPropertyChanged(); }
        }

        private string _caChartTitle   = "Chiffre d'affaires mensuel";
        public  string CAChartTitle    { get => _caChartTitle;   set { _caChartTitle   = value; OnPropertyChanged(); } }

        private string _caChartSummary = "6 derniers mois";
        public  string CAChartSummary  { get => _caChartSummary; set { _caChartSummary = value; OnPropertyChanged(); } }

        private SeriesCollection _caMensuelSeries = new();
        public  SeriesCollection CAMensuelSeries   { get => _caMensuelSeries;   set { _caMensuelSeries   = value; OnPropertyChanged(); } }

        private string[] _caMensuelLabels = Array.Empty<string>();
        public  string[] CAMensuelLabels   { get => _caMensuelLabels; set { _caMensuelLabels = value; OnPropertyChanged(); } }

        private Func<double, string> _caFormatter;
        public  Func<double, string> CAFormatter { get => _caFormatter; set { _caFormatter = value; OnPropertyChanged(); } }

        // ── Pie statuts ─────────────────────────────────────────────────────────

        private SeriesCollection _statutSeries = new();
        public  SeriesCollection StatutSeries   { get => _statutSeries; set { _statutSeries = value; OnPropertyChanged(); } }

        // ── Préventes en cours (tooltip) ────────────────────────────────────────

        private ObservableCollection<PreventeEnCoursDisplay> _preventesEnCours = new();
        public  ObservableCollection<PreventeEnCoursDisplay> PreventesEnCours
        {
            get => _preventesEnCours;
            set { _preventesEnCours = value; OnPropertyChanged(); }
        }

        // ── Top produits ────────────────────────────────────────────────────────

        private ObservableCollection<ProduitStatDisplay> _topProduits = new();
        public  ObservableCollection<ProduitStatDisplay> TopProduits
        {
            get => _topProduits;
            set { _topProduits = value; OnPropertyChanged(); }
        }

        // ── Mouvements stock ────────────────────────────────────────────────────

        private ObservableCollection<MouvementStatDisplay> _mouvementsRecents = new();
        public  ObservableCollection<MouvementStatDisplay> MouvementsRecents
        {
            get => _mouvementsRecents;
            set { _mouvementsRecents = value; OnPropertyChanged(); }
        }

        private SeriesCollection _mouvementsSeries = new();
        public  SeriesCollection MouvementsSeries
        {
            get => _mouvementsSeries;
            set { _mouvementsSeries = value; OnPropertyChanged(); }
        }

        private string[] _mouvementsLabels = Array.Empty<string>();
        public  string[] MouvementsLabels
        {
            get => _mouvementsLabels;
            set { _mouvementsLabels = value; OnPropertyChanged(); }
        }

        // ── Cache données ────────────────────────────────────────────────────────

        private List<Prevente>     _allPreventes     = new();
        private List<Participation> _allParticipations = new();

        // ── Commandes ────────────────────────────────────────────────────────────

        public ICommand RefreshCommand      { get; }
        public ICommand SetViewModeCommand  { get; }
        public ICommand PreviousYearCommand { get; }
        public ICommand NextYearCommand     { get; }

        // ── Constructeur ─────────────────────────────────────────────────────────

        public StatisticsViewModel()
        {
            CAFormatter         = v => $"{v:N0} €";
            RefreshCommand      = new RelayCommand(_ => LoadData());
            SetViewModeCommand  = new RelayCommand(p => CAViewMode = p?.ToString() ?? "SixMois");
            PreviousYearCommand = new RelayCommand(_ =>
            {
                int minYear = AvailableYears.Count > 0 ? AvailableYears.Min() : 2020;
                if (SelectedYear > minYear) SelectedYear--;
            });
            NextYearCommand = new RelayCommand(_ =>
            {
                if (SelectedYear < DateTime.Now.Year) SelectedYear++;
            });
            LoadData();
        }

        // ── Chargement principal ─────────────────────────────────────────────────

        private void LoadData()
        {
            try
            {
                var vendeurId = UserSession.Instance.CurrentUser?.IdUser;
                if (vendeurId == null) return;

                using var db = new GroupyContext();

                _allPreventes = db.Preventes
                    .Include(p => p.Produit).ThenInclude(pr => pr.Categorie)
                    .Include(p => p.Participations)
                    .AsNoTracking()
                    .ToList()
                    .Where(p => p.Produit != null && p.Produit.IdVendeur == vendeurId)
                    .ToList();

                _allParticipations = _allPreventes
                    .SelectMany(p => p.Participations ?? Enumerable.Empty<Participation>())
                    .ToList();

                // Années avec données + année courante
                var years = _allParticipations
                    .Where(p => p.DateParticipation.HasValue)
                    .Select(p => p.DateParticipation!.Value.Year)
                    .Distinct()
                    .OrderByDescending(y => y)
                    .ToList();
                if (!years.Contains(DateTime.Now.Year))
                    years.Insert(0, DateTime.Now.Year);

                AvailableYears.Clear();
                foreach (var y in years) AvailableYears.Add(y);

                // Nombre de produits actifs
                TotalProduits = db.Produits.Count(p => p.IdVendeur == vendeurId);

                CalculerKPIs(_allPreventes, _allParticipations);
                ChargerPreventesEnCours(_allPreventes);
                RefreshCAChart();
                ChargerStatutsPie(_allPreventes);
                ChargerTopProduits(_allPreventes);
                ChargerMouvementsStock(db, vendeurId.Value);
            }
            catch { }
        }

        // ── KPIs ─────────────────────────────────────────────────────────────────

        private void CalculerKPIs(List<Prevente> preventes, List<Participation> participations)
        {
            var terminees = preventes
                .Where(p => p.Statut?.Equals("terminée", StringComparison.OrdinalIgnoreCase) == true)
                .ToList();

            decimal caTotal = terminees.Sum(p => (p.Participations?.Count ?? 0) * p.PrixPrevente);
            CATotal = $"{caTotal:N2} €";

            var now = DateTime.Now;
            var partMois = participations
                .Where(p => p.DateParticipation?.Month == now.Month
                         && p.DateParticipation?.Year  == now.Year)
                .ToList();
            decimal caMois = partMois.Sum(p =>
                preventes.FirstOrDefault(pr => pr.IdPrevente == p.IdPrevente)?.PrixPrevente ?? 0);
            CAMoisEnCours = $"{caMois:N2} €";

            TotalVentes = participations.Count;
            PanierMoyen = participations.Count > 0
                ? participations.Sum(p =>
                    preventes.FirstOrDefault(x => x.IdPrevente == p.IdPrevente)?.PrixPrevente ?? 0)
                  / participations.Count
                : 0;

            TauxConversion = preventes.Count > 0
                ? Math.Round((double)terminees.Count / preventes.Count * 100, 1)
                : 0;

            TotalClients = participations.Select(p => p.IdClient).Distinct().Count();
        }

        // ── Préventes en cours ───────────────────────────────────────────────────

        private void ChargerPreventesEnCours(List<Prevente> preventes)
        {
            var enCours = preventes
                .Where(p => p.Statut?.Equals("en cours", StringComparison.OrdinalIgnoreCase) == true)
                .OrderByDescending(p => p.Participations?.Count ?? 0)
                .ToList();

            NbPreventesEnCours = enCours.Count;
            PreventesEnCours.Clear();
            foreach (var p in enCours)
            {
                PreventesEnCours.Add(new PreventeEnCoursDisplay
                {
                    NomProduit      = p.Produit?.NomProduit ?? "—",
                    Prix            = p.PrixPrevente,
                    NbParticipations = p.Participations?.Count ?? 0
                });
            }
        }

        // ── Graphique CA – dispatch ──────────────────────────────────────────────

        private void RefreshCAChart()
        {
            switch (_caViewMode)
            {
                case "SixMois":    ChargerCA_SixMois();          break;
                case "ParAnnee":   ChargerCA_ParAnnee(_selectedYear); break;
                case "MultiAnnees": ChargerCA_MultiAnnees();      break;
            }
        }

        // Mode 1 – 6 derniers mois glissants
        private void ChargerCA_SixMois()
        {
            var caValues    = new ChartValues<double>();
            var ventValues  = new ChartValues<double>();
            var labels      = new List<string>();
            var now         = DateTime.Now;

            for (int i = 5; i >= 0; i--)
            {
                var mois  = now.AddMonths(-i);
                var parts = _allParticipations
                    .Where(p => p.DateParticipation?.Month == mois.Month
                             && p.DateParticipation?.Year  == mois.Year)
                    .ToList();

                double ca = (double)parts.Sum(p =>
                    _allPreventes.FirstOrDefault(x => x.IdPrevente == p.IdPrevente)?.PrixPrevente ?? 0);

                caValues.Add(ca);
                ventValues.Add(parts.Count);
                labels.Add(mois.ToString("MMM yy"));
            }

            double total = caValues.Sum();
            CAChartTitle   = "Chiffre d'affaires mensuel";
            CAChartSummary = $"6 derniers mois  •  Cumulé : {total:N0} €";
            BuildCAChart(caValues, ventValues, labels);
        }

        // Mode 2 – Mois par mois pour une année choisie
        private void ChargerCA_ParAnnee(int year)
        {
            var caValues   = new ChartValues<double>();
            var ventValues = new ChartValues<double>();
            var labels     = new List<string>
                { "Jan","Fév","Mar","Avr","Mai","Jun","Jul","Aoû","Sep","Oct","Nov","Déc" };

            for (int m = 1; m <= 12; m++)
            {
                var parts = _allParticipations
                    .Where(p => p.DateParticipation?.Year  == year
                             && p.DateParticipation?.Month == m)
                    .ToList();
                double ca = (double)parts.Sum(p =>
                    _allPreventes.FirstOrDefault(x => x.IdPrevente == p.IdPrevente)?.PrixPrevente ?? 0);

                caValues.Add(ca);
                ventValues.Add(parts.Count);
            }

            double total = caValues.Sum();
            int meilleurMois = caValues.IndexOf(caValues.Max()) + 1;
            string meilleur  = meilleurMois >= 1 && meilleurMois <= 12
                ? new DateTime(year, meilleurMois, 1).ToString("MMMM") : "—";

            CAChartTitle   = $"Chiffre d'affaires {year}";
            CAChartSummary = $"Total annuel : {total:N0} €  •  Meilleur mois : {meilleur}";
            BuildCAChart(caValues, ventValues, labels);
        }

        // Mode 3 – Comparaison toutes les années
        private void ChargerCA_MultiAnnees()
        {
            var years = _allParticipations
                .Where(p => p.DateParticipation.HasValue)
                .Select(p => p.DateParticipation!.Value.Year)
                .Distinct()
                .OrderBy(y => y)
                .ToList();

            if (years.Count == 0) years.Add(DateTime.Now.Year);
            if (!years.Contains(DateTime.Now.Year)) years.Add(DateTime.Now.Year);

            var caValues   = new ChartValues<double>();
            var ventValues = new ChartValues<double>();
            var labels     = new List<string>();

            foreach (var year in years)
            {
                var parts = _allParticipations
                    .Where(p => p.DateParticipation?.Year == year)
                    .ToList();
                double ca = (double)parts.Sum(p =>
                    _allPreventes.FirstOrDefault(x => x.IdPrevente == p.IdPrevente)?.PrixPrevente ?? 0);
                caValues.Add(ca);
                ventValues.Add(parts.Count);
                labels.Add(year.ToString());
            }

            double total = caValues.Sum();
            CAChartTitle   = "Évolution annuelle du CA";
            CAChartSummary = $"{years.Count} année(s)  •  Total cumulé : {total:N0} €";
            BuildCAChart(caValues, ventValues, labels);
        }

        // Construit les séries LiveCharts
        private void BuildCAChart(ChartValues<double> caValues, ChartValues<double> ventValues, List<string> labels)
        {
            CAMensuelSeries.Clear();

            CAMensuelSeries.Add(new ColumnSeries
            {
                Title           = "CA (€)",
                Values          = caValues,
                Fill            = new SolidColorBrush(Color.FromRgb(108, 93, 211)),
                MaxColumnWidth  = 42,
                ColumnPadding   = 6,
                DataLabels      = false,
                LabelPoint      = p => p.Y > 0 ? $"{p.Y:N0} €" : ""
            });

            CAMensuelSeries.Add(new LineSeries
            {
                Title            = "Participations",
                Values           = ventValues,
                Stroke           = new SolidColorBrush(Color.FromRgb(69, 227, 184)),
                Fill             = new SolidColorBrush(Color.FromArgb(18, 69, 227, 184)),
                PointGeometrySize = 7,
                StrokeThickness  = 2,
                LineSmoothness   = 0.5,
                ScalesYAt        = 1,
                DataLabels       = false,
                LabelPoint       = p => p.Y > 0 ? $"{(int)p.Y}" : ""
            });

            CAMensuelLabels = labels.ToArray();
        }

        // ── Pie statuts ──────────────────────────────────────────────────────────

        private void ChargerStatutsPie(List<Prevente> preventes)
        {
            var enCours  = preventes.Count(p => p.Statut?.Equals("en cours",  StringComparison.OrdinalIgnoreCase) == true);
            var terminees = preventes.Count(p => p.Statut?.Equals("terminée", StringComparison.OrdinalIgnoreCase) == true);
            var annulees  = preventes.Count(p => p.Statut?.Equals("annulée",  StringComparison.OrdinalIgnoreCase) == true);

            StatutSeries.Clear();
            if (enCours  > 0) StatutSeries.Add(new PieSeries { Title = "En cours",  Values = new ChartValues<int> { enCours  }, Fill = new SolidColorBrush(Color.FromRgb(255, 159, 67)),  DataLabels = true, LabelPoint = p => $"{p.Y}" });
            if (terminees > 0) StatutSeries.Add(new PieSeries { Title = "Terminées", Values = new ChartValues<int> { terminees }, Fill = new SolidColorBrush(Color.FromRgb(69,  227, 184)), DataLabels = true, LabelPoint = p => $"{p.Y}" });
            if (annulees  > 0) StatutSeries.Add(new PieSeries { Title = "Annulées",  Values = new ChartValues<int> { annulees  }, Fill = new SolidColorBrush(Color.FromRgb(255,  91,  91)), DataLabels = true, LabelPoint = p => $"{p.Y}" });
        }

        // ── Top produits ─────────────────────────────────────────────────────────

        private void ChargerTopProduits(List<Prevente> preventes)
        {
            var top = preventes
                .Where(p => p.Produit != null)
                .GroupBy(p => p.IdProduit)
                .Select(g =>
                {
                    var first    = g.First();
                    int ventes   = g.Sum(p => p.Participations?.Count ?? 0);
                    decimal ca   = g.Sum(p => (p.Participations?.Count ?? 0) * p.PrixPrevente);
                    int nbPrev   = g.Count();
                    return new ProduitStatDisplay
                    {
                        NomProduit  = first.Produit?.NomProduit ?? "—",
                        Categorie   = first.Produit?.Categorie?.Lib ?? "—",
                        NbVentes    = ventes,
                        CA          = ca,
                        NbPreventes = nbPrev,
                        PrixMoyen   = ventes > 0 ? ca / ventes : 0
                    };
                })
                .OrderByDescending(x => x.CA)
                .Take(5)
                .ToList();

            TopProduits.Clear();
            int rank = 1;
            foreach (var item in top) { item.Rang = rank++; TopProduits.Add(item); }
        }

        // ── Mouvements stock ─────────────────────────────────────────────────────

        private void ChargerMouvementsStock(GroupyContext db, int vendeurId)
        {
            var depuis = DateTime.Now.AddDays(-30);

            var mouvements = db.MouvementsStock
                .Include(m => m.Produit)
                .AsNoTracking()
                .ToList()
                .Where(m => m.IdVendeur == vendeurId && m.DateMouvement >= depuis)
                .OrderByDescending(m => m.DateMouvement)
                .Take(10)
                .ToList();

            MouvementsRecents.Clear();
            foreach (var m in mouvements)
            {
                MouvementsRecents.Add(new MouvementStatDisplay
                {
                    Produit  = m.Produit?.NomProduit ?? "—",
                    Type     = m.TypeMouvement,
                    Quantite = m.Quantite,
                    Motif    = m.Motif,
                    Date     = m.DateMouvement
                });
            }

            // Graphique 4 semaines
            var allMvt = db.MouvementsStock.AsNoTracking().ToList()
                .Where(m => m.IdVendeur == vendeurId && m.DateMouvement >= depuis).ToList();

            var entreesValues = new ChartValues<double>();
            var sortiesValues = new ChartValues<double>();
            var mvtLabels     = new List<string>();
            var today         = DateTime.Now.Date;

            for (int i = 3; i >= 0; i--)
            {
                var debut   = today.AddDays(-(i + 1) * 7 + 1);
                var fin     = i == 0 ? today : today.AddDays(-i * 7);
                var semaine = allMvt.Where(m => m.DateMouvement.Date >= debut && m.DateMouvement.Date <= fin).ToList();
                entreesValues.Add(semaine.Where(m => m.TypeMouvement == "entree").Sum(m => m.Quantite));
                sortiesValues.Add(semaine.Where(m => m.TypeMouvement == "sortie").Sum(m => m.Quantite));
                mvtLabels.Add($"{debut:dd/MM}");
            }

            MouvementsSeries.Clear();
            MouvementsSeries.Add(new ColumnSeries { Title = "Entrées", Values = entreesValues, Fill = new SolidColorBrush(Color.FromRgb(69,  227, 184)), MaxColumnWidth = 20 });
            MouvementsSeries.Add(new ColumnSeries { Title = "Sorties", Values = sortiesValues, Fill = new SolidColorBrush(Color.FromRgb(255, 159,  67)), MaxColumnWidth = 20 });
            MouvementsLabels = mvtLabels.ToArray();
        }

        // ── INPC ─────────────────────────────────────────────────────────────────

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
