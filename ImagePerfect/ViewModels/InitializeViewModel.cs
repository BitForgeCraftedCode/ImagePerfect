using Avalonia;
using ImagePerfect.Helpers;
using ImagePerfect.Models;
using ImagePerfect.ObjectMappers;
using ImagePerfect.Repository;
using ImagePerfect.Repository.IRepository;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;


namespace ImagePerfect.ViewModels
{
	public class InitializeViewModel : ViewModelBase
	{
        private string _rootFolderLocation = string.Empty;
        private readonly MySqlDataSource _dataSource;
        private readonly IConfiguration _configuration;
        private readonly MainWindowViewModel _mainWindowViewModel;
        public InitializeViewModel(MySqlDataSource dataSource, IConfiguration config, MainWindowViewModel mainWindowViewModel)
        {
            _dataSource = dataSource;
            _configuration = config;
            _mainWindowViewModel = mainWindowViewModel;
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

        public async void Initialize()
        {
            await using UnitOfWork uow = await UnitOfWork.CreateAsync(_dataSource, _configuration);
            ImageMethods imageMethods = new ImageMethods(uow);
            SaveDirectoryMethods saveDirectoryMethods = new SaveDirectoryMethods(uow);

            await GetRootFolder(uow);
            await _mainWindowViewModel.GetTagsList(uow);
            await _mainWindowViewModel.SettingsVm.GetSettings(uow);
            _mainWindowViewModel.ImageDatesVm = await imageMethods.GetImageDates();

            SaveDirectory saveDirectory = await saveDirectoryMethods.GetSavedDirectory();
            if (saveDirectory.SavedDirectory != "")
            {
                //update variables
                _mainWindowViewModel.SavedDirectoryVm.SavedDirectory = saveDirectory.SavedDirectory;
                _mainWindowViewModel.SavedDirectoryVm.SavedFolderPage = saveDirectory.SavedFolderPage;
                _mainWindowViewModel.SavedDirectoryVm.SavedTotalFolderPages = saveDirectory.SavedTotalFolderPages;
                _mainWindowViewModel.SavedDirectoryVm.SavedImagePage = saveDirectory.SavedImagePage;
                _mainWindowViewModel.SavedDirectoryVm.SavedTotalImagePages = saveDirectory.SavedTotalImagePages;
                _mainWindowViewModel.SavedDirectoryVm.SavedOffsetVector = new Vector(saveDirectory.XVector, saveDirectory.YVector);
            }
            else
            {
                //initially set SavedDirectory to CurrentDirectory so method wont fail if btn clicked before saving a directory
                _mainWindowViewModel.SavedDirectoryVm.SavedDirectory = _mainWindowViewModel.ExplorerVm.CurrentDirectory;
            }

        }
    }
}