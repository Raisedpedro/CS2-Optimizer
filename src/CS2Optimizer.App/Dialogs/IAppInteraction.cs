using CS2Optimizer.Core.Engine;

namespace CS2Optimizer.App.Dialogs;

public interface IAppInteraction : IInteraction
{
    void ShowMessage(string title, string message, bool error = false);
    string? PickFolder(string title, string? initialDirectory = null);
    string? SaveFile(string title, string fileName, string filter, string defaultExtension);
}
