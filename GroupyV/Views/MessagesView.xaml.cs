using GroupyV.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GroupyV.Views
{
    public partial class MessagesView : UserControl
    {
        private MessagesViewModel? _vm;

        public MessagesView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // Désabonner l'ancien ViewModel
            if (_vm != null)
            {
                _vm.ScrollToBottomRequested -= ScrollToBottom;
                _vm.Messages.CollectionChanged -= OnMessagesChanged;
            }

            _vm = e.NewValue as MessagesViewModel;

            if (_vm != null)
            {
                _vm.ScrollToBottomRequested += ScrollToBottom;
                _vm.Messages.CollectionChanged += OnMessagesChanged;
            }
        }

        private void OnMessagesChanged(object? sender,
            System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // Petit délai pour laisser WPF rendre les nouveaux éléments avant de scroller
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded,
                new System.Action(ScrollToBottom));
        }

        private void ScrollToBottom()
        {
            MessagesScrollViewer?.ScrollToEnd();
        }

        // ── Enter pour envoyer (Shift+Enter = nouvelle ligne non activée ici) ──
        private void MessageInputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !e.KeyboardDevice.IsKeyDown(Key.LeftShift)
                                   && !e.KeyboardDevice.IsKeyDown(Key.RightShift))
            {
                if (_vm?.SendMessageCommand?.CanExecute(null) == true)
                    _vm.SendMessageCommand.Execute(null);

                e.Handled = true;
            }
        }
    }
}
