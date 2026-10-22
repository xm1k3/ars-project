using System.IO;
using System.Threading.Tasks;
using ArsStudio.Core;
using ArsStudio.ViewModels;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace ArsStudio.Views;

public sealed class DialogService(Window owner) : IDialogService
{
    private static readonly FilePickerFileType CubeStateType = new("Cube State")
    {
        Patterns = [$"*.{CubeStateFile.Extension}"],
    };

    public Task ShowMessageAsync(string message) => new MessageWindow(message).ShowDialog(owner);

    public async Task<string?> OpenCubeFileAsync()
    {
        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            FileTypeFilter = [CubeStateType],
        });

        if (files.Count == 0)
        {
            return null;
        }

        await using var stream = await files[0].OpenReadAsync();
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    public async Task<bool> SaveCubeFileAsync(string suggestedName, string content)
    {
        var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedFileName = suggestedName,
            DefaultExtension = CubeStateFile.Extension,
            FileTypeChoices = [CubeStateType],
        });

        if (file is null)
        {
            return false;
        }

        await using var stream = await file.OpenWriteAsync();
        await using var writer = new StreamWriter(stream);
        await writer.WriteAsync(content);
        return true;
    }
}
