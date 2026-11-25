using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Fusion.Controls;
using Fusion.Models;
using Fusion.Services;
using Hardcodet.Wpf.TaskbarNotification;

namespace Fusion;

public partial class MainWindow : Window
{
    private readonly SecretStore _secrets = new();
    private readonly DiscordBotService _bot = new();
    private readonly UpdateService _updates = new();
    private readonly ObservableCollection<string> _logLines = new();
    private bool _reallyQuit;
    private bool _hiddenToTray;

    public MainWindow()
    {
        InitializeComponent();
        LogList.ItemsSource = _logLines;

        _bot.StatusChanged += OnBotStatusChanged;
        _bot.Log += OnBotLog;
        _bot.AlertRequested += OnBotAlert;
        _bot.GuildsChanged += OnGuildsChanged;

        Opacity = 0;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(280));
        BeginAnimation(OpacityProperty, fade);

        var stored = _secrets.Load();
        TokenField.Secret = stored.DiscordToken;
        GeminiField.Secret = stored.GeminiApiKey;

        ApplyStatus(BotStatusKind.Offline, "HORS LIGNE");
        AppendLog("Fusion est prêt.");

        if (!string.IsNullOrWhiteSpace(stored.DiscordToken) &&
            !string.IsNullOrWhiteSpace(stored.GeminiApiKey))
        {
            await _bot.StartAsync(stored.DiscordToken, stored.GeminiApiKey);
        }

        _ = CheckUpdatesOnStartupAsync();
    }

    private void OnOpenSettings(object sender, RoutedEventArgs e) =>
        SettingsWindow.Show(this);

    private async Task CheckUpdatesOnStartupAsync()
    {
        try
        {
            var update = await _updates.CheckForAvailableUpdateAsync();
            if (update is null) return;
            await Dispatcher.InvokeAsync(() =>
            {
                if (!IsVisible || _hiddenToTray) return;
                UpdateWindow.Show(this, update);
            });
        }
        catch
        {
            // Ne bloque jamais le demarrage.
        }
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_reallyQuit)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }

        TrayIcon.Dispose();
        _bot.Dispose();
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
            HideToTray();
    }

    private void HideToTray()
    {
        _hiddenToTray = true;
        Hide();
        TrayIcon.Visibility = Visibility.Visible;
    }

    private void ShowFromTray()
    {
        _hiddenToTray = false;
        Show();
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;
        Activate();
    }

    private void OnMinimize(object sender, RoutedEventArgs e) => HideToTray();

    private void OnMaximize(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
        MaxIcon.Data = WindowState == WindowState.Maximized
            ? System.Windows.Media.Geometry.Parse("M 2 2 H 10 V 10 H 2 Z M 0 4 H 6 V 12 H 0 Z")
            : System.Windows.Media.Geometry.Parse("M 0 0 H 10 V 10 H 0 Z");
    }

    private void OnClose(object sender, RoutedEventArgs e) => HideToTray();

    private void OnTrayOpen(object sender, RoutedEventArgs e) => ShowFromTray();

    private async void OnTrayQuit(object sender, RoutedEventArgs e)
    {
        _reallyQuit = true;
        await _bot.StopAsync();
        Close();
    }

    private async void OnReloadClick(object sender, RoutedEventArgs e)
    {
        var token = TokenField.Secret.Trim();
        var key = GeminiField.Secret.Trim();
        _secrets.Save(new StoredSecrets { DiscordToken = token, GeminiApiKey = key });
        AppendLog("Clés enregistrées. Rechargement…");
        await _bot.StartAsync(token, key);
    }

    private async void OnStopClick(object sender, RoutedEventArgs e)
    {
        AppendLog("Arrêt demandé.");
        await _bot.StopAsync();
    }

    private void OnCopyInviteClick(object sender, RoutedEventArgs e)
    {
        ulong clientId = _bot.ApplicationId ?? 0;
        if (clientId == 0 &&
            !InviteLinkBuilder.TryExtractClientIdFromToken(TokenField.Secret, out clientId))
        {
            ShowAlert("Impossible de construire le lien d'invitation. Vérifie le token Discord.", true);
            return;
        }

        var link = InviteLinkBuilder.Build(clientId);
        try
        {
            Clipboard.SetText(link);
            AppendLog("Lien d'invitation copié.");
        }
        catch
        {
            ShowAlert("Impossible de copier dans le presse-papiers.", false);
        }
    }

    private void OnOpenGeminiKeyLink(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://aistudio.google.com/apikey",
                UseShellExecute = true
            });
        }
        catch
        {
            ShowAlert("Impossible d'ouvrir le navigateur.", false);
        }
    }

    private async void OnSendClick(object sender, RoutedEventArgs e)
    {
        if (ChannelCombo.SelectedItem is not ChannelItem channel)
        {
            ShowAlert("Choisis un serveur et un salon.", false);
            return;
        }

        var text = MessageBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            ShowAlert("Écris un message à envoyer.", false);
            return;
        }

        var ok = await _bot.SendMessageAsync(channel.Id, text);
        if (ok)
            MessageBox.Text = string.Empty;
    }

    private void OnGuildChanged(object sender, SelectionChangedEventArgs e)
    {
        ChannelCombo.ItemsSource = null;
        if (GuildCombo.SelectedItem is GuildItem guild)
            ChannelCombo.ItemsSource = _bot.GetTextChannels(guild.Id);
    }

    private void OnMessageTextChanged(object sender, TextChangedEventArgs e)
    {
        MessagePlaceholder.Visibility = string.IsNullOrWhiteSpace(MessageBox.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void OnCopyLogClick(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(string.Join(Environment.NewLine, _logLines));
            AppendLog("Journal copié.");
        }
        catch
        {
            ShowAlert("Impossible de copier le journal.", false);
        }
    }

    private void OnBotStatusChanged(BotStatusKind kind, string label)
    {
        Dispatcher.Invoke(() => ApplyStatus(kind, label));
    }

    private void OnBotLog(string message)
    {
        Dispatcher.Invoke(() => AppendLog(message));
    }

    private void OnBotAlert(string message, bool critical)
    {
        Dispatcher.Invoke(() =>
        {
            if (_hiddenToTray || !IsVisible || WindowState == WindowState.Minimized)
                ToastWindow.Show(message);
            else
                AlertWindow.Show(this, critical ? "Fusion — Attention" : "Fusion", message);
        });
    }

    private void OnGuildsChanged()
    {
        Dispatcher.Invoke(() =>
        {
            var selectedId = (GuildCombo.SelectedItem as GuildItem)?.Id;
            GuildCombo.ItemsSource = _bot.Guilds;
            if (selectedId is ulong id)
            {
                GuildCombo.SelectedItem = _bot.Guilds.FirstOrDefault(g => g.Id == id);
            }
            else if (_bot.Guilds.Count > 0)
            {
                GuildCombo.SelectedIndex = 0;
            }
            else
            {
                ChannelCombo.ItemsSource = null;
            }
        });
    }

    private void ApplyStatus(BotStatusKind kind, string label)
    {
        StatusLabel.Text = label;
        var (fill, glow) = kind switch
        {
            BotStatusKind.Online => (Color.FromRgb(0x3D, 0xCF, 0x7A), Color.FromRgb(0x3D, 0xCF, 0x7A)),
            BotStatusKind.Connecting or BotStatusKind.TemporaryIssue =>
                (Color.FromRgb(0xE6, 0xC3, 0x5C), Color.FromRgb(0xE6, 0xC3, 0x5C)),
            BotStatusKind.InvalidCredentials =>
                (Color.FromRgb(0x1A, 0x1A, 0x1A), Color.FromRgb(0x55, 0x55, 0x55)),
            _ => (Color.FromRgb(0xE2, 0x55, 0x55), Color.FromRgb(0xE2, 0x55, 0x55))
        };

        StatusDot.Fill = new SolidColorBrush(fill);
        StatusGlow.Color = glow;
        StatusLabel.Foreground = kind == BotStatusKind.Online
            ? new SolidColorBrush(Color.FromRgb(0x3D, 0xCF, 0x7A))
            : kind is BotStatusKind.Connecting or BotStatusKind.TemporaryIssue
                ? new SolidColorBrush(Color.FromRgb(0xE6, 0xC3, 0x5C))
                : kind == BotStatusKind.InvalidCredentials
                    ? (Brush)FindResource("TextMutedBrush")
                    : (Brush)FindResource("AccentBrush");

        PulseStatus();
    }

    private void PulseStatus()
    {
        var anim = new DoubleAnimation
        {
            From = 0.35,
            To = 0.85,
            Duration = TimeSpan.FromMilliseconds(700),
            AutoReverse = true,
            RepeatBehavior = new RepeatBehavior(1)
        };
        StatusGlow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.OpacityProperty, anim);
    }

    private void AppendLog(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        _logLines.Add(line);
        while (_logLines.Count > 300)
            _logLines.RemoveAt(0);

        Dispatcher.BeginInvoke(() => LogScroll.ScrollToEnd(), DispatcherPriority.Background);
    }

    private void ShowAlert(string message, bool critical)
    {
        var title = critical ? "Fusion — Attention" : "Fusion";
        if (_hiddenToTray || !IsVisible)
        {
            ToastWindow.Show(message);
            return;
        }

        AlertWindow.Show(this, title, message);
    }
}
