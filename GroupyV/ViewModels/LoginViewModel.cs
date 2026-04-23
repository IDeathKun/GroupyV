using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GroupyV.Data;
using GroupyV.Helpers;
using GroupyV.Views;
using GroupyV.Services;

namespace GroupyV.ViewModels
{
    public class LoginViewModel : INotifyPropertyChanged
    {
        private const int MaxAttempts = 5;
        private const int LockoutSeconds = 30;

        private int _failedAttempts;
        private DateTime _lockoutUntil = DateTime.MinValue;

        private string _email;
        private string _errorMessage;
        private Visibility _errorVisibility = Visibility.Collapsed;
        private bool _isLoading;

        public string Email { get => _email; set { _email = value; OnPropertyChanged(); } }
        public string ErrorMessage { get => _errorMessage; set { _errorMessage = value; OnPropertyChanged(); } }
        public Visibility ErrorVisibility { get => _errorVisibility; set { _errorVisibility = value; OnPropertyChanged(); } }
        public bool IsLoading { get => _isLoading; set { _isLoading = value; OnPropertyChanged(); } }
        public ICommand LoginCommand { get; }

        public LoginViewModel()
        {
            LoginCommand = new RelayCommand(async p => await OnLoginAsync(p), _ => !IsLoading);
        }

        private async Task OnLoginAsync(object parameter)
        {
            if (IsLoading) return;

            if (parameter is not PasswordBox passwordBox) return;
            string password = passwordBox.Password;

            if (string.IsNullOrEmpty(Email) || string.IsNullOrEmpty(password))
            {
                ErrorMessage = "Veuillez remplir tous les champs.";
                ErrorVisibility = Visibility.Visible;
                return;
            }

            // Vérification du verrouillage temporaire
            if (DateTime.Now < _lockoutUntil)
            {
                var remaining = (int)(_lockoutUntil - DateTime.Now).TotalSeconds;
                ErrorMessage = $"Trop de tentatives. Réessayez dans {remaining} seconde(s).";
                ErrorVisibility = Visibility.Visible;
                return;
            }

            IsLoading = true;
            CommandManager.InvalidateRequerySuggested();
            ErrorVisibility = Visibility.Collapsed;

            try
            {
                // Délai artificiel pour ralentir les attaques brute-force
                await Task.Delay(500);

                var user = await Task.Run(() =>
                {
                    using var db = new GroupyContext();
                    return db.Utilisateurs.FirstOrDefault(u => u.Email == Email);
                });

                if (user != null && BCrypt.Net.BCrypt.Verify(password, user.MotDePasse))
                {
                    _failedAttempts = 0;
                    UserSession.Instance.StartSession(user);

                    MainWindow main = new MainWindow();
                    main.Show();

                    foreach (Window window in Application.Current.Windows.Cast<Window>().ToList())
                    {
                        if (window != main)
                            window.Close();
                    }
                }
                else
                {
                    _failedAttempts++;

                    if (_failedAttempts >= MaxAttempts)
                    {
                        _lockoutUntil = DateTime.Now.AddSeconds(LockoutSeconds);
                        _failedAttempts = 0;
                        ErrorMessage = $"Trop de tentatives. Compte temporairement bloqué {LockoutSeconds} secondes.";
                    }
                    else
                    {
                        int remaining = MaxAttempts - _failedAttempts;
                        ErrorMessage = $"Email ou mot de passe incorrect. ({remaining} tentative(s) restante(s))";
                    }

                    ErrorVisibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "Erreur de connexion : " + ex.Message;
                ErrorVisibility = Visibility.Visible;
            }
            finally
            {
                IsLoading = false;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}