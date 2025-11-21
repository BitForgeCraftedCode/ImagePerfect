using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using ImagePerfect.Helpers;
using ImagePerfect.Models;
using ImagePerfect.Repository;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;

namespace ImagePerfect.ViewModels
{
	public class HistoryViewModel : ViewModelBase
    {
        private readonly MySqlDataSource _dataSource;
        private readonly IConfiguration _configuration;
        private readonly MainWindowViewModel _mainWindowViewModel;

        private SaveDirectory _activeSavedDirectory;
        private bool _isSavedHistoryDirectoryLoaded = false;
        private bool _loadSavedHistoryDirectoryFromCache = true;

        public HistoryViewModel(MySqlDataSource dataSource, IConfiguration config, MainWindowViewModel mainWindowViewModel)
		{
            _dataSource = dataSource;
            _configuration = config;
            _mainWindowViewModel = mainWindowViewModel;
		}
        public Interaction<SaveDirectory, Unit> LoadHistoryRequest { get; } = new();

        public ObservableCollection<SaveDirectory> SaveDirectoryItemsList { get; set; } = new();

        private SaveDirectory? _selectedSaveDirectoryItem;

        public SaveDirectory? SelectedSaveDirectoryItem
        {
            get => _selectedSaveDirectoryItem;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedSaveDirectoryItem, value);
                if (value != null)
                {
                    // Load automatically when user selects it
                    LoadHistoryRequest.Handle(value).Subscribe();
                }
            }
        }

        public bool LoadSavedHistoryDirectoryFromCache
        {
            get => _loadSavedHistoryDirectoryFromCache;
            set => this.RaiseAndSetIfChanged(ref _loadSavedHistoryDirectoryFromCache, value);
        }

        public bool IsSavedHistoryDirectoryLoaded
        {
            get => _isSavedHistoryDirectoryLoaded;
            set => _isSavedHistoryDirectoryLoaded = value;
        }

        public async Task SaveDirectoryToHistory(ScrollViewer scrollViewer, bool isMainSavedDirectory)
		{
            _mainWindowViewModel.ShowLoading = true;
            await using UnitOfWork uow = await UnitOfWork.CreateAsync(_dataSource, _configuration);
            SaveDirectoryMethods saveDirectoryMethods = new SaveDirectoryMethods(uow);

            IsSavedHistoryDirectoryLoaded = true;
            double XVector = scrollViewer.Offset.X;
            double YVector = scrollViewer.Offset.Y;
            SaveDirectory saveDirectoryItem = new SaveDirectory
			{
                DisplayName = PathHelper.GetFolderNameFromFolderPath(_mainWindowViewModel.ExplorerVm.CurrentDirectory),
                //update variables
                SavedDirectory = _mainWindowViewModel.ExplorerVm.CurrentDirectory,
                SavedFolderPage = _mainWindowViewModel.ExplorerVm.CurrentFolderPage,
                SavedTotalFolderPages = _mainWindowViewModel.ExplorerVm.TotalFolderPages,
                SavedImagePage = _mainWindowViewModel.ExplorerVm.CurrentImagePage,
                SavedTotalImagePages = _mainWindowViewModel.ExplorerVm.TotalImagePages,
                XVector = XVector,
                YVector = YVector,
                SavedOffsetVector = new Vector(XVector, YVector),
                //update filter variables
                SavedCurrentFilter = _mainWindowViewModel.ExplorerVm.currentFilter,
                SavedSelectedLetterForFilter = _mainWindowViewModel.ExplorerVm.selectedLetterForFilter,
                SavedSelectedRatingForFilter = _mainWindowViewModel.ExplorerVm.selectedRatingForFilter,
                SavedSelectedYearForFilter = _mainWindowViewModel.ExplorerVm.selectedYearForFilter,
                SavedSelectedMonthForFilter = _mainWindowViewModel.ExplorerVm.selectedMonthForFilter,
                SavedStartDateForFilter = _mainWindowViewModel.ExplorerVm.startDateForFilter,
                SavedEndDateForFilter = _mainWindowViewModel.ExplorerVm.endDateForFilter,
                SavedTagForFilter = _mainWindowViewModel.ExplorerVm.tagForFilter,
                SavedTextForFilter = _mainWindowViewModel.ExplorerVm.textForFilter,
                SavedComboFolderFilterRating = _mainWindowViewModel.ExplorerVm.ComboFolderFilterRating,
                SavedComboFolderFilterTagOne = _mainWindowViewModel.ExplorerVm.ComboFolderFilterTagOne,
                SavedComboFolderFilterTagTwo = _mainWindowViewModel.ExplorerVm.ComboFolderFilterTagTwo,
                SavedFilterInCurrentDirectory = _mainWindowViewModel.ExplorerVm.FilterInCurrentDirectory,
                SavedLoadFoldersAscending = _mainWindowViewModel.ExplorerVm.LoadFoldersAscending
            };
            await SetSavedDirectoryCache(saveDirectoryItem);
            // Check for existing entry with same SavedDirectory path
            var existingIndex = SaveDirectoryItemsList
                .Select((item, idx) => new { item, idx })
                .FirstOrDefault(x =>
                    string.Equals(x.item.SavedDirectory, saveDirectoryItem.SavedDirectory, StringComparison.OrdinalIgnoreCase)
                );
            if( existingIndex != null && isMainSavedDirectory == false)
            {
                SaveDirectoryItemsList[existingIndex.idx] = saveDirectoryItem;
            }
            else
            {
                if (isMainSavedDirectory)
                {
                    //persist to database
                    await saveDirectoryMethods.UpdateSaveDirectory(saveDirectoryItem);
                    SaveDirectoryItemsList[0] = saveDirectoryItem;
                }
                else
                {
                    SaveDirectoryItemsList.Add(saveDirectoryItem);
                }
            }
            
            _activeSavedDirectory = saveDirectoryItem;
            //pruning the oldeset (exclude main saved) if more than 20 -- need to manage RAM
            if (SaveDirectoryItemsList.Count > _mainWindowViewModel.SettingsVm.HistoryPointsSize)
            {
                await DisposeSessionHistoryItemBitmaps(SaveDirectoryItemsList[1]);
                SaveDirectoryItemsList.RemoveAt(1);
            }
            _mainWindowViewModel.ShowLoading = false;
        }
        //not sure about this. Dispose in SetSavedDirectoryCache causes Null Ref error
        //because it will try to dispose of a bitmap currently in the UI
        //This idea was to dispose later after UI removes objects
        private void DisposeLater(IDisposable? bitmap)
        {
            if (bitmap == null)
                return;
            DispatcherTimer timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                bitmap.Dispose();
            };
            timer.Start();
        }
        private async Task SetSavedDirectoryCache(SaveDirectory saveDirectoryItem)
        {
            // --- FOLDERS ---
            ObservableCollection<FolderViewModel> sourceFolders = _mainWindowViewModel.LibraryFolders;
            List<FolderViewModel> targetFolders = saveDirectoryItem.SavedDirectoryFolders;

            // grow or shrink list to match source count
            while (targetFolders.Count < sourceFolders.Count)
                targetFolders.Add(new FolderViewModel());
            while (targetFolders.Count > sourceFolders.Count)
                targetFolders.RemoveAt(targetFolders.Count - 1);

            await Parallel.ForEachAsync(
                Enumerable.Range(0, sourceFolders.Count),
                new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                async (i, ct) =>
                {
                    FolderViewModel deepCopy = await DeepCopy.CopyFolderVm(sourceFolders[i]);
                    //DisposeLater(targetFolders[i].CoverImageBitmap);
                    // Overwrite everything relevant
                    targetFolders[i] = deepCopy;
                });

            // --- IMAGES ---
            ObservableCollection<ImageViewModel> sourceImages = _mainWindowViewModel.Images;
            List<ImageViewModel> targetImages = saveDirectoryItem.SavedDirectoryImages;

            while (targetImages.Count < sourceImages.Count)
                targetImages.Add(new ImageViewModel());
            while (targetImages.Count > sourceImages.Count)
                targetImages.RemoveAt(targetImages.Count - 1);

            await Parallel.ForEachAsync(
                Enumerable.Range(0, sourceImages.Count),
                new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                async (i, ct) =>
                {
                    ImageViewModel deepCopy = await DeepCopy.CopyImageVm(sourceImages[i]);
                    //DisposeLater(targetImages[i].ImageBitmap);
                    // Overwrite everything relevant
                    targetImages[i] = deepCopy;
                });
        }

        public async Task UpdateSavedHistoryDirectoryCache()
        {
            _mainWindowViewModel.ShowLoading = true;
            await SetSavedDirectoryCache(_activeSavedDirectory);
            IsSavedHistoryDirectoryLoaded = false;
            _mainWindowViewModel.ShowLoading = false;
        }

        public async Task LoadMainSavedDirectory(ScrollViewer scrollViewer)
        {
            SaveDirectory saveDirectoryItem = SaveDirectoryItemsList.First();
            await LoadSavedDirectory(saveDirectoryItem, scrollViewer);
        }
        public async Task LoadSavedDirectoryHistoryItem(SaveDirectory saveDirectoryItem, ScrollViewer scrollViewer)
        {
            await LoadSavedDirectory(saveDirectoryItem, scrollViewer);
        }

        private async Task LoadSavedDirectory(SaveDirectory saveDirectoryItem, ScrollViewer scrollViewer)
        {
            _activeSavedDirectory = saveDirectoryItem;
            // ensure we don't tell other code the saved-dir is "loaded" until we've restored it
            // otherwise RefreshFolders and or RefreshImages will call UpdateSavedHistoryDirectoryCache and write the wrong directory to Cache
            IsSavedHistoryDirectoryLoaded = false;
            //update all variables
            _mainWindowViewModel.ExplorerVm.CurrentDirectory = saveDirectoryItem.SavedDirectory;
            _mainWindowViewModel.ExplorerVm.CurrentFolderPage = saveDirectoryItem.SavedFolderPage;
            _mainWindowViewModel.ExplorerVm.TotalFolderPages = saveDirectoryItem.SavedTotalFolderPages;
            _mainWindowViewModel.ExplorerVm.CurrentImagePage = saveDirectoryItem.SavedImagePage;
            _mainWindowViewModel.ExplorerVm.TotalImagePages = saveDirectoryItem.SavedTotalImagePages;
            _mainWindowViewModel.ExplorerVm.MaxPage = Math.Max(_mainWindowViewModel.ExplorerVm.TotalImagePages, _mainWindowViewModel.ExplorerVm.TotalFolderPages);
            _mainWindowViewModel.ExplorerVm.MaxCurrentPage = Math.Max(_mainWindowViewModel.ExplorerVm.CurrentImagePage, _mainWindowViewModel.ExplorerVm.CurrentFolderPage);

            //filter variables
            _mainWindowViewModel.ExplorerVm.currentFilter = saveDirectoryItem.SavedCurrentFilter;
            _mainWindowViewModel.ExplorerVm.selectedLetterForFilter = saveDirectoryItem.SavedSelectedLetterForFilter;
            _mainWindowViewModel.ExplorerVm.selectedRatingForFilter = saveDirectoryItem.SavedSelectedRatingForFilter;
            _mainWindowViewModel.ExplorerVm.selectedYearForFilter = saveDirectoryItem.SavedSelectedYearForFilter;
            _mainWindowViewModel.ExplorerVm.selectedMonthForFilter = saveDirectoryItem.SavedSelectedMonthForFilter;
            _mainWindowViewModel.ExplorerVm.startDateForFilter = saveDirectoryItem.SavedStartDateForFilter;
            _mainWindowViewModel.ExplorerVm.endDateForFilter = saveDirectoryItem.SavedEndDateForFilter;
            _mainWindowViewModel.ExplorerVm.tagForFilter = saveDirectoryItem.SavedTagForFilter;
            _mainWindowViewModel.ExplorerVm.textForFilter = saveDirectoryItem.SavedTextForFilter;
            _mainWindowViewModel.ExplorerVm.ComboFolderFilterRating = saveDirectoryItem.SavedComboFolderFilterRating;
            _mainWindowViewModel.ExplorerVm.ComboFolderFilterTagOne = saveDirectoryItem.SavedComboFolderFilterTagOne;
            _mainWindowViewModel.ExplorerVm.ComboFolderFilterTagTwo = saveDirectoryItem.SavedComboFolderFilterTagTwo;
            _mainWindowViewModel.ExplorerVm.FilterInCurrentDirectory = saveDirectoryItem.SavedFilterInCurrentDirectory;
            _mainWindowViewModel.ExplorerVm.LoadFoldersAscending = saveDirectoryItem.SavedLoadFoldersAscending;

            if ((saveDirectoryItem.SavedDirectoryFolders.Count > 0 || saveDirectoryItem.SavedDirectoryImages.Count > 0) && LoadSavedHistoryDirectoryFromCache == true)
            {
                //fast path: restore from cache
                List<FolderViewModel> oldFolders = _mainWindowViewModel.LibraryFolders.ToList();
                _mainWindowViewModel.LibraryFolders = new ObservableCollection<FolderViewModel>();
                await Task.Run(() =>
                {
                    //only dispose of bitmaps that are not in SessionHistory
                    foreach (FolderViewModel folder in oldFolders)
                    {
                        if (!_mainWindowViewModel.ExplorerVm.IsInSessionHistory(folder))
                        {
                            folder.CoverImageBitmap?.Dispose();
                        }
                    }
                });
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    _mainWindowViewModel.LibraryFolders = new ObservableCollection<FolderViewModel>(saveDirectoryItem.SavedDirectoryFolders);
                });

                List<ImageViewModel> oldImages = _mainWindowViewModel.Images.ToList();
                _mainWindowViewModel.Images = new ObservableCollection<ImageViewModel>();
                await Task.Run(() =>
                {
                    //only dispose of image bitmaps that are not in SessionHistory
                    foreach (ImageViewModel img in oldImages)
                    {
                        if (!_mainWindowViewModel.ExplorerVm.IsInSessionHistory(img))
                        {
                            img.ImageBitmap?.Dispose();
                        }
                    }
                });
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    _mainWindowViewModel.Images = new ObservableCollection<ImageViewModel>(saveDirectoryItem.SavedDirectoryImages);
                });

                // now that we've restored from cache, mark saved-dir as loaded
                IsSavedHistoryDirectoryLoaded = true;

                // defer scroll until after layout
                Dispatcher.UIThread.Post(() =>
                {
                    scrollViewer.Offset = saveDirectoryItem.SavedOffsetVector;
                }, DispatcherPriority.Background);
            }
            else
            {
                // slow path: full rebuild
                await _mainWindowViewModel.DirectoryNavigationVm.ReLoadSavedDirectory(saveDirectoryItem.SavedDirectory);
                // populate the runtime cache now that the UI is showing the saved directory
                await SetSavedDirectoryCache(saveDirectoryItem);
                // now mark saved-dir as loaded so refreshes can update cache later
                IsSavedHistoryDirectoryLoaded = true;
                scrollViewer.Offset = saveDirectoryItem.SavedOffsetVector;
            }
        }

        private async Task DisposeSessionHistoryItemBitmaps(SaveDirectory saveDirectoryItem)
        {
            await Task.Run(() => 
            {
                foreach (FolderViewModel folderVm in saveDirectoryItem.SavedDirectoryFolders)
                    folderVm.CoverImageBitmap?.Dispose();

                foreach (ImageViewModel imageVm in saveDirectoryItem.SavedDirectoryImages)
                    imageVm.ImageBitmap?.Dispose();
            });
        }
	}
}