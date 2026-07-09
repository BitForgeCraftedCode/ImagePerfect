using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using ImagePerfect.Helpers;
using ImagePerfect.Models;
using ImagePerfect.Repository;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using ReactiveUI;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Threading;
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

        private readonly ConcurrentQueue<IDisposable> DisposeQueue = new();
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

        private SaveDirectory CreateSaveDirectory(ScrollViewer scrollViewer)
        {
            ExplorerViewModel explorer = _mainWindowViewModel.ExplorerVm;
            double XVector = scrollViewer.Offset.X;
            double YVector = scrollViewer.Offset.Y;
            return new SaveDirectory 
            {
                DisplayName = PathHelper.GetHistroyDisplayNameFromPath(explorer.CurrentDirectory),
                //update variables
                SavedDirectory = explorer.CurrentDirectory,
                SavedFolderPage = explorer.CurrentFolderPage,
                SavedTotalFolderPages = explorer.TotalFolderPages,
                SavedImagePage = explorer.CurrentImagePage,
                SavedTotalImagePages = explorer.TotalImagePages,
                XVector = XVector,
                YVector = YVector,
                SavedOffsetVector = new Vector(XVector, YVector),
                //update filter variables
                SavedCurrentFilter = explorer.currentFilter,
                SavedSelectedLetterForFilter = explorer.selectedLetterForFilter,
                SavedSelectedRatingForFilter = explorer.selectedRatingForFilter,
                SavedSelectedYearForFilter = explorer.selectedYearForFilter,
                SavedSelectedMonthForFilter = explorer.selectedMonthForFilter,
                SavedStartDateForFilter = explorer.startDateForFilter,
                SavedEndDateForFilter = explorer.endDateForFilter,
                SavedTextForFilter = explorer.textForFilter,
                SavedComboFolderFilterRating = explorer.ComboFolderFilterRating,
                SavedTagsForFolderDescriptionAndTagsFilter = explorer.tagsForFolderDescriptionAndTagsFilter,
                SavedTextForFolderDescriptionAndTagsFilter = explorer.TextForFolderDescriptionAndTagsFilter,
                SavedTagsForFilter = explorer.tagsForFilter,
                SavedTagsForFolderFilter = explorer.tagsForFolderFilter,
                SavedTagsForImageFilter = explorer.tagsForImageFilter,
                SavedFilterInCurrentDirectory = explorer.FilterInCurrentDirectory,
                SavedLoadFoldersAscending = explorer.LoadFoldersAscending
            };
        }
        public async Task SaveDirectoryToHistory(ScrollViewer scrollViewer, bool isMainSavedDirectory)
		{
            _mainWindowViewModel.ShowLoading = true;
            await using UnitOfWork uow = await UnitOfWork.CreateAsync(_dataSource, _configuration);
            SaveDirectoryMethods saveDirectoryMethods = new SaveDirectoryMethods(uow);

            IsSavedHistoryDirectoryLoaded = true;
            SaveDirectory saveDirectoryItem = CreateSaveDirectory(scrollViewer);
            // Check for existing entry with same SavedDirectory path
            var existingIndex = SaveDirectoryItemsList
                .Select((item, idx) => new { item, idx })
                .FirstOrDefault(x =>
                    string.Equals(x.item.SavedDirectory, saveDirectoryItem.SavedDirectory, StringComparison.OrdinalIgnoreCase)
                );
            /*
             * If the same directory is saved multiple times, preserve the existing folders and images 
             * by adding them to the new SaveDirectoryItem. 
             * This ensures bitmaps are properly disposed and refreshed in SetSavedDirectoryCache.
             * 
             * Note: saveDirectoryItem is newly created above, so its folder/image lists are initially empty.
             */
            if ( existingIndex != null && isMainSavedDirectory == false)
            {
                saveDirectoryItem.SavedDirectoryFolders.AddRange(SaveDirectoryItemsList[existingIndex.idx].SavedDirectoryFolders);
                saveDirectoryItem.SavedDirectoryImages.AddRange(SaveDirectoryItemsList[existingIndex.idx].SavedDirectoryImages);
                await SetSavedDirectoryCache(saveDirectoryItem);
                SaveDirectoryItemsList[existingIndex.idx] = saveDirectoryItem;
            }
            else
            {
                if (existingIndex != null && isMainSavedDirectory == true)
                {
                    saveDirectoryItem.SavedDirectoryFolders.AddRange(SaveDirectoryItemsList[existingIndex.idx].SavedDirectoryFolders);
                    saveDirectoryItem.SavedDirectoryImages.AddRange(SaveDirectoryItemsList[existingIndex.idx].SavedDirectoryImages);
                    await SetSavedDirectoryCache(saveDirectoryItem);
                    //persist to database
                    await saveDirectoryMethods.UpdateSaveDirectory(saveDirectoryItem);
                    SaveDirectoryItemsList[existingIndex.idx] = saveDirectoryItem;
                }
                else if (existingIndex == null && isMainSavedDirectory == true)
                {
                    await SetSavedDirectoryCache(saveDirectoryItem);
                    //persist to database
                    await saveDirectoryMethods.UpdateSaveDirectory(saveDirectoryItem);
                    SaveDirectoryItemsList[0] = saveDirectoryItem;
                }
                else
                {
                    await SetSavedDirectoryCache(saveDirectoryItem);
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
        // Disposing bitmaps directly in SetSavedDirectoryCache can cause NullReferenceExceptions,
        // because some bitmaps may still be bound to the UI. 
        // Instead, defer disposal until after the UI has removed references.
        // logging errors for background clean up methods like this -- dont throw let if fail silently but log the errors
        private int _disposeRunning = 0;
        private async Task DisposeDeferredBitmaps()
        {
            //Prevent overlapping timer runs
            //If disposal ever takes longer than the interval
            if (Interlocked.Exchange(ref _disposeRunning, 1) == 1)
                return;
            int disposedCount = 0;
            int errorCount = 0;
            try
            {
                await Task.Run(() =>
                {
                    while (DisposeQueue.TryDequeue(out var bmp))
                    {
                        try
                        {
                            bmp?.Dispose();
                            disposedCount++;
                        }
                        catch
                        {
                            errorCount++;
                        }
                    }
                });
            }
            catch (Exception ex) 
            {
                // This should be extremely rare (Task infrastructure failure)
                Log.Error(ex, "Deferred bitmap disposal task failed catastrophically");
                return;
            }
            finally
            {
                Interlocked.Exchange(ref _disposeRunning, 0);
            }
            if (errorCount > 0)
            {
                Log.Warning(
                    "Deferred bitmap disposal completed with errors. " +
                    "Disposed={DisposedCount}, Errors={ErrorCount}, RemainingInQueue={QueueCount}",
                    disposedCount,
                    errorCount,
                    DisposeQueue.Count);
            }
            else if (disposedCount > 0)
            {
                Log.Debug(
                    "Deferred bitmap disposal completed. Disposed={DisposedCount}",
                    disposedCount);
            }
        }
        
        private async Task SetSavedDirectoryCache(SaveDirectory saveDirectoryItem)
        {
            // --- FOLDERS ---
            ObservableCollection<FolderViewModel> sourceFolders = _mainWindowViewModel.LibraryFolders;
            FolderViewModel[] newFolders = new FolderViewModel[sourceFolders.Count];

            await Parallel.ForEachAsync(
                Enumerable.Range(0, sourceFolders.Count),
                new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                async (i, ct) =>
                {
                    newFolders[i] = await DeepCopy.CopyFolderVm(sourceFolders[i]);
                });
            //add folder CoverImageBitmap to DisposeQueue
            //SavedDirectoryFolders and Images will be > 0 when calling this method from UpdateSavedHistoryDirectoryCache or SaveDirectoryToHistory
            //when saving same dir twice or more
            if (saveDirectoryItem.SavedDirectoryFolders.Count > 0) 
            {
                Parallel.ForEach(
                    saveDirectoryItem.SavedDirectoryFolders,
                    new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                    (folderVm) => {
                        if (folderVm.CoverImageBitmap != null)
                            DisposeQueue.Enqueue(folderVm.CoverImageBitmap);
                    });
            }
            saveDirectoryItem.SavedDirectoryFolders.Clear();
            saveDirectoryItem.SavedDirectoryFolders.AddRange(newFolders);
            // --- IMAGES ---
            ObservableCollection<ImageViewModel> sourceImages = _mainWindowViewModel.Images;
            ImageViewModel[] newImages = new ImageViewModel[sourceImages.Count];

            await Parallel.ForEachAsync(
                Enumerable.Range(0, sourceImages.Count),
                new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                async (i, ct) =>
                {
                    newImages[i] = await DeepCopy.CopyImageVm(sourceImages[i]);
                });
            //add image bitmaps to DisposeQueue
            if (saveDirectoryItem.SavedDirectoryImages.Count > 0) 
            {
                Parallel.ForEach(
                    saveDirectoryItem.SavedDirectoryImages,
                    new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                    (imageVm) => {
                        if (imageVm.ImageBitmap != null)
                            DisposeQueue.Enqueue(imageVm.ImageBitmap);
                    });
            }
            saveDirectoryItem.SavedDirectoryImages.Clear();
            saveDirectoryItem.SavedDirectoryImages.AddRange(newImages);
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

        private void RestoreExplorerState(SaveDirectory saveDirectoryItem)
        {
            ExplorerViewModel explorer = _mainWindowViewModel.ExplorerVm;
            //update all variables
            explorer.CurrentDirectory = saveDirectoryItem.SavedDirectory;
            explorer.CurrentFolderPage = saveDirectoryItem.SavedFolderPage;
            explorer.TotalFolderPages = saveDirectoryItem.SavedTotalFolderPages;
            explorer.CurrentImagePage = saveDirectoryItem.SavedImagePage;
            explorer.TotalImagePages = saveDirectoryItem.SavedTotalImagePages;
            explorer.MaxPage = Math.Max(explorer.TotalImagePages, explorer.TotalFolderPages);
            explorer.MaxCurrentPage = Math.Max(explorer.CurrentImagePage, explorer.CurrentFolderPage);

            //filter variables
            explorer.currentFilter = saveDirectoryItem.SavedCurrentFilter;
            explorer.selectedLetterForFilter = saveDirectoryItem.SavedSelectedLetterForFilter;
            explorer.selectedRatingForFilter = saveDirectoryItem.SavedSelectedRatingForFilter;
            explorer.selectedYearForFilter = saveDirectoryItem.SavedSelectedYearForFilter;
            explorer.selectedMonthForFilter = saveDirectoryItem.SavedSelectedMonthForFilter;
            explorer.startDateForFilter = saveDirectoryItem.SavedStartDateForFilter;
            explorer.endDateForFilter = saveDirectoryItem.SavedEndDateForFilter;
            explorer.textForFilter = saveDirectoryItem.SavedTextForFilter;
            explorer.ComboFolderFilterRating = saveDirectoryItem.SavedComboFolderFilterRating;
            explorer.tagsForFolderDescriptionAndTagsFilter = saveDirectoryItem.SavedTagsForFolderDescriptionAndTagsFilter;
            explorer.TextForFolderDescriptionAndTagsFilter = saveDirectoryItem.SavedTextForFolderDescriptionAndTagsFilter;
            explorer.tagsForFilter = saveDirectoryItem.SavedTagsForFilter;
            explorer.tagsForFolderFilter = saveDirectoryItem.SavedTagsForFolderFilter;
            explorer.tagsForImageFilter = saveDirectoryItem.SavedTagsForImageFilter;
            explorer.FilterInCurrentDirectory = saveDirectoryItem.SavedFilterInCurrentDirectory;
            explorer.LoadFoldersAscending = saveDirectoryItem.SavedLoadFoldersAscending;
        }
        private async Task LoadSavedDirectory(SaveDirectory saveDirectoryItem, ScrollViewer scrollViewer)
        {
            _activeSavedDirectory = saveDirectoryItem;
            // ensure we don't tell other code the saved-dir is "loaded" until we've restored it
            // otherwise RefreshFolders and or RefreshImages will call UpdateSavedHistoryDirectoryCache and write the wrong directory to Cache
            IsSavedHistoryDirectoryLoaded = false;
            RestoreExplorerState(saveDirectoryItem);
         
            if ((saveDirectoryItem.SavedDirectoryFolders.Count > 0 || saveDirectoryItem.SavedDirectoryImages.Count > 0) && LoadSavedHistoryDirectoryFromCache == true)
            {
                //fast path: restore from cache
                List<FolderViewModel> oldFolders = _mainWindowViewModel.LibraryFolders.ToList();
                _mainWindowViewModel.LibraryFolders = new ObservableCollection<FolderViewModel>();
                try
                {
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
                }
                catch (Exception ex) 
                {
                    Log.Error(ex,
                        "LoadSavedDirectory Failed disposing folder cover image bitmaps. " +
                        "FolderCount={FolderCount}",
                        oldFolders.Count);
                    throw;
                }
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    _mainWindowViewModel.LibraryFolders = new ObservableCollection<FolderViewModel>(saveDirectoryItem.SavedDirectoryFolders);
                });

                List<ImageViewModel> oldImages = _mainWindowViewModel.Images.ToList();
                _mainWindowViewModel.Images = new ObservableCollection<ImageViewModel>();
                try
                {
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
                }
                catch (Exception ex)
                {
                    Log.Error(ex,
                       "LoadSavedDirectory Failed disposing image bitmaps. " +
                       "ImageCount={ImageCount}",
                       oldImages.Count);
                    throw;
                }
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

                await DisposeDeferredBitmaps();
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

                await DisposeDeferredBitmaps();
            }
        }

        private async Task DisposeSessionHistoryItemBitmaps(SaveDirectory saveDirectoryItem)
        {
            try
            {
                await Task.Run(() =>
                {
                    foreach (FolderViewModel folderVm in saveDirectoryItem.SavedDirectoryFolders)
                        folderVm.CoverImageBitmap?.Dispose();

                    foreach (ImageViewModel imageVm in saveDirectoryItem.SavedDirectoryImages)
                        imageVm.ImageBitmap?.Dispose();
                });
            }
            catch (Exception ex) 
            {
                Log.Error(ex,
                      "DisposeSessionHistoryItemBitmaps Failed disposing image/folder bitmaps. " +
                      "ImageCount={ImageCount}, FolderCount={FolderCount}",
                      saveDirectoryItem.SavedDirectoryImages.Count,
                      saveDirectoryItem.SavedDirectoryFolders.Count);
                throw;
            }
        }
	}
}