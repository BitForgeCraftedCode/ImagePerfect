using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using ImagePerfect.Helpers;
using ImagePerfect.Models;
using ImagePerfect.ObjectMappers;
using ImagePerfect.Repository.IRepository;


namespace ImagePerfect.ViewModels
{
	public class InitializeViewModel : ViewModelBase
	{
        private string _rootFolderLocation = string.Empty;
        private readonly IUnitOfWork _unitOfWork;
        private readonly SaveDirectoryMethods _saveDirectoryMethods;
        private readonly FolderMethods _folderMethods;
        private readonly ImageMethods _imageMethods;
        private readonly MainWindowViewModel _mainWindowViewModel;
        public InitializeViewModel(IUnitOfWork unitOfWork, MainWindowViewModel mainWindowViewModel) 
		{
            _unitOfWork = unitOfWork;
            _mainWindowViewModel = mainWindowViewModel;
            _saveDirectoryMethods = new SaveDirectoryMethods(_unitOfWork);
            _folderMethods = new FolderMethods(_unitOfWork);
            _imageMethods = new ImageMethods(_unitOfWork);
        }

        public string RootFolderLocation
        {
            get => _rootFolderLocation;
            set => _rootFolderLocation = value;
        }

        private async Task GetRootFolder()
        {
            Folder? rootFolder = await _folderMethods.GetRootFolder();
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
            await GetRootFolder();
            await _mainWindowViewModel.GetTagsList();
            await _mainWindowViewModel.SettingsVm.GetSettings();
            _mainWindowViewModel.ImageDatesVm = await _imageMethods.GetImageDates();

            SaveDirectory saveDirectory = await _saveDirectoryMethods.GetSavedDirectory();
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