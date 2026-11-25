using System.Windows;

namespace Fusion.Controls;

public partial class AlertWindow : Window
{
    public AlertWindow(string title, string message)
    {
        InitializeComponent();
        TitleText.Text = title;
        MessageText.Text = message;
    }

    private void OnOk(object sender, RoutedEventArgs e) => DialogResult = true;

    public static void Show(Window? owner, string title, string message)
    {
        var dlg = new AlertWindow(title, message);
        if (owner is not null && owner.IsVisible)
        {
            dlg.Owner = owner;
            dlg.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
        else
        {
            dlg.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }
        dlg.ShowDialog();
    }
}
