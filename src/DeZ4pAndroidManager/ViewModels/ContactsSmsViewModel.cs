// © DeZ4p | t.me/DeZ4p | All Rights Reserved

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using DeZ4pAndroidManager.Models;
using DeZ4pAndroidManager.Services;

namespace DeZ4pAndroidManager.ViewModels;

public class ContactsSmsViewModel : INotifyPropertyChanged
{
    private readonly ContactsService _contacts;
    private readonly SmsService _sms;
    private readonly DeviceStateService _deviceState;

    private string _currentSerial = string.Empty;
    private string _contactSearchText = "";
    private string _conversationSearchText = "";
    private string _newNumberText = "";
    private string _composeText = "";
    private ContactItem? _selectedContact;
    private SmsConversation? _selectedConversation;
    private bool _isLoadingAll;
    private bool _isLoadingMessages;
    private bool _isBusy;
    private string _statusMessage = "";

    private List<SmsMessage> _allMessages = new();

    public ContactsSmsViewModel(ContactsService contacts, SmsService sms, DeviceStateService deviceState)
    {
        _contacts = contacts;
        _sms = sms;
        _deviceState = deviceState;

        FilteredContacts = CollectionViewSource.GetDefaultView(Contacts);
        FilteredContacts.Filter = FilterContact;

        FilteredConversations = CollectionViewSource.GetDefaultView(Conversations);
        FilteredConversations.Filter = FilterConversation;

        RefreshCommand = new AsyncRelayCommand(RefreshAllAsync);
        RefreshMessagesCommand = new AsyncRelayCommand(ReloadSelectedMessagesAsync);
        SendSmsCommand = new AsyncRelayCommand(SendAsync);
        CallCommand = new AsyncRelayCommand(CallAsync);
        CopyNumberCommand = new RelayCommand(_ => CopyNumber());
        StartChatFromNumberCommand = new RelayCommand(_ => StartChatFromNumber());

        _deviceState.ConnectionChanged += (_, _) =>
        {
            Application.Current?.Dispatcher.Invoke(async () =>
            {
                try { await OnDeviceChangedAsync(); } catch { }
            });
        };

        _ = OnDeviceChangedAsync();
    }

    public ObservableCollection<ContactItem> Contacts { get; } = new();
    public ObservableCollection<SmsConversation> Conversations { get; } = new();
    public ObservableCollection<SmsMessage> Messages { get; } = new();

    public ICollectionView FilteredContacts { get; }
    public ICollectionView FilteredConversations { get; }

    public ContactItem? SelectedContact
    {
        get => _selectedContact;
        set
        {
            if (_selectedContact == value) return;
            _selectedContact = value;
            OnPropertyChanged();
            if (value != null) OpenChatFor(value);
        }
    }

    public SmsConversation? SelectedConversation
    {
        get => _selectedConversation;
        set
        {
            if (_selectedConversation == value) return;
            _selectedConversation = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelectedConversation));
            OnPropertyChanged(nameof(RecipientDisplay));
            ReloadMessagesFromCache();
        }
    }

    public bool HasSelectedConversation => _selectedConversation != null;

    public string RecipientDisplay => _selectedConversation == null
        ? LocalizationService.Translate("Contacts.SelectConversation")
        : _selectedConversation.DisplayName;

    public string ContactSearchText
    {
        get => _contactSearchText;
        set { _contactSearchText = value ?? ""; OnPropertyChanged(); FilteredContacts.Refresh(); }
    }

    public string ConversationSearchText
    {
        get => _conversationSearchText;
        set { _conversationSearchText = value ?? ""; OnPropertyChanged(); FilteredConversations.Refresh(); }
    }

    public string NewNumberText
    {
        get => _newNumberText;
        set { _newNumberText = value ?? ""; OnPropertyChanged(); }
    }

    public string ComposeText
    {
        get => _composeText;
        set { _composeText = value ?? ""; OnPropertyChanged(); }
    }

    public bool IsLoadingAll
    {
        get => _isLoadingAll;
        set { _isLoadingAll = value; OnPropertyChanged(); }
    }

    public bool IsLoadingMessages
    {
        get => _isLoadingMessages;
        set { _isLoadingMessages = value; OnPropertyChanged(); }
    }

    public bool IsBusy
    {
        get => _isBusy;
        set { _isBusy = value; OnPropertyChanged(); }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value ?? ""; OnPropertyChanged(); }
    }

    public bool HasDevice => !string.IsNullOrEmpty(_currentSerial);

    public ICommand RefreshCommand { get; }
    public ICommand RefreshMessagesCommand { get; }
    public ICommand SendSmsCommand { get; }
    public ICommand CallCommand { get; }
    public ICommand CopyNumberCommand { get; }
    public ICommand StartChatFromNumberCommand { get; }

    private bool FilterContact(object o)
    {
        if (o is not ContactItem c) return false;
        if (string.IsNullOrWhiteSpace(ContactSearchText)) return true;
        var q = ContactSearchText.Trim();
        return (c.DisplayName ?? "").Contains(q, StringComparison.OrdinalIgnoreCase)
            || c.PhoneNumbers.Any(p => p.Contains(q, StringComparison.OrdinalIgnoreCase));
    }

    private bool FilterConversation(object o)
    {
        if (o is not SmsConversation c) return false;
        if (string.IsNullOrWhiteSpace(ConversationSearchText)) return true;
        var q = ConversationSearchText.Trim();
        return (c.DisplayName ?? "").Contains(q, StringComparison.OrdinalIgnoreCase)
            || (c.Address ?? "").Contains(q, StringComparison.OrdinalIgnoreCase)
            || (c.LastMessage ?? "").Contains(q, StringComparison.OrdinalIgnoreCase);
    }

    private async Task OnDeviceChangedAsync()
    {
        var d = _deviceState.CurrentDevice;
        if (d == null || d.IsFastboot)
        {
            _currentSerial = "";
            Contacts.Clear();
            Conversations.Clear();
            Messages.Clear();
            _allMessages.Clear();
            StatusMessage = d == null
                ? LocalizationService.Translate("Contacts.NoDevice")
                : LocalizationService.Translate("Contacts.NeedAdb");
            OnPropertyChanged(nameof(HasDevice));
            return;
        }

        _currentSerial = d.Serial;
        OnPropertyChanged(nameof(HasDevice));
        await RefreshAllAsync();
    }

    public async Task RefreshAllAsync()
    {
        if (!HasDevice) return;

        IsLoadingAll = true;
        try
        {
            // 1) Contacts
            var contactList = await _contacts.ListContactsAsync(_currentSerial);
            Contacts.Clear();
            foreach (var c in contactList) Contacts.Add(c);
            FilteredContacts.Refresh();

            // Build number → contact-name map
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var c in contactList)
            {
                foreach (var num in c.PhoneNumbers)
                {
                    var key = SmsService.NormalizeNumber(num);
                    if (!string.IsNullOrEmpty(key) && !map.ContainsKey(key))
                        map[key] = c.DisplayName;
                }
            }

            // 2) Load ALL messages (paginated)
            _allMessages = await _sms.GetAllMessagesAsync(_currentSerial);

            // 3) Group into conversations
            var convs = _sms.BuildConversations(_allMessages, map);
            Conversations.Clear();
            foreach (var cv in convs) Conversations.Add(cv);
            FilteredConversations.Refresh();

            // Keep previous selection if possible
            if (_selectedConversation != null)
            {
                var still = Conversations.FirstOrDefault(c => c.ThreadId == _selectedConversation.ThreadId);
                if (still != null)
                {
                    _selectedConversation = still;
                    OnPropertyChanged(nameof(SelectedConversation));
                    OnPropertyChanged(nameof(HasSelectedConversation));
                    OnPropertyChanged(nameof(RecipientDisplay));
                    ReloadMessagesFromCache();
                }
                else
                {
                    _selectedConversation = null;
                    Messages.Clear();
                    OnPropertyChanged(nameof(SelectedConversation));
                    OnPropertyChanged(nameof(HasSelectedConversation));
                    OnPropertyChanged(nameof(RecipientDisplay));
                }
            }

            if (Conversations.Count == 0)
            {
                StatusMessage = $"0 conversations. Diag: {_sms.LastDiagnostic.Split('\n').FirstOrDefault() ?? "no data"}";
            }
            else
            {
                StatusMessage = $"{Contacts.Count} {LocalizationService.Translate("Contacts.ContactsCount")}  -  {Conversations.Count} {LocalizationService.Translate("Contacts.ConversationsCount")}";
            }
        }
        catch (Exception ex) { StatusMessage = $"Error: {ex.Message}"; }
        finally { IsLoadingAll = false; }
    }

    private Task ReloadSelectedMessagesAsync()
    {
        ReloadMessagesFromCache();
        return Task.CompletedTask;
    }

    private void ReloadMessagesFromCache()
    {
        Messages.Clear();
        if (_selectedConversation == null) return;

        var filtered = _sms.FilterByAddress(_allMessages, _selectedConversation.Address);
        foreach (var m in filtered) Messages.Add(m);
    }

    private void OpenChatFor(ContactItem contact)
    {
        var number = contact.PrimaryPhone;
        if (string.IsNullOrEmpty(number) || number == "-") return;
        SelectOrCreateConversation(number, contact.DisplayName);
    }

    private void StartChatFromNumber()
    {
        var raw = _newNumberText?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(raw))
        {
            StatusMessage = LocalizationService.Translate("Contacts.EnterNumberFirst");
            return;
        }
        SelectOrCreateConversation(raw, "");
        NewNumberText = "";
    }

    private void SelectOrCreateConversation(string rawNumber, string contactName)
    {
        var key = SmsService.NormalizeNumber(rawNumber);
        var existing = Conversations.FirstOrDefault(c => c.ThreadId == key);
        if (existing != null)
        {
            SelectedConversation = existing;
            StatusMessage = $"{LocalizationService.Translate("Contacts.Opened")}: {existing.DisplayName}";
            return;
        }

        var conv = new SmsConversation
        {
            ThreadId = key,
            Address = rawNumber,
            ContactName = contactName ?? "",
            LastDate = DateTime.MinValue,
            LastMessage = ""
        };
        Conversations.Insert(0, conv);
        FilteredConversations.Refresh();
        SelectedConversation = conv;
        StatusMessage = $"{LocalizationService.Translate("Contacts.NewConversation")}: {conv.DisplayName}";
    }

    private async Task SendAsync()
    {
        if (!HasDevice) return;
        if (_selectedConversation == null)
        {
            StatusMessage = LocalizationService.Translate("Contacts.SelectConversation");
            return;
        }

        IsBusy = true;
        try
        {
            var text = ComposeText ?? "";
            var ok = await _sms.OpenComposerAsync(_currentSerial, _selectedConversation.Address, text);
            StatusMessage = ok
                ? $"✅ {LocalizationService.Translate("Contacts.ComposerOpened")}"
                : $"❌ {LocalizationService.Translate("Contacts.ComposerFailed")}";
            if (ok) ComposeText = "";
        }
        finally { IsBusy = false; }
    }

    private async Task CallAsync()
    {
        if (!HasDevice || _selectedConversation == null) return;

        IsBusy = true;
        try
        {
            var ok = await _sms.OpenDialerAsync(_currentSerial, _selectedConversation.Address);
            StatusMessage = ok
                ? $"📞 {LocalizationService.Translate("Contacts.DialerOpened")}"
                : $"❌ {LocalizationService.Translate("Contacts.DialerFailed")}";
        }
        finally { IsBusy = false; }
    }

    private void CopyNumber()
    {
        if (_selectedConversation == null) return;
        try
        {
            System.Windows.Clipboard.SetText(_selectedConversation.Address);
            StatusMessage = $"📋 {LocalizationService.Translate("Contacts.NumberCopied")}";
        }
        catch { }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}