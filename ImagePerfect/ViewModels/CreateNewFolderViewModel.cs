using ImagePerfect.Helpers;
using ImagePerfect.Models;
using ImagePerfect.Repository;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using ReactiveUI;
using Serilog;
using System;
using System.IO;
using System.Threading.Tasks;

namespace ImagePerfect.ViewModels
{
	public class CreateNewFolderViewModel : ViewModelBase
	{
        private string _newFolderName = string.Empty;
        private bool _isNewFolderEnabled;

        private readonly MySqlDataSource _dataSource;
        private readonly IConfiguration _configuration;
        private readonly MainWindowViewModel _mainWindowViewModel;
        public CreateNewFolderViewModel(MySqlDataSource dataSource, IConfiguration config, MainWindowViewModel mainWindowViewModel) 
        {
            _dataSource = dataSource;
            _configuration = config;
            _mainWindowViewModel = mainWindowViewModel;
        }

        public string NewFolderName
        {
            get => _newFolderName;
            set
            {
                this.RaiseAndSetIfChanged(ref _newFolderName, value);
                if (value == "" || _mainWindowViewModel.ExplorerVm.CurrentDirectory == _mainWindowViewModel.InitializeVm.RootFolderLocation || _mainWindowViewModel.ExplorerVm.currentFilter != ExplorerViewModel.Filters.None)
                {
                    IsNewFolderEnabled = false;
                }
                else
                {
                    IsNewFolderEnabled = true;
                }
            }
        }

        public bool IsNewFolderEnabled
        {
            get => _isNewFolderEnabled;
            set => this.RaiseAndSetIfChanged(ref _isNewFolderEnabled, value);
        }

        public async Task CreateNewFolder()
		{
            //first check if directory exists
            string newFolderPath = PathHelper.GetNewFolderPath(_mainWindowViewModel.ExplorerVm.CurrentDirectory, NewFolderName);
            if (Directory.Exists(newFolderPath))
            {
                await MessageBoxHelper.ShowAsync(
                    "New Folder",
                    $"A folder with this name already exists."
                );
                return;
            }
            bool directoryCreated = false;
            bool databaseInsertSucceeded = false;
            try
            {
                await using UnitOfWork uow = await UnitOfWork.CreateAsync(_dataSource, _configuration);
                FolderMethods folderMethods = new FolderMethods(uow);
                //add dir to database -- also need to update parent folders HasChildren bool value
                Folder newFolder = new Folder
                {
                    FolderName = NewFolderName,
                    FolderPath = newFolderPath,
                    HasChildren = false,
                    CoverImagePath = "",
                    FolderDescription = "",
                    FolderRating = 0,
                    HasFiles = false,
                    IsRoot = false,
                    FolderContentMetaDataScanned = false,
                    AreImagesImported = false,
                };
                //first create on disk
                Directory.CreateDirectory(newFolderPath);
                directoryCreated = true;
                databaseInsertSucceeded = await folderMethods.CreateNewFolder(newFolder);
                if (databaseInsertSucceeded)
                {
                    try
                    {
                        //refresh UI
                        _mainWindowViewModel.ExplorerVm.currentFilter = ExplorerViewModel.Filters.None;
                        await _mainWindowViewModel.ExplorerVm.RefreshFolders("", uow);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Folder was created, but refreshing the view failed for {FolderPath}", newFolderPath);
                    }
                }
                else
                {
                    TryRemoveCreatedDirectory(newFolderPath, directoryCreated);
                    await MessageBoxHelper.ShowAsync(
                        "Create New Folder",
                        $"Could not save the folder to the library. The new directory was removed. Please try again."
                    );
                }
            }
            catch(Exception ex)
            {
                Log.Error(ex, "Failed to create folder at {FolderPath}", newFolderPath);
                if (directoryCreated && !databaseInsertSucceeded)
                {
                    TryRemoveCreatedDirectory(newFolderPath, directoryCreated);
                }

                await MessageBoxHelper.ShowAsync(
                    "Create New Folder",
                    "Could not create the folder. Check the logs for details.");
                return;
            }
        }

        private static void TryRemoveCreatedDirectory(string folderPath, bool directoryCreated)
        {
            if (!directoryCreated)
            {
                return;
            }

            try
            {
                Directory.Delete(folderPath);
            }
            catch (Exception cleanupException)
            {
                Log.Error(cleanupException,
                    "Failed to remove folder at {FolderPath} after creation failed",
                    folderPath);
            }
        }
	}
}
