using GroupyV.Data;
using GroupyV.Helpers;
using GroupyV.Models;
using GroupyV.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace GroupyV.ViewModels
{
    // ─── Display models ──────────────────────────────────────────────────────────

    public class ConversationItemVm : BaseViewModel
    {
        public int IdConversation { get; set; }
        public int IdClient { get; set; }
        public int IdVendeur { get; set; }
        public int IdPrevente { get; set; }
        public string ClientNomComplet { get; set; } = "";
        public string ClientInitiales { get; set; } = "?";
        public string NomProduit { get; set; } = "—";
        public string LastMessage { get; set; } = "";
        public DateTime? LastMessageAt { get; set; }

        private int _unreadCount;
        public int UnreadCount
        {
            get => _unreadCount;
            set { _unreadCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasUnread)); }
        }

        public bool HasUnread => UnreadCount > 0;

        public string TimeDisplay
        {
            get
            {
                if (!LastMessageAt.HasValue) return "";
                var delta = DateTime.Now - LastMessageAt.Value;
                if (delta.TotalMinutes < 1) return "À l'instant";
                if (delta.TotalHours < 1) return $"{(int)delta.TotalMinutes} min";
                if (delta.TotalHours < 24) return LastMessageAt.Value.ToString("HH:mm");
                if (delta.TotalDays < 7) return LastMessageAt.Value.ToString("ddd");
                return LastMessageAt.Value.ToString("dd/MM");
            }
        }
    }

    public class ChatMessageVm
    {
        public int IdMessage { get; set; }
        public string Contenu { get; set; } = "";
        public DateTime? CreatedAt { get; set; }
        public bool IsMine { get; set; }
        public string SenderName { get; set; } = "";
        public string Initiales { get; set; } = "?";
        public string TimeText => CreatedAt?.ToString("HH:mm") ?? "";
        public string DateLabel => CreatedAt?.ToString("dd/MM/yyyy") ?? "";
    }

    // ─── ViewModel ───────────────────────────────────────────────────────────────

    public class MessagesViewModel : BaseViewModel, IDisposable
    {
        private readonly int _vendeurId;
        private readonly DispatcherTimer _refreshTimer;
        private ObservableCollection<ConversationItemVm> _allConversations = new();

        /// <summary>
        /// Empêche le setter de SelectedConversation de réagir aux null
        /// envoyés par le ListBox quand on remplace FilteredConversations.
        /// </summary>
        private bool _isUpdatingCollection = false;

        // ── Collections ──────────────────────────────────────────────────────────

        private ObservableCollection<ConversationItemVm> _filteredConversations = new();
        public ObservableCollection<ConversationItemVm> FilteredConversations
        {
            get => _filteredConversations;
            private set { _filteredConversations = value; OnPropertyChanged(); }
        }

        private ObservableCollection<ChatMessageVm> _messages = new();
        public ObservableCollection<ChatMessageVm> Messages
        {
            get => _messages;
            private set { _messages = value; OnPropertyChanged(); }
        }

        // ── Selection ────────────────────────────────────────────────────────────

        private ConversationItemVm? _selectedConversation;
        public ConversationItemVm? SelectedConversation
        {
            get => _selectedConversation;
            set
            {
                // Ignorer le null envoyé par le ListBox quand on remplace la collection
                if (_isUpdatingCollection && value == null) return;

                if (_selectedConversation?.IdConversation == value?.IdConversation) return;
                _selectedConversation = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasConversationSelected));
                OnPropertyChanged(nameof(HasNoConversationSelected));
                if (value != null)
                    _ = LoadMessagesAsync(value.IdConversation);
                else
                    Messages.Clear();
            }
        }

        public bool HasConversationSelected => _selectedConversation != null;
        public bool HasNoConversationSelected => _selectedConversation == null;

        // ── UI State ─────────────────────────────────────────────────────────────

        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set { _searchText = value; OnPropertyChanged(); ApplyFilter(); }
        }

        private string _newMessageText = "";
        public string NewMessageText
        {
            get => _newMessageText;
            set { _newMessageText = value; OnPropertyChanged(); }
        }

        private bool _isLoadingMessages;
        public bool IsLoadingMessages
        {
            get => _isLoadingMessages;
            set { _isLoadingMessages = value; OnPropertyChanged(); }
        }

        private bool _isSending;
        public bool IsSending
        {
            get => _isSending;
            set { _isSending = value; OnPropertyChanged(); }
        }

        private int _totalUnread;
        public int TotalUnread
        {
            get => _totalUnread;
            set { _totalUnread = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasUnreadBadge)); }
        }

        public bool HasUnreadBadge => TotalUnread > 0;

        // ── Commands ─────────────────────────────────────────────────────────────

        public ICommand SendMessageCommand { get; }
        public ICommand RefreshCommand { get; }

        // ── Events ───────────────────────────────────────────────────────────────

        /// <summary>Déclenché quand il faut scroller en bas du chat.</summary>
        public event Action? ScrollToBottomRequested;

        // ─────────────────────────────────────────────────────────────────────────

        public MessagesViewModel()
        {
            _vendeurId = UserSession.Instance.CurrentUser?.IdUser ?? 0;

            SendMessageCommand = new RelayCommand(
                async _ => await SendMessageAsync(),
                _ => !IsSending && !string.IsNullOrWhiteSpace(NewMessageText) && SelectedConversation != null
            );

            RefreshCommand = new RelayCommand(async _ => await RefreshCurrentAsync());

            // Auto-refresh toutes les 10 secondes
            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            _refreshTimer.Tick += async (_, _) => await SilentRefreshAsync();
            _refreshTimer.Start();

            _ = LoadConversationsAsync();
        }

        // ── Data loading ─────────────────────────────────────────────────────────

        private async Task LoadConversationsAsync()
        {
            try
            {
                int currentSelectionId = _selectedConversation?.IdConversation ?? 0;

                var convs = await Task.Run(() =>
                {
                    using var db = new GroupyContext();

                    // Conversations du vendeur connecté
                    var conversations = db.Conversations
                        .Where(c => c.IdVendeur == _vendeurId)
                        .AsNoTracking()
                        .ToList();

                    if (!conversations.Any())
                        return Enumerable.Empty<ConversationItemVm>().ToList();

                    var convIds    = conversations.Select(c => c.IdConversation).ToList();
                    var clientIds  = conversations.Select(c => c.IdClient).Distinct().ToList();
                    var preventeIds = conversations.Select(c => c.IdPrevente).Distinct().ToList();

                    // Clients (depuis utilisateur)
                    var clients = db.Utilisateurs
                        .Where(u => clientIds.Contains(u.IdUser))
                        .AsNoTracking()
                        .ToDictionary(u => u.IdUser);

                    // Préventes + Produit
                    var preventes = db.Preventes
                        .Include(p => p.Produit)
                        .Where(p => preventeIds.Contains(p.IdPrevente))
                        .AsNoTracking()
                        .ToDictionary(p => p.IdPrevente);

                    // Tous les messages de ces conversations
                    var allMessages = db.ChatMessages
                        .Where(m => convIds.Contains(m.IdConversation))
                        .AsNoTracking()
                        .ToList();

                    return conversations.Select(c =>
                    {
                        clients.TryGetValue(c.IdClient, out var client);
                        preventes.TryGetValue(c.IdPrevente, out var prevente);

                        var convMsgs = allMessages
                            .Where(m => m.IdConversation == c.IdConversation)
                            .OrderByDescending(m => m.CreatedAt)
                            .ToList();

                        var lastMsg    = convMsgs.FirstOrDefault();
                        int unreadCount = convMsgs.Count(m => m.IdExpediteur != _vendeurId && !m.Lu);

                        string prenom   = client?.Prenom ?? "";
                        string nom      = client?.Nom ?? "";
                        string initials = ((prenom.Length > 0 ? prenom[0].ToString() : "") +
                                           (nom.Length    > 0 ? nom[0].ToString()    : "")).ToUpper();

                        return new ConversationItemVm
                        {
                            IdConversation   = c.IdConversation,
                            IdClient         = c.IdClient,
                            IdVendeur        = c.IdVendeur,
                            IdPrevente       = c.IdPrevente,
                            ClientNomComplet = $"{prenom} {nom}".Trim(),
                            ClientInitiales  = string.IsNullOrEmpty(initials) ? "?" : initials,
                            NomProduit       = prevente?.Produit?.NomProduit ?? "—",
                            LastMessage      = lastMsg?.Contenu ?? "",
                            LastMessageAt    = lastMsg?.CreatedAt ?? c.UpdatedAt ?? c.CreatedAt,
                            UnreadCount      = unreadCount
                        };
                    })
                    .OrderByDescending(c => c.LastMessageAt)
                    .ToList();
                });

                _allConversations = new ObservableCollection<ConversationItemVm>(convs);
                TotalUnread       = convs.Sum(c => c.UnreadCount);
                ApplyFilter();

                // Rétablir la sélection si elle existait
                if (currentSelectionId != 0)
                {
                    var restoredConv = FilteredConversations
                        .FirstOrDefault(c => c.IdConversation == currentSelectionId);
                    if (restoredConv != null && _selectedConversation?.IdConversation == currentSelectionId)
                    {
                        // Mettre à jour le badge sans changer la sélection
                        _selectedConversation = restoredConv;
                        OnPropertyChanged(nameof(SelectedConversation));
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Messagerie] Erreur chargement conversations : {ex.Message}");
            }
        }

        private async Task LoadMessagesAsync(int conversationId)
        {
            IsLoadingMessages = true;
            try
            {
                var msgs = await Task.Run(() =>
                {
                    using var db = new GroupyContext();

                    // Marquer comme lus
                    var unread = db.ChatMessages
                        .Where(m => m.IdConversation == conversationId
                                 && m.IdExpediteur != _vendeurId
                                 && !m.Lu)
                        .ToList();

                    if (unread.Any())
                    {
                        foreach (var m in unread) m.Lu = true;
                        db.SaveChanges();
                    }

                    // Charger les messages avec info expéditeur
                    return db.ChatMessages
                        .Include(m => m.Expediteur)
                        .Where(m => m.IdConversation == conversationId)
                        .OrderBy(m => m.CreatedAt)
                        .AsNoTracking()
                        .ToList()
                        .Select(m => new ChatMessageVm
                        {
                            IdMessage  = m.IdMessage,
                            Contenu    = m.Contenu ?? "",
                            CreatedAt  = m.CreatedAt,
                            IsMine     = m.IdExpediteur == _vendeurId,
                            SenderName = m.Expediteur != null
                                ? $"{m.Expediteur.Prenom} {m.Expediteur.Nom}".Trim()
                                : "Inconnu",
                            Initiales  = GetInitiales(m.Expediteur)
                        })
                        .ToList();
                });

                Messages.Clear();
                foreach (var msg in msgs)
                    Messages.Add(msg);

                ScrollToBottomRequested?.Invoke();

                // Réinitialiser le badge de la conversation sélectionnée
                var conv = _allConversations.FirstOrDefault(c => c.IdConversation == conversationId);
                if (conv != null)
                {
                    conv.UnreadCount = 0;
                    TotalUnread      = _allConversations.Sum(c => c.UnreadCount);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Messagerie] Erreur chargement messages : {ex.Message}");
            }
            finally
            {
                IsLoadingMessages = false;
            }
        }

        // ── Send ─────────────────────────────────────────────────────────────────

        private async Task SendMessageAsync()
        {
            if (SelectedConversation == null || string.IsNullOrWhiteSpace(NewMessageText)) return;

            IsSending = true;
            CommandManager.InvalidateRequerySuggested();

            string content        = NewMessageText.Trim();
            int    conversationId = SelectedConversation.IdConversation;
            NewMessageText        = "";

            try
            {
                await Task.Run(() =>
                {
                    using var db = new GroupyContext();
                    db.ChatMessages.Add(new ChatMessage
                    {
                        IdConversation = conversationId,
                        IdExpediteur   = _vendeurId,
                        Contenu        = content,
                        Lu             = false,
                        CreatedAt      = DateTime.Now
                    });
                    db.SaveChanges();

                    // Mettre à jour updated_at de la conversation
                    var conv = db.Conversations.Find(conversationId);
                    if (conv != null)
                    {
                        conv.UpdatedAt = DateTime.Now;
                        db.SaveChanges();
                    }
                });

                // Ajouter immédiatement dans la liste locale
                Messages.Add(new ChatMessageVm
                {
                    Contenu    = content,
                    CreatedAt  = DateTime.Now,
                    IsMine     = true,
                    SenderName = UserSession.Instance.GetNomComplet(),
                    Initiales  = UserSession.Instance.GetInitiales()
                });

                ScrollToBottomRequested?.Invoke();

                // Rafraîchir la liste des conversations
                await LoadConversationsAsync();
            }
            catch (Exception ex)
            {
                NewMessageText = content; // Restaurer le texte en cas d'erreur
                MessageBox.Show($"Erreur lors de l'envoi : {ex.Message}", "Erreur",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsSending = false;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        // ── Refresh ──────────────────────────────────────────────────────────────

        private async Task RefreshCurrentAsync()
        {
            await LoadConversationsAsync();
            if (_selectedConversation != null)
                await LoadMessagesAsync(_selectedConversation.IdConversation);
        }

        /// <summary>Rafraîchissement silencieux (timer) : ne recharge les messages
        /// que s'il y a de nouveaux non-lus pour ne pas perdre le scroll.</summary>
        private async Task SilentRefreshAsync()
        {
            await LoadConversationsAsync();

            if (_selectedConversation == null) return;

            // Recharger seulement si nouveaux messages
            var conv = _allConversations
                .FirstOrDefault(c => c.IdConversation == _selectedConversation.IdConversation);

            if (conv?.UnreadCount > 0)
                await LoadMessagesAsync(_selectedConversation.IdConversation);
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private void ApplyFilter()
        {
            int? selectedId = _selectedConversation?.IdConversation;

            // Bloquer les null parasites du ListBox pendant le remplacement de la collection
            _isUpdatingCollection = true;
            try
            {
                if (string.IsNullOrWhiteSpace(_searchText))
                {
                    FilteredConversations = new ObservableCollection<ConversationItemVm>(_allConversations);
                }
                else
                {
                    string q = _searchText.ToLowerInvariant();
                    FilteredConversations = new ObservableCollection<ConversationItemVm>(
                        _allConversations.Where(c =>
                            (c.ClientNomComplet?.ToLowerInvariant().Contains(q) ?? false) ||
                            (c.NomProduit?.ToLowerInvariant().Contains(q)       ?? false) ||
                            (c.LastMessage?.ToLowerInvariant().Contains(q)      ?? false)
                        )
                    );
                }
            }
            finally
            {
                _isUpdatingCollection = false;
            }

            // Remettre à jour la référence de l'objet sélectionné (nouvel objet dans la nouvelle collection)
            if (selectedId.HasValue)
            {
                var restored = FilteredConversations
                    .FirstOrDefault(c => c.IdConversation == selectedId.Value);

                if (restored != null)
                {
                    _selectedConversation = restored;
                    // Notifier le ListBox pour qu'il re-sélectionne la bonne ligne
                    OnPropertyChanged(nameof(SelectedConversation));
                }
            }
        }

        private static string GetInitiales(Utilisateur? u)
        {
            if (u == null) return "?";
            string p = u.Prenom?.Length > 0 ? u.Prenom[0].ToString() : "";
            string n = u.Nom?.Length    > 0 ? u.Nom[0].ToString()    : "";
            return (p + n).ToUpper();
        }

        public void Dispose()
        {
            _refreshTimer.Stop();
        }
    }
}
