using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Alejandria.Services;

// Diálogos de guardar/abrir; en WinUI 3 necesitan el HWND
public static class FilePickerHelper
{
    // Ruta elegida o null
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

    // Ruta elegida o null
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
