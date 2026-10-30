using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Alejandria.Services;

// Diálogos nativos de guardar/abrir archivo. En WinUI 3 de escritorio el
// picker necesita el HWND de la ventana, por eso se inicializa antes de mostrarlo.
public static class FilePickerHelper
{
    // Devuelve la ruta elegida, o null si se canceló.
    public static async Task<string?> PickSaveFileAsync(string suggestedFileName, string fileTypeDescription, string extension)
    {
        if (App.MainWindow is null)
        {
            return null;
        }

        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.Desktop,
            SuggestedFileName = suggestedFileName
        };
        picker.FileTypeChoices.Add(fileTypeDescription, new List<string> { extension });

        var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
        InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSaveFileAsync();
        return file?.Path;
    }

    // Devuelve la ruta elegida, o null si se canceló.
    public static async Task<string?> PickOpenFileAsync(string extension)
    {
        if (App.MainWindow is null)
        {
            return null;
        }

        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.Desktop
        };
        picker.FileTypeFilter.Add(extension);

        var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
        InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSingleFileAsync();
        return file?.Path;
    }
}
