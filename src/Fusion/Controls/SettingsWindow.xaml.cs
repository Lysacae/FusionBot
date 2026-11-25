using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using Fusion.Services;

namespace Fusion.Controls;

public partial class SettingsWindow : Window
{
    private readonly UpdateService _updates = new();
    private bool _busy;

    public SettingsWindow()
    {
        InitializeComponent();
        VersionLabel.Text = $"Version installée : {AppVersion.Current}";
        GitHubLink.Text = AppVersion.GitHubProfileUrl;
        DiscordLabel.Text = AppVersion.DiscordUsername;
        UpdateMessage.Text = string.Empty;
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private void OnOpenGitHub(object sender, MouseButtonEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = AppVersion.GitHubProfileUrl,
                UseShellExecute = true
            });
        }
        catch { /* ignore */ }
    }

    private void OnCopyDiscord(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(AppVersion.DiscordUsername);
            UpdateMessage.Text = "Pseudo Discord copie.";
        }
        catch
        {
            UpdateMessage.Text = "Impossible de copier.";
        }
    }

    private async void OnCheckUpdates(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        _busy = true;
        CheckUpdateButton.IsEnabled = false;
        CheckUpdateButton.Content = "Verification…";
        UpdateMessage.Text = string.Empty;

        try
        {
            var available = await _updates.CheckForAvailableUpdateAsync();
            if (available is null)
            {
                UpdateMessage.Text = $"Fusion {AppVersion.Current} est a jour.";
            }
            else
            {
                var owner = Owner;
                Close();
                UpdateWindow.Show(owner, available);
            }
        }
        catch
        {
            UpdateMessage.Text = "Impossible de verifier les mises a jour.";
        }
        finally
        {
            _busy = false;
            CheckUpdateButton.IsEnabled = true;
            CheckUpdateButton.Content = "Verifier les mises a jour";
        }
    }

    public static void Show(Window owner)
    {
        var dlg = new SettingsWindow { Owner = owner };
        dlg.ShowDialog();
    }
}
