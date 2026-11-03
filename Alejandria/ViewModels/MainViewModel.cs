using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Alejandria.Models;
using Alejandria.Views;

namespace Alejandria.ViewModels;

// Shell: barra de título y menú
public partial class MainViewModel : ObservableObject
{
    public ObservableCollection<NavItem> NavItems { get; } = new();

    [ObservableProperty]
    private string appTitle = "Alejandría";

    public MainViewModel()
    {
        NavItems.Add(new NavItem { Tag = "Altas", Label = "Altas de Modelos", Icon = "\uE80F", PageType = typeof(AltasModelosPage) });
        NavItems.Add(new NavItem { Tag = "Modelos", Label = "Modelos", Icon = "\uE8A5", PageType = typeof(ModelosPage) });
        NavItems.Add(new NavItem { Tag = "Historico", Label = "Histórico", Icon = "\uE81C", PageType = typeof(HistoricoPage) });
        NavItems.Add(new NavItem { Tag = "Estadisticas", Label = "Estadísticas", Icon = "\uE9D9", PageType = typeof(EstadisticasPage) });
        NavItems.Add(new NavItem { Tag = "Settings", Label = "Ajustes", Icon = "\uE713", PageType = typeof(SettingsPage) });
    }
}
