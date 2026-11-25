using System.Windows;
using Fusion.Services;

namespace Fusion.Controls;

public partial class UpdateWindow : Window
{
    private readonly LatestUpdate _update;
    private readonly UpdateService _service = new();
    private bool _busy;

    public UpdateWindow(LatestUpdate update)
    {
        InitializeComponent();
        _update = update;
        VersionText.Text = $"Vous avez la version {AppVersion.Current}. La version {update.Version} est disponible.";
        NotesText.Text = string.IsNullOrWhiteSpace(update.Notes)
            ? "Aucune note de version."
            : update.Notes.Trim();
    }

    private void OnLater(object sender, RoutedEventArgs e) => DialogResult = false;

    private async void OnInstall(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        _busy = true;
        InstallButton.IsEnabled = false;
        LaterButton.IsEnabled = false;
        InstallButton.Content = "Installation…";
        StatusText.Text = "Fusion va se fermer. La nouvelle version remplace l'ancienne automatiquement.";
        StatusText.Visibility = Visibility.Visible;

        try
        {
            await _service.DownloadAndInstallAsync(_update);
            Application.Current.Shutdown();
        }
        catch (Exception)
        {
            StatusText.Text = "Echec du telechargement. Ouverture du lien de release…";
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = _update.DownloadUrl,
                    UseShellExecute = true
                });
            }
            catch { /* ignore */ }

            _busy = false;
            InstallButton.IsEnabled = true;
            LaterButton.IsEnabled = true;
            InstallButton.Content = "Installer";
        }
    }

    public static bool? Show(Window? owner, LatestUpdate update)
    {
        var dlg = new UpdateWindow(update);
        if (owner is not null && owner.IsVisible)
            dlg.Owner = owner;
        return dlg.ShowDialog();
    }
}
