using Avalonia;
using ImagePerfect.Helpers;
using ImagePerfect.Models;
using ImagePerfect.ObjectMappers;
using ImagePerfect.Repository;
using ImagePerfect.Repository.IRepository;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;


namespace ImagePerfect.ViewModels
{
	public class InitializeViewModel : ViewModelBase
	{
        private string _rootFolderLocation = string.Empty;
        private bool _hasRootLibrary = true;
        private readonly MySqlDataSource _dataSource;
        private readonly IConfiguration _configuration;
        private readonly MainWindowViewModel _mainWindowViewModel;
        public InitializeViewModel(MySqlDataSource dataSource, IConfiguration config, MainWindowViewModel mainWindowViewModel)
        {
            _dataSource = dataSource;
            _configuration = config;
            _mainWindowViewModel = mainWindowViewModel;
        }

        //used to hide UI buttons if no RootLibrary is selected
        public bool HasRootLibrary
        {
            get => _hasRootLibrary;
            set => this.RaiseAndSetIfChanged(ref _hasRootLibrary, value);
        }
        public string RootFolderLocation
        {
            get => _rootFolderLocation;
            set => _rootFolderLocation = value;
        }

        private async Task GetRootFolder(UnitOfWork uow)
        {
            FolderMethods folderMethods = new FolderMethods(uow);
            Folder? rootFolder = await folderMethods.GetRootFolder();
            if (rootFolder != null)
            {
                FolderViewModel rootFolderVm = await FolderMapper.GetFolderVm(rootFolder);
                _mainWindowViewModel.LibraryFolders.Add(rootFolderVm);
                RootFolderLocation = PathHelper.RemoveOneFolderFromPath(rootFolder.FolderPath);
                _mainWindowViewModel.ExplorerVm.CurrentDirectory = RootFolderLocation;
            }
        }

        public async Task Initialize()
        {
            await using UnitOfWork uow = await UnitOfWork.CreateAsync(_dataSource, _configuration);
            ImageMethods imageMethods = new ImageMethods(uow);
            SaveDirectoryMethods saveDirectoryMethods = new SaveDirectoryMethods(uow);

            await GetRootFolder(uow);
            await _mainWindowViewModel.GetTagsList(uow);
            await _mainWindowViewModel.SettingsVm.GetSettings(uow);
            _mainWindowViewModel.ImageDatesVm = await imageMethods.GetImageDates();

            if (string.IsNullOrEmpty(RootFolderLocation))
            {
                HasRootLibrary = false;
            }

            SaveDirectory saveDirectory = await saveDirectoryMethods.GetSavedDirectory();
            if (saveDirectory.SavedDirectory != "")
            {
                //update variables
                SaveDirectory saveDirectoryItem = new SaveDirectory
                {
                    DisplayName = PathHelper.GetHistroyDisplayNameFromPath(saveDirectory.SavedDirectory),
                    SavedDirectory = saveDirectory.SavedDirectory,
                    SavedFolderPage = saveDirectory.SavedFolderPage,
                    SavedTotalFolderPages = saveDirectory.SavedTotalFolderPages,
                    SavedImagePage = saveDirectory.SavedImagePage,
                    SavedTotalImagePages = saveDirectory.SavedTotalImagePages,
                    SavedOffsetVector = new Vector(saveDirectory.XVector, saveDirectory.YVector)
                };
                _mainWindowViewModel.HistoryVm.SaveDirectoryItemsList.Add(saveDirectoryItem);

            }
            else if(RootFolderLocation != "")
            {
                //initially set SavedDirectory to CurrentDirectory so method wont fail if btn clicked before saving a directory
                SaveDirectory saveDirectoryItem = new SaveDirectory
                {
                    DisplayName = PathHelper.GetHistroyDisplayNameFromPath(_mainWindowViewModel.ExplorerVm.CurrentDirectory),
                    SavedDirectory = _mainWindowViewModel.ExplorerVm.CurrentDirectory
                };
                _mainWindowViewModel.HistoryVm.SaveDirectoryItemsList.Add(saveDirectoryItem);
            }

        }
    }
}