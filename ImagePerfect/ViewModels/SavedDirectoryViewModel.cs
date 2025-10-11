using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using ImagePerfect.Models;
using ImagePerfect.Repository.IRepository;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace ImagePerfect.ViewModels
{
    //see InitializeViewModel as that class initally sets up the SavedDirectory
    public class SavedDirectoryViewModel : ViewModelBase
	{
        private readonly IUnitOfWork _unitOfWork;
        private readonly SaveDirectoryMethods _saveDirectoryMethods;
        private readonly MainWindowViewModel _mainWindowViewModel;

        public SavedDirectoryViewModel(IUnitOfWork unitOfWork, MainWindowViewModel mainWindowViewModel)
        {
            _unitOfWork = unitOfWork;
            _mainWindowViewModel = mainWindowViewModel;
            _saveDirectoryMethods = new SaveDirectoryMethods(_unitOfWork);
        }

        public async Task SaveDirectory(ScrollViewer scrollViewer)
        {
            //update variables
            _mainWindowViewModel.IsSavedDirectoryLoaded = true;
            _mainWindowViewModel.SavedDirectory = _mainWindowViewModel.CurrentDirectory;
            _mainWindowViewModel.SavedFolderPage = _mainWindowViewModel.CurrentFolderPage;
            _mainWindowViewModel.SavedTotalFolderPages = _mainWindowViewModel.TotalFolderPages;
            _mainWindowViewModel.SavedImagePage = _mainWindowViewModel.CurrentImagePage;
            _mainWindowViewModel.SavedTotalImagePages = _mainWindowViewModel.TotalImagePages;
            double XVector = scrollViewer.Offset.X;
            double YVector = scrollViewer.Offset.Y;
            _mainWindowViewModel.SavedOffsetVector = new Vector(XVector, YVector);
            //persist to database
            SaveDirectory saveDirectory = new()
            {
                SavedDirectoryId = 1,
                SavedDirectory = _mainWindowViewModel.CurrentDirectory,
                SavedFolderPage = _mainWindowViewModel.CurrentFolderPage,
                SavedTotalFolderPages = _mainWindowViewModel.TotalFolderPages,
                SavedImagePage = _mainWindowViewModel.CurrentImagePage,
                SavedTotalImagePages = _mainWindowViewModel.TotalImagePages,
                XVector = scrollViewer.Offset.X,
                YVector = scrollViewer.Offset.Y
            };
            await _saveDirectoryMethods.UpdateSaveDirectory(saveDirectory);

            SetSavedDirectoryCache();
        }

        private void SetSavedDirectoryCache()
        {
            // update runtime cache
            _mainWindowViewModel.SavedDirectoryFolders.Clear();
            _mainWindowViewModel.SavedDirectoryImages.Clear();

            _mainWindowViewModel.SavedDirectoryFolders.AddRange(_mainWindowViewModel.LibraryFolders);
            _mainWindowViewModel.SavedDirectoryImages.AddRange(_mainWindowViewModel.Images);
        }
        public void UpdateSavedDirectoryCache()
        {
            SetSavedDirectoryCache();
            _mainWindowViewModel.IsSavedDirectoryLoaded = false;
        }
        public async Task LoadSavedDirectory(ScrollViewer scrollViewer)
        {
            // ensure we don't tell other code the saved-dir is "loaded" until we've restored it
            // otherwise RefreshFolders and or RefreshImages will call UpdateSavedDirectoryCache and write the wrong directory to Cache
            _mainWindowViewModel.IsSavedDirectoryLoaded = false;
            _mainWindowViewModel.CurrentDirectory = _mainWindowViewModel.SavedDirectory;
            _mainWindowViewModel.CurrentFolderPage = _mainWindowViewModel.SavedFolderPage;
            _mainWindowViewModel.TotalFolderPages = _mainWindowViewModel.SavedTotalFolderPages;
            _mainWindowViewModel.CurrentImagePage = _mainWindowViewModel.SavedImagePage;
            _mainWindowViewModel.TotalImagePages = _mainWindowViewModel.SavedTotalImagePages;
            _mainWindowViewModel.MaxPage = Math.Max(_mainWindowViewModel.TotalImagePages, _mainWindowViewModel.TotalFolderPages);
            _mainWindowViewModel.MaxCurrentPage = Math.Max(_mainWindowViewModel.CurrentImagePage, _mainWindowViewModel.CurrentFolderPage);
            if ((_mainWindowViewModel.SavedDirectoryFolders.Count > 0 || _mainWindowViewModel.SavedDirectoryImages.Count > 0) && _mainWindowViewModel.LoadSavedDirectoryFromCache == true)
            {
                //fast path: restore from cache
                _mainWindowViewModel.LibraryFolders.Clear();
                foreach(FolderViewModel folder in _mainWindowViewModel.SavedDirectoryFolders)
                    _mainWindowViewModel.LibraryFolders.Add(folder);

                _mainWindowViewModel.Images.Clear();
                foreach(ImageViewModel image in _mainWindowViewModel.SavedDirectoryImages)
                    _mainWindowViewModel.Images.Add(image);

                // now that we've restored from cache, mark saved-dir as loaded
                _mainWindowViewModel.IsSavedDirectoryLoaded = true;

                // defer scroll until after layout
                Dispatcher.UIThread.Post(() =>
                {
                    scrollViewer.Offset = _mainWindowViewModel.SavedOffsetVector;
                }, DispatcherPriority.Background);
            }
            else
            {
                // slow path: full rebuild
                await _mainWindowViewModel.LoadCurrentDirectory();
                // populate the runtime cache now that the UI is showing the saved directory
                SetSavedDirectoryCache();
                // now mark saved-dir as loaded so refreshes can update cache later
                _mainWindowViewModel.IsSavedDirectoryLoaded = true;
                scrollViewer.Offset = _mainWindowViewModel.SavedOffsetVector;
            }
        }
    }
}