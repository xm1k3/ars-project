using System.Threading.Tasks;

namespace ArsStudio.ViewModels;

public interface IDialogService
{
    Task ShowMessageAsync(string message);

    Task<string?> OpenCubeFileAsync();

    Task<bool> SaveCubeFileAsync(string suggestedName, string content);
}
