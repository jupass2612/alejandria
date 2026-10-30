using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Alejandria.Controls;

public sealed partial class StatusBadge : UserControl
{
    public StatusBadge()
    {
        InitializeComponent();
    }

    public static readonly DependencyProperty StatusProperty =
        DependencyProperty.Register(nameof(Status), typeof(string), typeof(StatusBadge), new PropertyMetadata("Pendiente", OnStatusChanged));

    private static void OnStatusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        // x:Bind Mode=OneWay a una DependencyProperty NO se refresca solo
        // cuando cambia después de creado el control — hace falta avisarle
        // explícitamente que vuelva a evaluar los bindings internos.
        ((StatusBadge)d).Bindings.Update();
    }

    public string Status
    {
        get => (string)GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }
}
