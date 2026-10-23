using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using ImagePerfect.ViewModels;
using ReactiveUI;
using ReactiveUI.Avalonia;
using System.Linq;
using System.Threading.Tasks;

namespace ImagePerfect.Views;

public partial class PickBackUpFolderView : ReactiveUserControl<BackUpViewModel>
{
    /*
     * Styled property: Exposes a property on an Avalonia UI object so the UI framework can manage its value, styling, and related behavior.
     * Vs
     * ViewModel property: Holds application state or business logic, such as the selected folder or image rating.
     * 
     * Styled props are props that are part of Avalonia UI and separate from View Model props. 
     * Avalonia UI framework manages the StyledProps. They exist for use cases like i have below -- to help make a button dynamic
     * 
     * These styled properties let us create two buttons from the same View with different button text and different folder source 
     * 
     * use a styled property if you were building a reusable custom control and wanted consumers to configure its appearance or behavior using Avalonia's styling system
     */

    //PickBackUpFolderView has a property called ButtonText of type string and the default value is "Get Folder"
    public static readonly StyledProperty<string> ButtonTextProperty =
        AvaloniaProperty.Register<PickBackUpFolderView, string>(nameof(ButtonText), "Get Folder");

    //PickBackUpFolderView has a property called FolderType of type string and the default value is "Source"
    public static readonly StyledProperty<string> FolderTypeProperty =
        AvaloniaProperty.Register<PickBackUpFolderView, string>(nameof(FolderType), "Source");

    //expose the styled properties as normal C# properties
    //getter and setter use Avalonia's property system will handle reads and writes
    public string ButtonText
    {
        get => GetValue(ButtonTextProperty);
        set => SetValue(ButtonTextProperty, value);
    }

    public string FolderType
    {
        get => GetValue(FolderTypeProperty);
        set => SetValue(FolderTypeProperty, value);
    }

    public PickBackUpFolderView()
    {
        InitializeComponent();
        this.WhenActivated(d => d(ViewModel!.SelectFolderInteraction.RegisterHandler(this.InteractionHandler)));
    }

    private async Task InteractionHandler(IInteractionContext<string, string?> context)
    {
        TopLevel? topLevel = TopLevel.GetTopLevel(this);
        var folders = await topLevel!.StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions
            {
                AllowMultiple = false,
                Title = context.Input
            });

        context.SetOutput(folders.FirstOrDefault()?.Path.ToString());
    }
}
