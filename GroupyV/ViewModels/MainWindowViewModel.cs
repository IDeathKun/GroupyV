using GroupyV.Helpers;
using GroupyV.Models;
using GroupyV.Services;
using GroupyV.Views;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace GroupyV.ViewModels
{
    public class MainWindowViewModel : BaseViewModel
    {
        private string _userName;
        public string UserName
        {
            get => _userName;
            set { _userName = value; OnPropertyChanged(); }
        }

        private string _userRole;
        public string UserRole
        {
            get => _userRole;
            set { _userRole = value; OnPropertyChanged(); }
        }

        private string _userInitials;
        public string UserInitials
        {
            get => _userInitials;
            set { _userInitials = value; OnPropertyChanged(); }
        }

        private object _currentView;
        public object CurrentView
        {
            get => _currentView;
            set { _currentView = value; OnPropertyChanged(); }
        }

        public ICommand NavigateCommand { get; }
        public ICommand LogoutCommand { get; }

        public MainWindowViewModel()
        {
            var session = UserSession.Instance;
            UserName = session.GetNomComplet();
            UserInitials = session.GetInitiales();
            UserRole = session.Role;

            NavigateCommand = new RelayCommand(param => ExecuteNavigate(param?.ToString()));
            LogoutCommand = new RelayCommand(_ => Logout());
            ShowDashboard();
        }

        private void ExecuteNavigate(string destination)
        {
            if (string.IsNullOrEmpty(destination)) return;

            switch (destination)
            {
                case "Dashboard": ShowDashboard(); break;
                case "Orders":    ShowOrders();    break;
                case "Stock":     ShowStock();     break;
                case "Stats":     ShowStats();     break;
                case "Messages":  ShowMessages();  break;
                case "Settings":  ShowSettings();  break;
            }
        }

        private void ShowDashboard() => CurrentView = new DashboardViewModel();

        private void ShowOrders()
        {
            var ordersVM = new OrdersViewModel();
            ordersVM.RequestOpenDetail = (id) => OpenOrderDetail(id);
            CurrentView = ordersVM;
        }

        private void ShowStock() => CurrentView = new StockViewModel();

        private void ShowStats() => CurrentView = new StatisticsViewModel();

        private void ShowMessages() => CurrentView = new MessagesViewModel();

        private void ShowSettings() => CurrentView = new SettingsViewModel();

        private void OpenOrderDetail(int orderId)
        {
            var detailVM = new OrderDetailViewModel(orderId);
            detailVM.RequestGoBack = () => ShowOrders();
            CurrentView = detailVM;
        }

        private void Logout()
        {
            if (!ShowStyledLogoutConfirmation())
                return;

            UserSession.Instance.EndSession();

            var login = new LoginWindow();
            login.Show();

            foreach (Window window in Application.Current.Windows.Cast<Window>().ToList())
            {
                if (window != login)
                {
                    window.Close();
                }
            }
        }

        private static bool ShowStyledLogoutConfirmation()
        {
            bool confirmed = false;

            var popup = new Window
            {
                Width = 460,
                Height = 270,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Application.Current?.MainWindow,
                Opacity = 0
            };

            var root = new Grid();

            var card = new Border
            {
                CornerRadius = new CornerRadius(18),
                Background = new SolidColorBrush(Color.FromRgb(24, 26, 42)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(62, 66, 92)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(24),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 30,
                    ShadowDepth = 8,
                    Opacity = 0.35,
                    Color = Colors.Black
                },
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new ScaleTransform(0.95, 0.95)
            };

            var content = new StackPanel();

            var iconBadge = new Border
            {
                Width = 60,
                Height = 60,
                CornerRadius = new CornerRadius(30),
                Background = new SolidColorBrush(Color.FromArgb(45, 255, 91, 91)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Child = new TextBlock
                {
                    Text = "!",
                    FontSize = 32,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(255, 120, 120)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextAlignment = TextAlignment.Center
                }
            };

            var title = new TextBlock
            {
                Text = "Confirmer la déconnexion",
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 14, 0, 8),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };

            var message = new TextBlock
            {
                Text = "Vous allez quitter votre session actuelle et revenir sur l'écran de connexion.",
                Foreground = new SolidColorBrush(Color.FromRgb(176, 183, 204)),
                FontSize = 13,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 22)
            };

            var cancelButton = new Button
            {
                Content = "Annuler",
                MinWidth = 130,
                Height = 40,
                Margin = new Thickness(0, 0, 10, 0),
                Background = new SolidColorBrush(Color.FromRgb(52, 55, 76)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontWeight = FontWeights.SemiBold
            };

            var confirmButton = new Button
            {
                Content = "Se déconnecter",
                MinWidth = 150,
                Height = 40,
                Background = new SolidColorBrush(Color.FromRgb(255, 91, 91)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontWeight = FontWeights.Bold
            };

            cancelButton.MouseEnter += (_, _) => cancelButton.Background = new SolidColorBrush(Color.FromRgb(76, 81, 111));
            cancelButton.MouseLeave += (_, _) => cancelButton.Background = new SolidColorBrush(Color.FromRgb(52, 55, 76));
            confirmButton.MouseEnter += (_, _) => confirmButton.Background = new SolidColorBrush(Color.FromRgb(255, 120, 120));
            confirmButton.MouseLeave += (_, _) => confirmButton.Background = new SolidColorBrush(Color.FromRgb(255, 91, 91));

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            buttons.Children.Add(cancelButton);
            buttons.Children.Add(confirmButton);

            content.Children.Add(iconBadge);
            content.Children.Add(title);
            content.Children.Add(message);
            content.Children.Add(buttons);

            card.Child = content;
            root.Children.Add(card);
            popup.Content = root;

            cancelButton.Click += (_, _) => popup.Close();
            confirmButton.Click += (_, _) => { confirmed = true; popup.Close(); };

            popup.Loaded += (_, _) =>
            {
                var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
                popup.BeginAnimation(Window.OpacityProperty, fade);

                var scaleX = new DoubleAnimation(0.95, 1, TimeSpan.FromMilliseconds(220));
                var scaleY = new DoubleAnimation(0.95, 1, TimeSpan.FromMilliseconds(220));
                ((ScaleTransform)card.RenderTransform).BeginAnimation(ScaleTransform.ScaleXProperty, scaleX);
                ((ScaleTransform)card.RenderTransform).BeginAnimation(ScaleTransform.ScaleYProperty, scaleY);
            };

            popup.ShowDialog();
            return confirmed;
        }
    }
}