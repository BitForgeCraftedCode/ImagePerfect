using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using ImagePerfect.ViewModels;
using ReactiveUI;
using ReactiveUI.Avalonia;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ImagePerfect.Views;

public partial class PickExternalImageViewerExeView : ReactiveUserControl<PickExternalImageViewerExeViewModel>
{
    public PickExternalImageViewerExeView()
    {
        InitializeComponent();
        this.WhenActivated(d => d(ViewModel!.SelectExternalImageViewerExeInteraction.RegisterHandler(this.InteractionHandler)));
    }

    private async Task InteractionHandler(IInteractionContext<string, List<string>?> context)
    {
        // Get our parent top level control in order to get the needed service (in our sample the storage provider. Can also be the clipboard etc.)
        TopLevel? topLevel = TopLevel.GetTopLevel(this);

        var storageFolder = await topLevel!.StorageProvider.OpenFilePickerAsync(
                new FilePickerOpenOptions()
                {
                    AllowMultiple = false,
                    Title = context.Input
                }
            );

        context.SetOutput(storageFolder.Select(x => x.Path.ToString()).ToList());
    }
}