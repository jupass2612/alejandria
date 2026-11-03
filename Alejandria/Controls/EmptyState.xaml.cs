using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Alejandria.Controls;

// Ícono, título, mensaje y acción opcional; con IsLoading muestra el anillo.
// Se pasa a los controles en código porque x:Bind interno no se refresca solo
public sealed partial class EmptyState : UserControl
{
    public event RoutedEventHandler? ActionClick;

    public EmptyState()
    {
        InitializeComponent();
        Refrescar();
    }

    public static readonly DependencyProperty GlyphProperty =
        DependencyProperty.Register(nameof(Glyph), typeof(string), typeof(EmptyState), new PropertyMetadata("", OnCambio));

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(EmptyState), new PropertyMetadata(string.Empty, OnCambio));

    public static readonly DependencyProperty MessageProperty =
        DependencyProperty.Register(nameof(Message), typeof(string), typeof(EmptyState), new PropertyMetadata(string.Empty, OnCambio));

    public static readonly DependencyProperty ActionTextProperty =
        DependencyProperty.Register(nameof(ActionText), typeof(string), typeof(EmptyState), new PropertyMetadata(string.Empty, OnCambio));

    public static readonly DependencyProperty IsLoadingProperty =
        DependencyProperty.Register(nameof(IsLoading), typeof(bool), typeof(EmptyState), new PropertyMetadata(false, OnCambio));

    public string Glyph
    {
        get => (string)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public string ActionText
    {
        get => (string)GetValue(ActionTextProperty);
        set => SetValue(ActionTextProperty, value);
    }

    public bool IsLoading
    {
        get => (bool)GetValue(IsLoadingProperty);
        set => SetValue(IsLoadingProperty, value);
    }

    private static void OnCambio(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((EmptyState)d).Refrescar();

    private void Refrescar()
    {
        if (TituloText is null)
        {
            return;
        }

        CargandoRing.IsActive = IsLoading;
        CargandoRing.Visibility = IsLoading ? Visibility.Visible : Visibility.Collapsed;
        IconoBorder.Visibility = IsLoading ? Visibility.Collapsed : Visibility.Visible;

        Icono.Glyph = Glyph;
        TituloText.Text = IsLoading ? "Cargando…" : Title;
        MensajeText.Text = IsLoading ? string.Empty : Message;
        MensajeText.Visibility = string.IsNullOrEmpty(MensajeText.Text) ? Visibility.Collapsed : Visibility.Visible;

        AccionButton.Content = ActionText;
        AccionButton.Visibility = !IsLoading && !string.IsNullOrEmpty(ActionText) ? Visibility.Visible : Visibility.Collapsed;
    }

    private void AccionButton_Click(object sender, RoutedEventArgs e) => ActionClick?.Invoke(this, e);
}
