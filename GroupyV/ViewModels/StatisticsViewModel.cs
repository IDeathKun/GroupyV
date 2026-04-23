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
    public class StatisticsViewModel : INotifyPropertyChanged
    {
        // ── KPI ──
        private string _caTotal;
        public string CATotal { get => _caTotal; set { _caTotal = value; OnPropertyChanged(); } }

        private string _caMoisEnCours;
        public string CAMoisEnCours { get => _caMoisEnCours; set { _caMoisEnCours = value; OnPropertyChanged(); } }

        private int _totalVentes;
        public int TotalVentes { get => _totalVentes; set { _totalVentes = value; OnPropertyChanged(); } }

        private decimal _panierMoyen;
        public decimal PanierMoyen { get => _panierMoyen; set { _panierMoyen = value; OnPropertyChanged(); } }

        private double _tauxConversion;
        public double TauxConversion { get => _tauxConversion; set { _tauxConversion = value; OnPropertyChanged(); } }

        private int _totalClients;
        public int TotalClients { get => _totalClients; set { _totalClients = value; OnPropertyChanged(); } }

        // ── Graphique CA mensuel ──
        private SeriesCollection _caMensuelSeries;
        public SeriesCollection CAMensuelSeries { get => _caMensuelSeries; set { _caMensuelSeries = value; OnPropertyChanged(); } }

        private string[] _caMensuelLabels;
        public string[] CAMensuelLabels { get => _caMensuelLabels; set { _caMensuelLabels = value; OnPropertyChanged(); } }

        private Func<double, string> _caFormatter;
        public Func<double, string> CAFormatter { get => _caFormatter; set { _caFormatter = value; OnPropertyChanged(); } }

        // ── Graphique ventes par statut (Pie) ──
        private SeriesCollection _statutSeries;
        public SeriesCollection StatutSeries { get => _statutSeries; set { _statutSeries = value; OnPropertyChanged(); } }

        // ── Top produits ──
        private ObservableCollection<ProduitStatDisplay> _topProduits;
        public ObservableCollection<ProduitStatDisplay> TopProduits { get => _topProduits; set { _topProduits = value; OnPropertyChanged(); } }

        // ── Mouvements stock récents ──
        private ObservableCollection<MouvementStatDisplay> _mouvementsRecents;
        public ObservableCollection<MouvementStatDisplay> MouvementsRecents { get => _mouvementsRecents; set { _mouvementsRecents = value; OnPropertyChanged(); } }

        // ── Graphique mouvements stock ──
        private SeriesCollection _mouvementsSeries;
        public SeriesCollection MouvementsSeries { get => _mouvementsSeries; set { _mouvementsSeries = value; OnPropertyChanged(); } }

        private string[] _mouvementsLabels;
        public string[] MouvementsLabels { get => _mouvementsLabels; set { _mouvementsLabels = value; OnPropertyChanged(); } }

        // ── Commandes ──
        public ICommand RefreshCommand { get; }

        public StatisticsViewModel()
        {
            CAMensuelSeries = new SeriesCollection();
            StatutSeries = new SeriesCollection();
            MouvementsSeries = new SeriesCollection();
            TopProduits = new ObservableCollection<ProduitStatDisplay>();
            MouvementsRecents = new ObservableCollection<MouvementStatDisplay>();
            CAFormatter = value => $"{value:N0} €";
            RefreshCommand = new RelayCommand(_ => LoadData());
            LoadData();
        }

        private void LoadData()
        {
            try
            {
                var vendeurId = UserSession.Instance.CurrentUser?.IdUser;
                if (vendeurId == null) return;

                using var db = new GroupyContext();

                var preventes = db.Preventes
                    .Include(p => p.Produit)
                        .ThenInclude(pr => pr.Categorie)
                    .Include(p => p.Participations)
                    .AsNoTracking()
                    .ToList()
                    .Where(p => p.Produit != null && p.Produit.IdVendeur == vendeurId)
                    .ToList();

                var participations = preventes
                    .SelectMany(p => p.Participations ?? Enumerable.Empty<Participation>())
                    .ToList();

                CalculerKPIs(preventes, participations);
                ChargerCAMensuel(participations, preventes);
                ChargerStatutsPie(preventes);
                ChargerTopProduits(preventes);
                ChargerMouvementsStock(db, vendeurId.Value);
            }
            catch { }
        }

        private void CalculerKPIs(List<Prevente> preventes, List<Participation> participations)
        {
            var terminees = preventes
                .Where(p => p.Statut != null && p.Statut.Equals("terminée", StringComparison.OrdinalIgnoreCase))
                .ToList();

            decimal caTotal = terminees.Sum(p => (p.Participations?.Count ?? 0) * p.PrixPrevente);
            CATotal = $"{caTotal:N2} €";

            var moisEnCours = DateTime.Now.Month;
            var anneeEnCours = DateTime.Now.Year;
            var participationsMois = participations
                .Where(p => p.DateParticipation.HasValue
                         && p.DateParticipation.Value.Month == moisEnCours
                         && p.DateParticipation.Value.Year == anneeEnCours)
                .ToList();

            decimal caMois = participationsMois
                .Sum(p =>
                {
                    var prevente = preventes.FirstOrDefault(pr => pr.IdPrevente == p.IdPrevente);
                    return prevente?.PrixPrevente ?? 0;
                });
            CAMoisEnCours = $"{caMois:N2} €";

            TotalVentes = participations.Count;
            PanierMoyen = participations.Count > 0
                ? participations.Sum(p =>
                {
                    var pr = preventes.FirstOrDefault(x => x.IdPrevente == p.IdPrevente);
                    return pr?.PrixPrevente ?? 0;
                }) / participations.Count
                : 0;

            TauxConversion = preventes.Count > 0
                ? Math.Round((double)terminees.Count / preventes.Count * 100, 1)
                : 0;

            var clientIds = participations
                .Select(p => p.IdClient)
                .Distinct();
            TotalClients = clientIds.Count();
        }

        private void ChargerCAMensuel(List<Participation> participations, List<Prevente> preventes)
        {
            var caValues = new ChartValues<double>();
            var labels = new List<string>();
            var now = DateTime.Now;

            for (int i = 5; i >= 0; i--)
            {
                var mois = now.AddMonths(-i);
                var partMois = participations
                    .Where(p => p.DateParticipation.HasValue
                             && p.DateParticipation.Value.Month == mois.Month
                             && p.DateParticipation.Value.Year == mois.Year)
                    .ToList();

                double ca = (double)partMois.Sum(p =>
                {
                    var pr = preventes.FirstOrDefault(x => x.IdPrevente == p.IdPrevente);
                    return pr?.PrixPrevente ?? 0;
                });

                caValues.Add(ca);
                labels.Add(mois.ToString("MMM yy"));
            }

            CAMensuelSeries.Clear();
            CAMensuelSeries.Add(new ColumnSeries
            {
                Title = "CA",
                Values = caValues,
                Fill = new SolidColorBrush(Color.FromRgb(108, 93, 211)),
                MaxColumnWidth = 35,
                ColumnPadding = 8
            });
            CAMensuelLabels = labels.ToArray();
        }

        private void ChargerStatutsPie(List<Prevente> preventes)
        {
            var enCours = preventes.Count(p => p.Statut != null && p.Statut.Equals("en cours", StringComparison.OrdinalIgnoreCase));
            var terminees = preventes.Count(p => p.Statut != null && p.Statut.Equals("terminée", StringComparison.OrdinalIgnoreCase));
            var annulees = preventes.Count(p => p.Statut != null && p.Statut.Equals("annulée", StringComparison.OrdinalIgnoreCase));

            StatutSeries.Clear();
            if (enCours > 0)
                StatutSeries.Add(new PieSeries { Title = "En cours", Values = new ChartValues<int> { enCours }, Fill = new SolidColorBrush(Color.FromRgb(255, 159, 67)), DataLabels = true, LabelPoint = p => $"{p.Y}" });
            if (terminees > 0)
                StatutSeries.Add(new PieSeries { Title = "Terminées", Values = new ChartValues<int> { terminees }, Fill = new SolidColorBrush(Color.FromRgb(69, 227, 184)), DataLabels = true, LabelPoint = p => $"{p.Y}" });
            if (annulees > 0)
                StatutSeries.Add(new PieSeries { Title = "Annulées", Values = new ChartValues<int> { annulees }, Fill = new SolidColorBrush(Color.FromRgb(255, 91, 91)), DataLabels = true, LabelPoint = p => $"{p.Y}" });
        }

        private void ChargerTopProduits(List<Prevente> preventes)
        {
            var top = preventes
                .Where(p => p.Produit != null)
                .GroupBy(p => p.IdProduit)
                .Select(g =>
                {
                    var first = g.First();
                    int ventes = g.Sum(p => p.Participations?.Count ?? 0);
                    decimal ca = g.Sum(p => (p.Participations?.Count ?? 0) * p.PrixPrevente);
                    int nbPreventes = g.Count();
                    return new ProduitStatDisplay
                    {
                        NomProduit = first.Produit?.NomProduit ?? "—",
                        Categorie = first.Produit?.Categorie?.Lib ?? "—",
                        NbVentes = ventes,
                        CA = ca,
                        NbPreventes = nbPreventes,
                        PrixMoyen = ventes > 0 ? ca / ventes : 0
                    };
                })
                .OrderByDescending(x => x.CA)
                .Take(5)
                .ToList();

            TopProduits.Clear();
            int rank = 1;
            foreach (var item in top)
            {
                item.Rang = rank++;
                TopProduits.Add(item);
            }
        }

        private void ChargerMouvementsStock(GroupyContext db, int vendeurId)
        {
            // Mouvements récents
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
                    Produit = m.Produit?.NomProduit ?? "—",
                    Type = m.TypeMouvement,
                    Quantite = m.Quantite,
                    Motif = m.Motif,
                    Date = m.DateMouvement
                });
            }

            // Graphique entrées/sorties par semaine (4 semaines)
            var entreesValues = new ChartValues<double>();
            var sortiesValues = new ChartValues<double>();
            var mvtLabels = new List<string>();
            var allMouvements = db.MouvementsStock
                .AsNoTracking()
                .ToList()
                .Where(m => m.IdVendeur == vendeurId && m.DateMouvement >= depuis)
                .ToList();

            var aujourdHui = DateTime.Now.Date;
            for (int i = 3; i >= 0; i--)
            {
                var debut = aujourdHui.AddDays(-(i + 1) * 7 + 1);
                var fin = aujourdHui.AddDays(-i * 7);
                if (i == 0) fin = aujourdHui;

                var semaine = allMouvements
                    .Where(m => m.DateMouvement.Date >= debut && m.DateMouvement.Date <= fin)
                    .ToList();

                entreesValues.Add(semaine.Where(m => m.TypeMouvement == "entree").Sum(m => m.Quantite));
                sortiesValues.Add(semaine.Where(m => m.TypeMouvement == "sortie").Sum(m => m.Quantite));
                mvtLabels.Add($"{debut:dd/MM}");
            }

            MouvementsSeries.Clear();
            MouvementsSeries.Add(new ColumnSeries
            {
                Title = "Entrées",
                Values = entreesValues,
                Fill = new SolidColorBrush(Color.FromRgb(69, 227, 184)),
                MaxColumnWidth = 20
            });
            MouvementsSeries.Add(new ColumnSeries
            {
                Title = "Sorties",
                Values = sortiesValues,
                Fill = new SolidColorBrush(Color.FromRgb(255, 159, 67)),
                MaxColumnWidth = 20
            });
            MouvementsLabels = mvtLabels.ToArray();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    // ── Classes d'affichage ──

    public class ProduitStatDisplay
    {
        public int Rang { get; set; }
        public string NomProduit { get; set; } = "—";
        public string Categorie { get; set; } = "—";
        public int NbVentes { get; set; }
        public decimal CA { get; set; }
        public int NbPreventes { get; set; }
        public decimal PrixMoyen { get; set; }
        public string RangIcon => Rang switch { 1 => "🥇", 2 => "🥈", 3 => "🥉", _ => $"#{Rang}" };
    }

    public class MouvementStatDisplay
    {
        public string Produit { get; set; } = "—";
        public string Type { get; set; } = "entree";
        public int Quantite { get; set; }
        public string Motif { get; set; } = "—";
        public DateTime Date { get; set; }
        public bool IsEntree => Type == "entree";
    }
}
