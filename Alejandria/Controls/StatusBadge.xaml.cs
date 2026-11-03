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
        // x:Bind OneWay a una DP no se refresca solo, hay que llamar Bindings.Update()
        ((StatusBadge)d).Bindings.Update();
    }

    public string Status
    {
        get => (string)GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }
}
