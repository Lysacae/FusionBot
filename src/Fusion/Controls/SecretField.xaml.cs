using System.Windows;
using System.Windows.Controls;

namespace Fusion.Controls;

public partial class SecretField : UserControl
{
    public static readonly DependencyProperty PlaceholderProperty =
        DependencyProperty.Register(nameof(Placeholder), typeof(string), typeof(SecretField),
            new PropertyMetadata(string.Empty));

    private bool _visible;
    private bool _syncing;

    public SecretField()
    {
        InitializeComponent();
        UpdatePlaceholder();
    }

    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public string Secret
    {
        get => _visible ? VisibleBox.Text : HiddenBox.Password;
        set
        {
            _syncing = true;
            HiddenBox.Password = value ?? string.Empty;
            VisibleBox.Text = value ?? string.Empty;
            _syncing = false;
            UpdatePlaceholder();
        }
    }

    private void OnToggleVisibility(object sender, RoutedEventArgs e)
    {
        _visible = !_visible;
        if (_visible)
        {
            VisibleBox.Text = HiddenBox.Password;
            VisibleBox.Visibility = Visibility.Visible;
            HiddenBox.Visibility = Visibility.Collapsed;
            VisibleBox.Focus();
            VisibleBox.CaretIndex = VisibleBox.Text.Length;
        }
        else
        {
            HiddenBox.Password = VisibleBox.Text;
            HiddenBox.Visibility = Visibility.Visible;
            VisibleBox.Visibility = Visibility.Collapsed;
            HiddenBox.Focus();
        }
        UpdatePlaceholder();
    }

    private void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_syncing) return;
        UpdatePlaceholder();
    }

    private void OnVisibleTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_syncing) return;
        UpdatePlaceholder();
    }

    private void UpdatePlaceholder()
    {
        var empty = string.IsNullOrEmpty(Secret);
        PlaceholderLabel.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
    }
}
