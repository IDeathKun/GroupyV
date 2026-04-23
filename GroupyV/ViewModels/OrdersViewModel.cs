using GroupyV.Data;
using GroupyV.Helpers;
using GroupyV.Models;
using GroupyV.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace GroupyV.ViewModels
{
    public class OrdersViewModel : BaseViewModel
    {
        private ObservableCollection<ClientOrderGroup> _clientGroups;
        public ObservableCollection<ClientOrderGroup> ClientGroups
        {
            get => _clientGroups;
            set { _clientGroups = value; OnPropertyChanged(); }
        }

        private int _totalClients;
        public int TotalClients
        {
            get => _totalClients;
            set { _totalClients = value; OnPropertyChanged(); }
        }

        private int _totalEnAttente;
        public int TotalEnAttente
        {
            get => _totalEnAttente;
            set { _totalEnAttente = value; OnPropertyChanged(); }
        }

        private int _totalExpediees;
        public int TotalExpediees
        {
            get => _totalExpediees;
            set { _totalExpediees = value; OnPropertyChanged(); }
        }

        public ICommand ManageOrderCommand { get; }
        public Action<int> RequestOpenDetail { get; set; }

        public OrdersViewModel()
        {
            ManageOrderCommand = new RelayCommand(ExecuteManageOrder);
            LoadClientOrders();
        }

        private void LoadClientOrders()
        {
            try
            {
                var currentVendeurId = UserSession.Instance.CurrentUser?.IdUser;
                if (currentVendeurId == null)
                {
                    ClientGroups = new ObservableCollection<ClientOrderGroup>();
                    return;
                }

                using (var db = new GroupyContext())
                {
                    // Charger TOUTES les préventes avec relations SANS filtre SQL sur statut
                    var toutesLesPreventes = db.Preventes
                        .Include(p => p.Produit)
                        .Include(p => p.Participations)
                            .ThenInclude(part => part.Client)
                                .ThenInclude(c => c.Utilisateur)
                        .Include(p => p.Participations)
                            .ThenInclude(part => part.Facture)
                        .AsNoTracking()
                        .ToList();

                    // Filtrer en mémoire : préventes du vendeur, non annulées
                    var preventesDuVendeur = toutesLesPreventes
                        .Where(p => p.Produit != null && p.Produit.IdVendeur == currentVendeurId)
                        .Where(p => p.Statut == null || !p.Statut.Equals("annulée", StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    // Charger les expéditions
                    var preventeIds = preventesDuVendeur.Select(p => p.IdPrevente).ToList();
                    var expeditions = db.Expeditions
                        .Where(e => preventeIds.Contains(e.IdPrevente))
                        .ToDictionary(e => e.IdPrevente);

                    // Regrouper par client
                    var allParticipations = preventesDuVendeur
                        .SelectMany(p => p.Participations.Select(part => new { Participation = part, Prevente = p }))
                        .ToList();

                    var groupedByClient = allParticipations
                        .Where(x => x.Participation.Client?.Utilisateur != null)
                        .GroupBy(x => x.Participation.IdClient)
                        .Select(g =>
                        {
                            var firstClient = g.First().Participation.Client;
                            var clientPreventes = g.Select(x =>
                            {
                                expeditions.TryGetValue(x.Prevente.IdPrevente, out var exp);
                                return new ClientPreventeInfo
                                {
                                    Prevente = x.Prevente,
                                    Participation = x.Participation,
                                    Expedition = exp
                                };
                            }).ToList();

                            return new ClientOrderGroup
                            {
                                Client = firstClient,
                                NomComplet = $"{firstClient.Utilisateur.Prenom} {firstClient.Utilisateur.Nom}",
                                Email = firstClient.Utilisateur.Email,
                                Phone = firstClient.Utilisateur.Phone,
                                Adresse = firstClient.Utilisateur.Adresse,
                                Initiales = GetInitiales(firstClient.Utilisateur),
                                Preventes = new ObservableCollection<ClientPreventeInfo>(clientPreventes),
                                NbCommandes = clientPreventes.Count,
                                TotalCA = clientPreventes.Sum(cp => cp.Prevente.PrixPrevente),
                                NbExpediees = clientPreventes.Count(cp => cp.Expedition != null),
                                NbEnAttente = clientPreventes.Count(cp => cp.Expedition == null)
                            };
                        })
                        .OrderByDescending(g => g.NbEnAttente)
                        .ThenByDescending(g => g.TotalCA)
                        .ToList();

                    ClientGroups = new ObservableCollection<ClientOrderGroup>(groupedByClient);
                    TotalClients = groupedByClient.Count;
                    TotalEnAttente = groupedByClient.Sum(g => g.NbEnAttente);
                    TotalExpediees = groupedByClient.Sum(g => g.NbExpediees);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erreur lors du chargement des commandes : " + ex.Message);
            }
        }

        private string GetInitiales(Utilisateur u)
        {
            if (u == null) return "?";
            string p = string.IsNullOrEmpty(u.Prenom) ? "" : u.Prenom.Substring(0, 1).ToUpper();
            string n = string.IsNullOrEmpty(u.Nom) ? "" : u.Nom.Substring(0, 1).ToUpper();
            return p + n;
        }

        private void ExecuteManageOrder(object parameter)
        {
            if (parameter is int id) RequestOpenDetail?.Invoke(id);
        }
    }

    // --- Classes d'affichage ---

    public class ClientOrderGroup
    {
        public Client Client { get; set; }
        public string NomComplet { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Adresse { get; set; }
        public string Initiales { get; set; }
        public ObservableCollection<ClientPreventeInfo> Preventes { get; set; }
        public int NbCommandes { get; set; }
        public decimal TotalCA { get; set; }
        public int NbExpediees { get; set; }
        public int NbEnAttente { get; set; }
        public string StatutGlobal => NbEnAttente == 0 ? "TOUT EXPÉDIÉ" : $"{NbEnAttente} EN ATTENTE";
        public string StatutColor => NbEnAttente == 0 ? "#00E676" : "#FF9100";
    }

    public class ClientPreventeInfo
    {
        public Prevente Prevente { get; set; }
        public Participation Participation { get; set; }
        public Expedition Expedition { get; set; }
        public bool IsShipped => Expedition != null;
        public string StatutText => IsShipped ? "Expédié" : "En attente";
        public string StatutColor => IsShipped ? "#00E676" : "#FF9100";
    }
}