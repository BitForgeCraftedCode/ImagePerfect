using Avalonia.Controls;
using ImagePerfect.Helpers;
using ImagePerfect.Models;
using ImagePerfect.ObjectMappers;
using ImagePerfect.Repository;
using ImagePerfect.Repository.IRepository;
using Microsoft.Extensions.Configuration;
using MsBox.Avalonia;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Enums;
using MsBox.Avalonia.Models;
using MySqlConnector;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace ImagePerfect.ViewModels
{
	public class PickRootFolderViewModel : ViewModelBase
	{
        private readonly MySqlDataSource _dataSource;
        private readonly IConfiguration _configuration;
        private readonly MainWindowViewModel _mainWindowViewModel;
           
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

        public ReactiveCommand<Unit, Unit> SelectLibraryFolderCommand { get; }

        private async Task SelectLibraryFolder()
        {
            await using UnitOfWork uow = await UnitOfWork.CreateAsync(_dataSource, _configuration);
            FolderMethods folderMethods = new FolderMethods(uow);
            FolderCsvMethods folderCsvMethods = new FolderCsvMethods(uow);  
            Folder? rootFolder = await folderMethods.GetRootFolder();
            if (rootFolder != null)
            {
                await MessageBoxManager.GetMessageBoxCustom(
                    new MessageBoxCustomParams
                    {
                        ButtonDefinitions = new List<ButtonDefinition>
                        {
                            new ButtonDefinition { Name = "Ok", },
                        },
                        ContentTitle = "Add Library",
                        ContentMessage = $"You already have a root library folder. You have to delete your library to add different one.",
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        SizeToContent = SizeToContent.WidthAndHeight,  // <-- lets it grow with content
                        MinWidth = 500  // optional, so it doesn’t wrap too soon
                    }
                ).ShowWindowDialogAsync(Globals.MainWindow);
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
                        DisplayName = PathHelper.GetFolderNameFromFolderPath(_mainWindowViewModel.ExplorerVm.CurrentDirectory),
                        SavedDirectory = _mainWindowViewModel.ExplorerVm.CurrentDirectory
                    };
                    _mainWindowViewModel.HistoryVm.SaveDirectoryItemsList.Add(saveDirectoryItem);
                }
            }
            _mainWindowViewModel.ShowLoading = false;
        }
    }
}