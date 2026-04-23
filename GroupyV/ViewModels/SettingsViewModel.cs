using GroupyV.Data;
using GroupyV.Helpers;
using GroupyV.Models;
using GroupyV.Services;
using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace GroupyV.ViewModels
{
    public class SettingsViewModel : INotifyPropertyChanged
    {
        // ── Profil ──
        private string _nom;
        public string Nom { get => _nom; set { _nom = value; OnPropertyChanged(); } }

        private string _prenom;
        public string Prenom { get => _prenom; set { _prenom = value; OnPropertyChanged(); } }

        private string _email;
        public string Email { get => _email; set { _email = value; OnPropertyChanged(); } }

        private string _phone;
        public string Phone { get => _phone; set { _phone = value; OnPropertyChanged(); } }

        private string _adresse;
        public string Adresse { get => _adresse; set { _adresse = value; OnPropertyChanged(); } }

        private string _initiales;
        public string Initiales { get => _initiales; set { _initiales = value; OnPropertyChanged(); } }

        private string _nomComplet;
        public string NomComplet { get => _nomComplet; set { _nomComplet = value; OnPropertyChanged(); } }

        // ── Entreprise ──
        private string _nomEntreprise;
        public string NomEntreprise { get => _nomEntreprise; set { _nomEntreprise = value; OnPropertyChanged(); } }

        private string _siret;
        public string Siret { get => _siret; set { _siret = value; OnPropertyChanged(); } }

        private string _adresseEntreprise;
        public string AdresseEntreprise { get => _adresseEntreprise; set { _adresseEntreprise = value; OnPropertyChanged(); } }

        private string _emailPro;
        public string EmailPro { get => _emailPro; set { _emailPro = value; OnPropertyChanged(); } }

        // ── Thème ──
        private bool _isDarkMode;
        public bool IsDarkMode
        {
            get => _isDarkMode;
            set
            {
                _isDarkMode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ThemeLabel));
                OnPropertyChanged(nameof(ThemeIcon));
                ThemeService.Instance.ApplyTheme(value);
                ShowStatusMessage(value ? "Mode sombre activé" : "Mode clair activé");
            }
        }

        public string ThemeLabel => IsDarkMode ? "Mode sombre" : "Mode clair";
        public string ThemeIcon => IsDarkMode ? "WeatherNight" : "WhiteBalanceSunny";

        // ── Messages ──
        private string _statusMessage;
        public string StatusMessage { get => _statusMessage; set { _statusMessage = value; OnPropertyChanged(); } }

        private bool _showStatus;
        public bool ShowStatus { get => _showStatus; set { _showStatus = value; OnPropertyChanged(); } }

        // ── Infos session ──
        private string _sessionInfo;
        public string SessionInfo { get => _sessionInfo; set { _sessionInfo = value; OnPropertyChanged(); } }

        private string _dateInscription;
        public string DateInscription { get => _dateInscription; set { _dateInscription = value; OnPropertyChanged(); } }

        // ── Commandes ──
        public ICommand SaveProfileCommand { get; }
        public ICommand SaveEntrepriseCommand { get; }
        public ICommand ToggleThemeCommand { get; }

        public SettingsViewModel()
        {
            SaveProfileCommand = new RelayCommand(_ => SaveProfile());
            SaveEntrepriseCommand = new RelayCommand(_ => SaveEntreprise());
            ToggleThemeCommand = new RelayCommand(_ => IsDarkMode = !IsDarkMode);

            _isDarkMode = ThemeService.Instance.IsDarkMode;

            LoadUserData();
        }

        private void LoadUserData()
        {
            var session = UserSession.Instance;
            var user = session.CurrentUser;
            if (user == null) return;

            Nom = user.Nom ?? "";
            Prenom = user.Prenom ?? "";
            Email = user.Email ?? "";
            Phone = user.Phone ?? "";
            Adresse = user.Adresse ?? "";
            Initiales = session.GetInitiales();
            NomComplet = session.GetNomComplet();
            SessionInfo = $"Connecté depuis le {session.LoginTime:dd/MM/yyyy} à {session.LoginTime:HH:mm}";
            DateInscription = $"Inscrit le {user.CreatedAt:dd/MM/yyyy}";

            try
            {
                using var db = new GroupyContext();
                var vendeur = db.Vendeurs.FirstOrDefault(v => v.IdUser == user.IdUser);
                if (vendeur != null)
                {
                    NomEntreprise = vendeur.NomEntreprise ?? "";
                    Siret = vendeur.Siret ?? "";
                    AdresseEntreprise = vendeur.AdresseEntreprise ?? "";
                    EmailPro = vendeur.EmailPro ?? "";
                }
            }
            catch { }
        }

        private void SaveProfile()
        {
            try
            {
                var userId = UserSession.Instance.CurrentUser?.IdUser;
                if (userId == null) return;

                using var db = new GroupyContext();
                var user = db.Utilisateurs.Find(userId);
                if (user == null) return;

                user.Nom = Nom;
                user.Prenom = Prenom;
                user.Email = Email;
                user.Phone = Phone;
                user.Adresse = Adresse;
                db.SaveChanges();

                UserSession.Instance.CurrentUser.Nom = Nom;
                UserSession.Instance.CurrentUser.Prenom = Prenom;
                UserSession.Instance.CurrentUser.Email = Email;
                UserSession.Instance.CurrentUser.Phone = Phone;
                UserSession.Instance.CurrentUser.Adresse = Adresse;

                Initiales = UserSession.Instance.GetInitiales();
                NomComplet = UserSession.Instance.GetNomComplet();

                ShowStatusMessage("Profil mis à jour avec succès !");
            }
            catch (Exception ex)
            {
                ShowStatusMessage("Erreur : " + ex.Message);
            }
        }

        private void SaveEntreprise()
        {
            try
            {
                var userId = UserSession.Instance.CurrentUser?.IdUser;
                if (userId == null) return;

                using var db = new GroupyContext();
                var vendeur = db.Vendeurs.Find(userId);
                if (vendeur == null) return;

                vendeur.NomEntreprise = NomEntreprise;
                vendeur.Siret = Siret;
                vendeur.AdresseEntreprise = AdresseEntreprise;
                vendeur.EmailPro = EmailPro;
                db.SaveChanges();

                ShowStatusMessage("Informations entreprise mises à jour !");
            }
            catch (Exception ex)
            {
                ShowStatusMessage("Erreur : " + ex.Message);
            }
        }

        private void ShowStatusMessage(string message)
        {
            StatusMessage = message;
            ShowStatus = true;

            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            timer.Tick += (s, e) =>
            {
                ShowStatus = false;
                timer.Stop();
            };
            timer.Start();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
