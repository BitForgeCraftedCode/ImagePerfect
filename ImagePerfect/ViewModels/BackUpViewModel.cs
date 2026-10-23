using ImagePerfect.Helpers;
using ReactiveUI;
using System.Threading.Tasks;

namespace ImagePerfect.ViewModels;

public class BackUpViewModel : ViewModelBase
{
    private string? _sourceFolderPath;
    private string? _destinationFolderPath;
    private string _rsyncCommand;

    public BackUpViewModel()
    {
        _SelectFolderInteraction = new Interaction<string, string?>();
        PickFolderCommand = ReactiveCommand.Create(async (string folderType) => await PickFolderAsync(folderType));
    }

    private readonly Interaction<string, string?> _SelectFolderInteraction;
    public Interaction<string, string?> SelectFolderInteraction { get { return _SelectFolderInteraction; } }

    public ReactiveCommand<string, Task> PickFolderCommand { get; }

    public string? SourceFolderPath
    {
        get => _sourceFolderPath;
        set => this.RaiseAndSetIfChanged(ref _sourceFolderPath, value);
    }

    public string? DestinationFolderPath
    {
        get => _destinationFolderPath;
        set => this.RaiseAndSetIfChanged(ref _destinationFolderPath, value);
    }

    public string RsyncCommand
    {
        get => _rsyncCommand;
        set => this.RaiseAndSetIfChanged(ref _rsyncCommand, value);
    }

    private async Task PickFolderAsync(string folderType)
    {
        string? selectedPath = await SelectFolderInteraction.Handle($"Select {folderType} Folder");
        if (string.IsNullOrWhiteSpace(selectedPath))
            return;

        if (folderType == "Source")
            SourceFolderPath = PathHelper.FormatPathForImageBackUpFolder(selectedPath);
        else if (folderType == "Destination")
            DestinationFolderPath = PathHelper.FormatPathForImageBackUpFolder(selectedPath);

        RsyncCommand = "rsync -av --delete " + SourceFolderPath + " " + DestinationFolderPath;

    }
}
