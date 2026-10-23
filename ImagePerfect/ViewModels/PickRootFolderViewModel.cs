using ImagePerfect.Helpers;
using ImagePerfect.Models;
using ImagePerfect.ObjectMappers;
using ImagePerfect.Repository;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using ReactiveUI;
using ReactiveUI.Primitives;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace ImagePerfect.ViewModels
{
	public class PickRootFolderViewModel : ViewModelBase
	{
        private readonly MySqlDataSource _dataSource;
        private readonly IConfiguration _configuration;
        private readonly MainWindowViewModel _mainWindowViewModel;
        /*
         * The Interaction and Folder Picker work like this:
         * 
         * ReactiveUI interactions let a view model request a UI-specific action—such as
         * opening a dialog or file picker—without putting UI code in the view model.
         *  
         * Interactions act as a request/response bridge: the view model sends a request with a string input and waits for a nullable list 
         * of paths in return. Here, the request string is the picker title, "Select Root Library Folder"
         * 
         * When the view is active, its code-behind registers InteractionHandler to handle SelectFolderInteraction
         * The handler gets the window's TopLevel, calls StorageProvider.OpenFolderPickerAsync with the request string as the title and
         * AllowMultiple = false, then returns the selected folder paths through context.SetOutput(...).
         * 
         * Flow
         * 1. The view is activated as part of Avalonia's view lifecycle when it enters the active UI (for example, when its window is shown). 
         * At that point, WhenActivated runs and registers InteractionHandler
         * 2. The user clicks the button -> runs the command.
         * 3. The command raises the interaction -> the handler opens the picker and returns its result to the waiting view-model code
         */
        public PickRootFolderViewModel(MySqlDataSource dataSource, IConfiguration config, MainWindowViewModel mainWindowViewModel) 
		{
            _dataSource = dataSource;
            _configuration = config;
            _SelectFolderInteraction = new Interaction<string, List<string>?>();
            SelectLibraryFolderCommand = ReactiveCommand.CreateFromTask(SelectLibraryFolder);
            _mainWindowViewModel = mainWindowViewModel;
        }

        private List<string>? _RootFolderPath;
        
        private readonly Interaction<string, List<string>?> _SelectFolderInteraction;

        public Interaction<string, List<string>?> SelectFolderInteraction { get { return _SelectFolderInteraction; } }

        public ReactiveCommand<RxVoid, RxVoid> SelectLibraryFolderCommand { get; }

        private async Task SelectLibraryFolder()
        {
            await using UnitOfWork uow = await UnitOfWork.CreateAsync(_dataSource, _configuration);
            FolderMethods folderMethods = new FolderMethods(uow);
            FolderCsvMethods folderCsvMethods = new FolderCsvMethods(uow);  
            Folder? rootFolder = await folderMethods.GetRootFolder();
            if (rootFolder != null)
            {
                await MessageBoxHelper.ShowAsync(
                    "Add Library",
                    $"You already have a root library folder. You have to delete your library to add different one."
                );
                return;
            }

            _RootFolderPath = await _SelectFolderInteraction.Handle("Select Root Library Folder");
            //list will be empty if Cancel is pressed exit method
            if (_RootFolderPath.Count == 0) 
            {
                return;
            }
            
            _mainWindowViewModel.ShowLoading = true;
            //build csv
            bool csvIsSet = await FolderCsvMethods.AddNewFoldersCsv(_RootFolderPath, true);
            //write csv to database
            if (csvIsSet) 
            {
                await folderCsvMethods.AddFolderCsv();

                rootFolder = await folderMethods.GetRootFolder();
                if (rootFolder != null)
                {
                    FolderViewModel rootFolderVm = await FolderMapper.GetFolderVm(rootFolder);
                    _mainWindowViewModel.LibraryFolders.Add(rootFolderVm);

                    _mainWindowViewModel.InitializeVm.RootFolderLocation = PathHelper.RemoveOneFolderFromPath(rootFolder.FolderPath);
                    _mainWindowViewModel.ExplorerVm.CurrentDirectory = _mainWindowViewModel.InitializeVm.RootFolderLocation;
                    //initially set SavedDirectory to CurrentDirectory so method wont fail if btn clicked before saving a directory
                    SaveDirectory saveDirectoryItem = new SaveDirectory
                    {
                        DisplayName = PathHelper.GetHistroyDisplayNameFromPath(_mainWindowViewModel.ExplorerVm.CurrentDirectory),
                        SavedDirectory = _mainWindowViewModel.ExplorerVm.CurrentDirectory
                    };
                    _mainWindowViewModel.HistoryVm.SaveDirectoryItemsList.Add(saveDirectoryItem);
                    _mainWindowViewModel.InitializeVm.HasRootLibrary = true;
                }
            }
            _mainWindowViewModel.ShowLoading = false;
        }
    }
}