using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Fusion.Controls;

public partial class ToastWindow : Window
{
    public ToastWindow(string message)
    {
        InitializeComponent();
        MessageText.Text = message;
        Loaded += async (_, _) =>
        {
            PositionBottomLeft();
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220));
            Root.BeginAnimation(OpacityProperty, fadeIn);
            await Task.Delay(3200);
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(280));
            fadeOut.Completed += (_, _) => Close();
            Root.BeginAnimation(OpacityProperty, fadeOut);
        };
    }

    private void PositionBottomLeft()
    {
        var work = SystemParameters.WorkArea;
        Left = work.Left + 18;
        Top = work.Bottom - ActualHeight - 18;
    }

    public static void Show(string message)
    {
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            var toast = new ToastWindow(message);
            toast.Show();
        }, DispatcherPriority.Normal);
    }
}
