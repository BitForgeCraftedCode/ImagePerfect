using Avalonia.Controls;
using Avalonia.Platform.Storage;
using ImagePerfect.ViewModels;
using ReactiveUI;
using ReactiveUI.Avalonia;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ImagePerfect.Views;

public partial class PickFoldersToExtractZipsView : ReactiveUserControl<PickFoldersToExtractZipsViewModel>
{
    public PickFoldersToExtractZipsView()
    {
        InitializeComponent();
        this.WhenActivated(d => d(ViewModel!.SelectZipFoldersInteraction.RegisterHandler(this.InteractionHandler)));
    }

    private async Task InteractionHandler(IInteractionContext<string, List<string>?> context)
    {
        // Get our parent top level control in order to get the needed service (in our sample the storage provider. Can also be the clipboard etc.)
        TopLevel? topLevel = TopLevel.GetTopLevel(this);
        var startLocation = await topLevel!.StorageProvider.TryGetFolderFromPathAsync(context.Input);
        var storageFolders = await topLevel!.StorageProvider.OpenFolderPickerAsync(
                new FolderPickerOpenOptions()
                {
                    AllowMultiple = true,
                    Title = "Select Folders Containing Zips",
                    SuggestedStartLocation = startLocation
                }
            );
        context.SetOutput(storageFolders.Select(x => x.Path.ToString()).ToList());
    }
}