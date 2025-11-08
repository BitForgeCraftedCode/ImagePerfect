using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using ImagePerfect.Helpers;
using ImagePerfect.Models;
using ImagePerfect.Repository;
using ImagePerfect.Repository.IRepository;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace ImagePerfect.ViewModels
{
    //see InitializeViewModel as that class initally sets up the SavedDirectory
    public class SavedDirectoryViewModel : ViewModelBase
	{
        private readonly MySqlDataSource _dataSource;
        private readonly IConfiguration _configuration;
        private readonly MainWindowViewModel _mainWindowViewModel;

        private string _savedDirectory = string.Empty;
        private int _savedFolderPage = 1;
        private int _savedTotalFolderPages = 1;
        private int _savedImagePage = 1;
        private int _savedTotalImagePages = 1;
        //used to save scrollviewer offset
        private Vector _savedOffsetVector = new Vector();
        private bool _isSavedDirectoryLoaded = false;
        private bool _loadSavedDirectoryFromCache = true;

        //saved filter variables
        public ExplorerViewModel.Filters savedCurrentFilter = ExplorerViewModel.Filters.None;
        public string savedSelectedLetterForFilter = "A";
        public int savedSelectedRatingForFilter = 0;
        public int savedSelectedYearForFilter = 0;
        public int savedSelectedMonthForFilter = 0;
        public DateTimeOffset savedStartDateForFilter;
        public DateTimeOffset savedEndDateForFilter;
        public string savedTagForFilter = string.Empty;
        public string savedTextForFilter = string.Empty;
        public int savedComboFolderFilterRating = 10;
        public string savedComboFolderFilterTagOne = string.Empty;
        public string savedComboFolderFilterTagTwo = string.Empty;
        public bool savedFilterInCurrentDirectory = true;
        public bool savedLoadFoldersAscending = true;


        public SavedDirectoryViewModel(MySqlDataSource dataSource, IConfiguration config, MainWindowViewModel mainWindowViewModel)
        {
            _dataSource = dataSource;
            _configuration = config;
            _mainWindowViewModel = mainWindowViewModel;
        }

        public List<FolderViewModel> SavedDirectoryFolders { get; } = new(); //runtime-only cache
        public List<ImageViewModel> SavedDirectoryImages { get; } = new(); //runtime-only cache

        public bool LoadSavedDirectoryFromCache
        {
            get => _loadSavedDirectoryFromCache;
            set => this.RaiseAndSetIfChanged(ref _loadSavedDirectoryFromCache, value);
        }
        public string SavedDirectory
        {
            get => _savedDirectory;
            set => _savedDirectory = value;
        }
        public int SavedFolderPage
        {
            get => _savedFolderPage;
            set => _savedFolderPage = value;
        }
        public int SavedTotalFolderPages
        {
            get => _savedTotalFolderPages;
            set => _savedTotalFolderPages = value;
        }
        public int SavedImagePage
        {
            get => _savedImagePage;
            set => _savedImagePage = value;
        }
        public int SavedTotalImagePages
        {
            get => _savedTotalImagePages;
            set => _savedTotalImagePages = value;
        }
        public Vector SavedOffsetVector
        {
            get => _savedOffsetVector;
            set => _savedOffsetVector = value;
        }
        public bool IsSavedDirectoryLoaded
        {
            get => _isSavedDirectoryLoaded;
            set => _isSavedDirectoryLoaded = value;
        }

        public async Task SaveDirectory(ScrollViewer scrollViewer)
        {
            _mainWindowViewModel.ShowLoading = true;
            await using UnitOfWork uow = await UnitOfWork.CreateAsync(_dataSource, _configuration);
            SaveDirectoryMethods saveDirectoryMethods = new SaveDirectoryMethods(uow);
            //update variables
            IsSavedDirectoryLoaded = true;
            SavedDirectory = _mainWindowViewModel.ExplorerVm.CurrentDirectory;
            SavedFolderPage = _mainWindowViewModel.ExplorerVm.CurrentFolderPage;
            SavedTotalFolderPages = _mainWindowViewModel.ExplorerVm.TotalFolderPages;
            SavedImagePage = _mainWindowViewModel.ExplorerVm.CurrentImagePage;
            SavedTotalImagePages = _mainWindowViewModel.ExplorerVm.TotalImagePages;
            double XVector = scrollViewer.Offset.X;
            double YVector = scrollViewer.Offset.Y;
            SavedOffsetVector = new Vector(XVector, YVector);

            //update filter variables -- filter vars are NOT presisted to db
            savedCurrentFilter = _mainWindowViewModel.ExplorerVm.currentFilter;
            savedSelectedLetterForFilter = _mainWindowViewModel.ExplorerVm.selectedLetterForFilter;
            savedSelectedRatingForFilter = _mainWindowViewModel.ExplorerVm.selectedRatingForFilter;
            savedSelectedYearForFilter = _mainWindowViewModel.ExplorerVm.selectedYearForFilter;
            savedSelectedMonthForFilter = _mainWindowViewModel.ExplorerVm.selectedMonthForFilter;
            savedStartDateForFilter = _mainWindowViewModel.ExplorerVm.startDateForFilter;
            savedEndDateForFilter = _mainWindowViewModel.ExplorerVm.endDateForFilter;
            savedTagForFilter = _mainWindowViewModel.ExplorerVm.tagForFilter;
            savedTextForFilter = _mainWindowViewModel.ExplorerVm.textForFilter;
            savedComboFolderFilterRating = _mainWindowViewModel.ExplorerVm.ComboFolderFilterRating;
            savedComboFolderFilterTagOne = _mainWindowViewModel.ExplorerVm.ComboFolderFilterTagOne;
            savedComboFolderFilterTagTwo = _mainWindowViewModel.ExplorerVm.ComboFolderFilterTagTwo;
            savedFilterInCurrentDirectory = _mainWindowViewModel.ExplorerVm.FilterInCurrentDirectory;
            savedLoadFoldersAscending = _mainWindowViewModel.ExplorerVm.LoadFoldersAscending;


            //persist to database
            SaveDirectory saveDirectory = new()
            {
                SavedDirectoryId = 1,
                SavedDirectory = _mainWindowViewModel.ExplorerVm.CurrentDirectory,
                SavedFolderPage = _mainWindowViewModel.ExplorerVm.CurrentFolderPage,
                SavedTotalFolderPages = _mainWindowViewModel.ExplorerVm.TotalFolderPages,
                SavedImagePage = _mainWindowViewModel.ExplorerVm.CurrentImagePage,
                SavedTotalImagePages = _mainWindowViewModel.ExplorerVm.TotalImagePages,
                XVector = scrollViewer.Offset.X,
                YVector = scrollViewer.Offset.Y
            };
            await saveDirectoryMethods.UpdateSaveDirectory(saveDirectory);

            await SetSavedDirectoryCache();
            _mainWindowViewModel.ShowLoading = false;
        }

        private async Task SetSavedDirectoryCache()
        {
            // update runtime cache
            SavedDirectoryFolders.Clear();
            SavedDirectoryImages.Clear();

            //Because the image bitmaps are disposed we have to make a deep copy
            FolderViewModel[] folderVmArray = new FolderViewModel[_mainWindowViewModel.LibraryFolders.Count];
            await Parallel.ForEachAsync(
                Enumerable.Range(0, _mainWindowViewModel.LibraryFolders.Count),
                new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                async (i, ct) => 
                {
                    folderVmArray[i] = await DeepCopy.CopyFolderVm(_mainWindowViewModel.LibraryFolders[i]);
                });
            SavedDirectoryFolders.AddRange(folderVmArray);
            
            ImageViewModel[] imageVmArray = new ImageViewModel[_mainWindowViewModel.Images.Count];
            await Parallel.ForEachAsync(
                Enumerable.Range(0, _mainWindowViewModel.Images.Count),
                new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                async (i, ct) => 
                {
                    imageVmArray[i] = await DeepCopy.CopyImageVm(_mainWindowViewModel.Images[i]);
                });
            SavedDirectoryImages.AddRange(imageVmArray);
        }
        public async Task UpdateSavedDirectoryCache()
        {
            _mainWindowViewModel.ShowLoading = true;
            await SetSavedDirectoryCache();
            IsSavedDirectoryLoaded = false;
            _mainWindowViewModel.ShowLoading = false;
        }
        public async Task LoadSavedDirectory(ScrollViewer scrollViewer)
        {
            // ensure we don't tell other code the saved-dir is "loaded" until we've restored it
            // otherwise RefreshFolders and or RefreshImages will call UpdateSavedDirectoryCache and write the wrong directory to Cache
            IsSavedDirectoryLoaded = false;
            _mainWindowViewModel.ExplorerVm.CurrentDirectory = SavedDirectory;
            _mainWindowViewModel.ExplorerVm.CurrentFolderPage = SavedFolderPage;
            _mainWindowViewModel.ExplorerVm.TotalFolderPages = SavedTotalFolderPages;
            _mainWindowViewModel.ExplorerVm.CurrentImagePage = SavedImagePage;
            _mainWindowViewModel.ExplorerVm.TotalImagePages = SavedTotalImagePages;
            _mainWindowViewModel.ExplorerVm.MaxPage = Math.Max(_mainWindowViewModel.ExplorerVm.TotalImagePages, _mainWindowViewModel.ExplorerVm.TotalFolderPages);
            _mainWindowViewModel.ExplorerVm.MaxCurrentPage = Math.Max(_mainWindowViewModel.ExplorerVm.CurrentImagePage, _mainWindowViewModel.ExplorerVm.CurrentFolderPage);

            //filter variables
            _mainWindowViewModel.ExplorerVm.currentFilter = savedCurrentFilter;
            _mainWindowViewModel.ExplorerVm.selectedLetterForFilter = savedSelectedLetterForFilter;
            _mainWindowViewModel.ExplorerVm.selectedRatingForFilter = savedSelectedRatingForFilter;
            _mainWindowViewModel.ExplorerVm.selectedYearForFilter = savedSelectedYearForFilter;
            _mainWindowViewModel.ExplorerVm.selectedMonthForFilter = savedSelectedMonthForFilter;
            _mainWindowViewModel.ExplorerVm.startDateForFilter = savedStartDateForFilter;
            _mainWindowViewModel.ExplorerVm.endDateForFilter = savedEndDateForFilter;
            _mainWindowViewModel.ExplorerVm.tagForFilter = savedTagForFilter;
            _mainWindowViewModel.ExplorerVm.textForFilter = savedTextForFilter;
            _mainWindowViewModel.ExplorerVm.ComboFolderFilterRating = savedComboFolderFilterRating;
            _mainWindowViewModel.ExplorerVm.ComboFolderFilterTagOne = savedComboFolderFilterTagOne;
            _mainWindowViewModel.ExplorerVm.ComboFolderFilterTagTwo = savedComboFolderFilterTagTwo;
            _mainWindowViewModel.ExplorerVm.FilterInCurrentDirectory = savedFilterInCurrentDirectory;
            _mainWindowViewModel.ExplorerVm.LoadFoldersAscending = savedLoadFoldersAscending;

            if ((SavedDirectoryFolders.Count > 0 || SavedDirectoryImages.Count > 0) && LoadSavedDirectoryFromCache == true)
            {
                //fast path: restore from cache
                List<FolderViewModel> oldFolders = _mainWindowViewModel.LibraryFolders.ToList();
                _mainWindowViewModel.LibraryFolders = new ObservableCollection<FolderViewModel>();
                await Task.Run(() =>
                {
                    foreach (FolderViewModel folder in oldFolders)
                    {
                        // Prevent NullReferenceExceptions when reloading the same saved directory.
                        // On a second load, oldFolders may reference the same objects in SavedDirectoryFolders.
                        // Only dispose bitmaps that are not part of the saved cache.
                        if (!SavedDirectoryFolders.Contains(folder))
                            folder.CoverImageBitmap?.Dispose();
                    }  
                });
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    _mainWindowViewModel.LibraryFolders = new ObservableCollection<FolderViewModel>(SavedDirectoryFolders);
                });

                List<ImageViewModel> oldImages = _mainWindowViewModel.Images.ToList();
                _mainWindowViewModel.Images = new ObservableCollection<ImageViewModel>();
                await Task.Run(() =>
                {
                    foreach (ImageViewModel img in oldImages) 
                    { 
                        //see above folder comment same applies here
                        if(!SavedDirectoryImages.Contains(img))
                            img.ImageBitmap?.Dispose();
                    }
                });
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    _mainWindowViewModel.Images = new ObservableCollection<ImageViewModel>(SavedDirectoryImages);
                });
               
                // now that we've restored from cache, mark saved-dir as loaded
                IsSavedDirectoryLoaded = true;

                // defer scroll until after layout
                Dispatcher.UIThread.Post(() =>
                {
                    scrollViewer.Offset = SavedOffsetVector;
                }, DispatcherPriority.Background);
            }
            else
            {
                // slow path: full rebuild
                await _mainWindowViewModel.DirectoryNavigationVm.ReLoadSavedDirectory(SavedDirectory);
                // populate the runtime cache now that the UI is showing the saved directory
                await SetSavedDirectoryCache();
                // now mark saved-dir as loaded so refreshes can update cache later
                IsSavedDirectoryLoaded = true;
                scrollViewer.Offset = SavedOffsetVector;
            }
        }
    }
}