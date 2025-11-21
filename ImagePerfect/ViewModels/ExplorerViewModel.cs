using Avalonia;
using Avalonia.Threading;
using ImagePerfect.Models;
using ImagePerfect.ObjectMappers;
using ImagePerfect.Repository;
using ImagePerfect.Repository.IRepository;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static Dapper.SqlMapper;

namespace ImagePerfect.ViewModels
{
    /*
     * VM for Pagination Refresh and Filters
     */
	public class ExplorerViewModel : ViewModelBase
	{
        private readonly MySqlDataSource _dataSource;
        private readonly IConfiguration _configuration;
        private readonly MainWindowViewModel _mainWindowViewModel;

        private string _currentDirectory = string.Empty;

        public List<Image> displayImages = new List<Image>();
        private List<ImageTag> displayImageTags = new List<ImageTag>();
        public List<Folder> displayFolders = new List<Folder>();
        private List<FolderTag> displayFolderTags = new List<FolderTag>();

        //pagination
        //see FolderPageSize in SettingsVm
        private int _totalFolderPages = 1;
        private int _currentFolderPage = 1;

        //see ImagePageSize in SettingsVm
        private int _totalImagePages = 1;
        private int _currentImagePage = 1;
        //max value between TotalFolderPages or TotalImagePages
        private int _maxPage = 1;
        //max value between CurrentFolderPage or CurrentImagePage
        private int _maxCurrentPage = 1;

        //Filters -- if you add new filter variables be sure to update SavedDirectoryVM
        public enum Filters
        {
            None,
            ImageRatingFilter,
            AllImagesInFolderAndSubFolders,
            FiveStarImagesInCurrentDirectory,
            FolderRatingFilter,
            ImageTagFilter,
            FolderTagFilter,
            FolderTagAndRatingFilter,
            FolderDescriptionFilter,
            FolderAlphabeticalFilter,
            FolderDateModifiedFilter,
            ImageYearFilter,
            ImageYearMonthFilter,
            ImageDateRangeFilter,
            AllFavoriteFolders,
            AllFoldersWithNoImportedImages,
            AllFoldersWithMetadataNotScanned,
            AllFoldersWithoutCovers
        }
        public Filters currentFilter = Filters.None;
        public string selectedLetterForFilter = "A";
        public int selectedRatingForFilter = 0;
        public int selectedYearForFilter = 0;
        public int selectedMonthForFilter = 0;
        public DateTimeOffset startDateForFilter;
        public DateTimeOffset endDateForFilter;
        public string tagForFilter = string.Empty;
        public string textForFilter = string.Empty;
        private int _comboFolderFilterRating = 10;
        private string _comboFolderFilterTagOne = string.Empty;
        private string _comboFolderFilterTagTwo = string.Empty;
        private bool _filterInCurrentDirectory = true;
        private bool _loadFoldersAscending = true;

        public ExplorerViewModel(MySqlDataSource dataSource, IConfiguration config, MainWindowViewModel mainWindowViewModel)
        {
            _dataSource = dataSource;
            _configuration = config;
            _mainWindowViewModel = mainWindowViewModel;
        }

        public string CurrentDirectory
        {
            get => _currentDirectory;
            set => this.RaiseAndSetIfChanged(ref _currentDirectory, value);
        }
        public bool LoadFoldersAscending
        {
            get => _loadFoldersAscending;
            set => this.RaiseAndSetIfChanged(ref _loadFoldersAscending, value);
        }

        public bool FilterInCurrentDirectory
        {
            get => _filterInCurrentDirectory;
            set => this.RaiseAndSetIfChanged(ref _filterInCurrentDirectory, value);
        }

        //pagination
        public int MaxCurrentPage
        {
            get => _maxCurrentPage;
            set => this.RaiseAndSetIfChanged(ref _maxCurrentPage, value);
        }
        public int MaxPage
        {
            get => _maxPage;
            set => this.RaiseAndSetIfChanged(ref _maxPage, value);
        }
        public int TotalImagePages
        {
            get => _totalImagePages;
            set => this.RaiseAndSetIfChanged(ref _totalImagePages, value);
        }

        public int CurrentImagePage
        {
            get => _currentImagePage;
            set => this.RaiseAndSetIfChanged(ref _currentImagePage, value);
        }
        public int TotalFolderPages
        {
            get => _totalFolderPages;
            set => this.RaiseAndSetIfChanged(ref _totalFolderPages, value);
        }
        public int CurrentFolderPage
        {
            get => _currentFolderPage;
            set => this.RaiseAndSetIfChanged(ref _currentFolderPage, value);
        }

        public int ComboFolderFilterRating
        {
            get => _comboFolderFilterRating;
            set => this.RaiseAndSetIfChanged(ref _comboFolderFilterRating, value);
        }

        public string ComboFolderFilterTagTwo
        {
            get => _comboFolderFilterTagTwo;
            set => this.RaiseAndSetIfChanged(ref _comboFolderFilterTagTwo, value);
        }
        public string ComboFolderFilterTagOne
        {
            get => _comboFolderFilterTagOne;
            set => this.RaiseAndSetIfChanged(ref _comboFolderFilterTagOne, value);
        }


        public void ResetPagination()
        {
            CurrentFolderPage = 1;
            TotalFolderPages = 1;
            CurrentImagePage = 1;
            TotalImagePages = 1;
            MaxCurrentPage = 1;
            MaxPage = 1;
        }

        private List<Image> ImagePagination()
        {
            //same as FolderPagination
            int offset = _mainWindowViewModel.SettingsVm.ImagePageSize * (CurrentImagePage - 1);
            int totalImageCount = displayImages.Count;
            if (totalImageCount == 0 || totalImageCount <= _mainWindowViewModel.SettingsVm.ImagePageSize)
                return displayImages;
            TotalImagePages = (int)Math.Ceiling(totalImageCount / (double)_mainWindowViewModel.SettingsVm.ImagePageSize);
            List<Image> displayImagesTemp;
            if (CurrentImagePage == TotalImagePages)
            {
                displayImagesTemp = displayImages.GetRange(offset, (totalImageCount - (TotalImagePages - 1) * _mainWindowViewModel.SettingsVm.ImagePageSize));
            }
            else
            {
                displayImagesTemp = displayImages.GetRange(offset, _mainWindowViewModel.SettingsVm.ImagePageSize);
            }
            MaxPage = Math.Max(TotalImagePages, TotalFolderPages);
            MaxCurrentPage = Math.Max(CurrentImagePage, CurrentFolderPage);
            return displayImagesTemp;
        }
        private async Task MapTagsToImagesAddToObservable()
        {
            //DB pull displayImages in the correct order I want to keep it
            //Parallel.ForEachAsync does not iterate in order. Need order preserved. 
            //so iterate over the correct count and store the results in order -- correct slot/index.
            //then re-iterate in order on the UIThread to display ordered results.
            ImageViewModel[] results = new ImageViewModel[displayImages.Count];
            await Parallel.ForEachAsync(
                Enumerable.Range(0, displayImages.Count),
                new ParallelOptions { MaxDegreeOfParallelism = 4 },
                async (i, ct) =>
                {
                    Image taggedImage = ImageMapper.MapTagsToImage(displayImages[i], displayImageTags);
                    ImageViewModel imageViewModel = await ImageMapper.GetImageVm(taggedImage);
                    results[i] = imageViewModel;
                });
            List<ImageViewModel> temp = new List<ImageViewModel>(results);
            // This must be on the UI thread
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _mainWindowViewModel.Images = new ObservableCollection<ImageViewModel>(temp);
            });
        }
        private string NormalizePath(string path)
        {
            // Trim whitespace
            path = path.Trim();
            // Remove trailing slashes
            path = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return path;
        }
        public bool IsInSessionHistory(ImageViewModel image)
        {
            string target = NormalizePath(image.ImagePath);
            return _mainWindowViewModel.HistoryVm.SaveDirectoryItemsList
                .Any(item => item.SavedDirectoryImages.Any(i => NormalizePath(i.ImagePath).Equals(target, StringComparison.OrdinalIgnoreCase)));

            //return _mainWindowViewModel.HistoryVm.SaveDirectoryItemsList
            //    .Any(item => item.SavedDirectoryImages.Any(i => i.ImagePath == image.ImagePath));
        }
        public bool IsInSessionHistory(FolderViewModel folder)
        {
            string target = NormalizePath(folder.FolderPath);
            return _mainWindowViewModel.HistoryVm.SaveDirectoryItemsList
                .Any(item => item.SavedDirectoryFolders.Any(f => NormalizePath(f.FolderPath).Equals(target, StringComparison.OrdinalIgnoreCase)));

            //return _mainWindowViewModel.HistoryVm.SaveDirectoryItemsList
            //    .Any(item => item.SavedDirectoryFolders.Any(f => f.FolderPath == folder.FolderPath));
        }

        private async Task SetDisplayImagesForRefreshImages((List<Image> images, List<ImageTag> tags) data)
        {
            displayImages = data.images;
            displayImageTags = data.tags;
            /*
             * Copy the current ObservableCollection<ImageViewModel> into a new List<ImageViewModel> oldImag (this is a reference copy)
             * so we can safely dispose their unmanaged bitmap resources after detaching the UI.
             * Disposing immediately releases GPU/VRAM and unmanaged memory, which are not freed
             * automatically when clearing the ObservableCollection. This keeps navigation smooth
             * and prevents memory buildup from old image bitmaps.
             */
            if (currentFilter == Filters.None) //dont clear folders -- folder may contain images and folders
            {
                List<ImageViewModel> oldImages = _mainWindowViewModel.Images.ToList();
                _mainWindowViewModel.Images = new ObservableCollection<ImageViewModel>();
                await Task.Run(() =>
                {
                    //only dispose of image bitmaps that are not in SessionHistory
                    foreach (ImageViewModel img in oldImages)
                    {
                        if (!IsInSessionHistory(img))
                        {
                            img.ImageBitmap?.Dispose();
                        }
                    }
                });
            }
            else
            {
                List<FolderViewModel> oldFolders = _mainWindowViewModel.LibraryFolders.ToList();
                List<ImageViewModel> oldImages = _mainWindowViewModel.Images.ToList();
                _mainWindowViewModel.Images = new ObservableCollection<ImageViewModel>();
                _mainWindowViewModel.LibraryFolders = new ObservableCollection<FolderViewModel>();
                await Task.Run(() => 
                {
                    foreach (ImageViewModel img in oldImages)
                    {
                        if (!IsInSessionHistory(img))
                        {
                            img.ImageBitmap?.Dispose();
                        }
                    }

                    foreach (FolderViewModel folder in oldFolders)
                    {
                        if (!IsInSessionHistory(folder))
                        {
                            folder.CoverImageBitmap?.Dispose();
                        }
                    }
                });
            }
            displayImages = ImagePagination();
            await MapTagsToImagesAddToObservable();
        }

        public async Task RefreshImages(string path = "", int folderId = 0, UnitOfWork? uow = null)
        {
            if (uow != null)
            {
                // Use the provided UoW (do NOT dispose here)
                await RefreshImagesInternal(path, folderId, uow);
            }
            else
            {
                // Create and dispose automatically
                await using UnitOfWork localUow = await UnitOfWork.CreateAsync(_dataSource, _configuration);
                await RefreshImagesInternal(path, folderId, localUow);
            }
        }
        private async Task RefreshImagesInternal(string path, int folderId, UnitOfWork uow)
        {
            ImageMethods imageMethods = new ImageMethods(uow);
            // Before clearing/reloading, capture the current UI state into cache
            if (_mainWindowViewModel.HistoryVm.IsSavedHistoryDirectoryLoaded && _mainWindowViewModel.HistoryVm.LoadSavedHistoryDirectoryFromCache)
            {
                await _mainWindowViewModel.HistoryVm.UpdateSavedHistoryDirectoryCache();
            }
            _mainWindowViewModel.ShowLoading = true;
            switch (currentFilter)
            {
                case Filters.None:
                    (List<Image> images, List<ImageTag> tags) imageResult;
                    if (string.IsNullOrEmpty(path))
                    {
                        imageResult = await imageMethods.GetAllImagesInFolder(folderId);
                    }
                    else
                    {
                        imageResult = await imageMethods.GetAllImagesInFolder(path);
                    }
                    await SetDisplayImagesForRefreshImages(imageResult);
                    break;
                case Filters.AllImagesInFolderAndSubFolders:
                    (List<Image> images, List<ImageTag> tags) allImagesInFolderAndSubFoldersResult = await imageMethods.GetAllImagesInFolderAndSubFolders(CurrentDirectory);
                    await SetDisplayImagesForRefreshImages(allImagesInFolderAndSubFoldersResult);
                    break;
                case Filters.ImageRatingFilter:
                    (List<Image> images, List<ImageTag> tags) imageRatingResult = await imageMethods.GetAllImagesAtRating(selectedRatingForFilter, FilterInCurrentDirectory, CurrentDirectory);
                    await SetDisplayImagesForRefreshImages(imageRatingResult);
                    break;
                case Filters.FiveStarImagesInCurrentDirectory:
                    (List<Image> images, List<ImageTag> tags) fiveStarImageRatingResult = await imageMethods.GetAllImagesAtRating(selectedRatingForFilter, true, CurrentDirectory);
                    await SetDisplayImagesForRefreshImages(fiveStarImageRatingResult);
                    break;
                case Filters.ImageTagFilter:
                    (List<Image> images, List<ImageTag> tags) imageTagResult = await imageMethods.GetAllImagesWithTag(tagForFilter, FilterInCurrentDirectory, CurrentDirectory);
                    await SetDisplayImagesForRefreshImages(imageTagResult);
                    break;
                case Filters.ImageYearFilter:
                    (List<Image> images, List<ImageTag> tags) imageYearResult = await imageMethods.GetAllImagesAtYear(selectedYearForFilter, FilterInCurrentDirectory, CurrentDirectory);
                    await SetDisplayImagesForRefreshImages(imageYearResult);
                    break;
                case Filters.ImageYearMonthFilter:
                    (List<Image> images, List<ImageTag> tags) imageYearMonthResult = await imageMethods.GetAllImagesAtYearMonth(selectedYearForFilter, selectedMonthForFilter, FilterInCurrentDirectory, CurrentDirectory);
                    await SetDisplayImagesForRefreshImages(imageYearMonthResult);
                    break;
                case Filters.ImageDateRangeFilter:
                    (List<Image> images, List<ImageTag> tags) imageDateRangeResult = await imageMethods.GetAllImagesInDateRange(startDateForFilter, endDateForFilter, FilterInCurrentDirectory, CurrentDirectory);
                    await SetDisplayImagesForRefreshImages(imageDateRangeResult);
                    break;
            }
            _mainWindowViewModel.ShowLoading = false;
        }

        private List<Folder> FolderPagination()
        {
            /* Example
             * FolderPageSize = 10
             * offest = 10*1 for page = 2
             * totalFolderCount = 14
             * TotalFolderPages = 2
             */
            int offest = _mainWindowViewModel.SettingsVm.FolderPageSize * (CurrentFolderPage - 1);
            int totalFolderCount = displayFolders.Count;
            if (totalFolderCount == 0 || totalFolderCount <= _mainWindowViewModel.SettingsVm.FolderPageSize)
                return displayFolders;
            TotalFolderPages = (int)Math.Ceiling(totalFolderCount / (double)_mainWindowViewModel.SettingsVm.FolderPageSize);
            List<Folder> displayFoldersTemp;
            if (CurrentFolderPage == TotalFolderPages)
            {
                //on last page GetRange count CANNOT be FolderPageSize or index out of range 
                //thus following logical example above in a array of 14 elements the range count on the last page is 14 - 10
                //formul used: totalFolderCount - ((TotalFolderPages - 1)*FolderPageSize)
                //folderCount minus total folders on all but last page
                //14 - 10
                displayFoldersTemp = displayFolders.GetRange(offest, (totalFolderCount - (TotalFolderPages - 1) * _mainWindowViewModel.SettingsVm.FolderPageSize));
            }
            else
            {
                displayFoldersTemp = displayFolders.GetRange(offest, _mainWindowViewModel.SettingsVm.FolderPageSize);
            }
            MaxPage = Math.Max(TotalImagePages, TotalFolderPages);
            MaxCurrentPage = Math.Max(CurrentImagePage, CurrentFolderPage);
            return displayFoldersTemp;
        }

        private async Task MapTagsToFoldersAddToObservable()
        {
            //Parallel.ForEachAsync does not iterate in order. Need order preserved. 
            //so iterate over the correct count and store the results in order -- correct slot/index.
            //then re-iterate in order on the UIThread to display ordered results.
            FolderViewModel[] results = new FolderViewModel[displayFolders.Count];
            await Parallel.ForEachAsync(
                    Enumerable.Range(0, displayFolders.Count),
                    new ParallelOptions { MaxDegreeOfParallelism = 4 },
                    async (i, ct) => {
                        Folder taggedFolder = FolderMapper.MapTagsToFolder(displayFolders[i], displayFolderTags);
                        FolderViewModel folderViewModel = await FolderMapper.GetFolderVm(taggedFolder);
                        results[i] = folderViewModel;
                    });
            List<FolderViewModel> temp = new List<FolderViewModel>(results);
            // This must be on the UI thread
            await Dispatcher.UIThread.InvokeAsync(() => {
                _mainWindowViewModel.LibraryFolders = new ObservableCollection<FolderViewModel>(temp);
            });
        }

        private async Task SetDisplayFoldersForRefreshFolders((List<Folder> folders, List<FolderTag> tags) data)
        {
            displayFolders = data.folders;
            displayFolderTags = data.tags;
            if (currentFilter == Filters.None) //dont clear images -- folder may have folders and images
            {
                List<FolderViewModel> oldFolders = _mainWindowViewModel.LibraryFolders.ToList();
                _mainWindowViewModel.LibraryFolders = new ObservableCollection<FolderViewModel>();
                await Task.Run(() =>
                {
                    foreach (FolderViewModel folder in oldFolders)
                    {
                        if (!IsInSessionHistory(folder))
                        {
                            folder.CoverImageBitmap?.Dispose();
                        }
                    }
                });
            }
            else
            {
                List<FolderViewModel> oldFolders = _mainWindowViewModel.LibraryFolders.ToList();
                List<ImageViewModel> oldImages = _mainWindowViewModel.Images.ToList();
                _mainWindowViewModel.Images = new ObservableCollection<ImageViewModel>();
                _mainWindowViewModel.LibraryFolders = new ObservableCollection<FolderViewModel>();
                await Task.Run(() =>
                {
                    foreach (ImageViewModel img in oldImages)
                    {
                        if (!IsInSessionHistory(img))
                        {
                            img.ImageBitmap?.Dispose();
                        }
                    }

                    foreach (FolderViewModel folder in oldFolders)
                    {
                        if (!IsInSessionHistory(folder))
                        {
                            folder.CoverImageBitmap?.Dispose();
                        }
                    }
                });
            }
            displayFolders = FolderPagination();
            await MapTagsToFoldersAddToObservable();

        }
        //public so we can call from other view models
        public async Task RefreshFolders(string path = "", UnitOfWork? uow = null)
        {
            if(uow != null)
            {
                await RefreshFoldersInternal(path, uow);
            }
            else
            {
                await using UnitOfWork localUow = await UnitOfWork.CreateAsync(_dataSource, _configuration);
                await RefreshFoldersInternal(path, localUow);
            }
        }
        private async Task RefreshFoldersInternal(string path, UnitOfWork uow)
        {
            FolderMethods folderMethods = new FolderMethods(uow);
            /*
             * Do not call UpdateSavedHistoryDirectoryCache() in RefreshFolderProps() as that only incrementally updates the live UI
             * So calling there will only capture the 1st UI change. 
             */
            // Before clearing/reloading, capture the current UI state into cache
            if (_mainWindowViewModel.HistoryVm.IsSavedHistoryDirectoryLoaded && _mainWindowViewModel.HistoryVm.LoadSavedHistoryDirectoryFromCache)
            {
                await _mainWindowViewModel.HistoryVm.UpdateSavedHistoryDirectoryCache();
            }

            _mainWindowViewModel.ShowLoading = true;
            switch (currentFilter)
            {
                case Filters.None:
                    (List<Folder> folders, List<FolderTag> tags) folderResult;
                    if (String.IsNullOrEmpty(path))
                    {
                        folderResult = await folderMethods.GetFoldersInDirectory(CurrentDirectory, LoadFoldersAscending);
                    }
                    else
                    {
                        folderResult = await folderMethods.GetFoldersInDirectory(path, LoadFoldersAscending);
                    }
                    await SetDisplayFoldersForRefreshFolders(folderResult);
                    break;
                case Filters.FolderDateModifiedFilter:
                    (List<Folder> folders, List<FolderTag> tags) foldersInCurrentDirectoryResult = await folderMethods.GetFoldersInDirectory(CurrentDirectory, LoadFoldersAscending);
                    //sort in C# on Date Modified
                    foldersInCurrentDirectoryResult.folders = SortFoldersByDateModified(foldersInCurrentDirectoryResult.folders);
                    await SetDisplayFoldersForRefreshFolders(foldersInCurrentDirectoryResult);
                    break;
                case Filters.FolderAlphabeticalFilter:
                    (List<Folder> folders, List<FolderTag> tags) folderAlphabeticalResult = await folderMethods.GetFoldersInDirectoryByStartingLetter(CurrentDirectory, LoadFoldersAscending, selectedLetterForFilter);
                    await SetDisplayFoldersForRefreshFolders(folderAlphabeticalResult);
                    break;
                case Filters.FolderRatingFilter:
                    (List<Folder> folders, List<FolderTag> tags) folderRatingResult = await folderMethods.GetAllFoldersAtRating(selectedRatingForFilter, FilterInCurrentDirectory, CurrentDirectory);
                    await SetDisplayFoldersForRefreshFolders(folderRatingResult);
                    break;
                case Filters.FolderTagFilter:
                    (List<Folder> folders, List<FolderTag> tags) folderTagResult = await folderMethods.GetAllFoldersWithTag(tagForFilter, FilterInCurrentDirectory, CurrentDirectory);
                    await SetDisplayFoldersForRefreshFolders(folderTagResult);
                    break;
                case Filters.FolderTagAndRatingFilter:
                    (List<Folder> folders, List<FolderTag> tags) folderRatingAndTagResult = await folderMethods.GetAllFoldersWithRatingAndTag(ComboFolderFilterRating, ComboFolderFilterTagOne, ComboFolderFilterTagTwo, FilterInCurrentDirectory, CurrentDirectory);
                    await SetDisplayFoldersForRefreshFolders(folderRatingAndTagResult);
                    break;
                case Filters.FolderDescriptionFilter:
                    (List<Folder> folders, List<FolderTag> tags) folderDescriptionResult = await folderMethods.GetAllFoldersWithDescriptionText(textForFilter, FilterInCurrentDirectory, CurrentDirectory);
                    await SetDisplayFoldersForRefreshFolders(folderDescriptionResult);
                    break;
                case Filters.AllFavoriteFolders:
                    (List<Folder> folders, List<FolderTag> tags) allFavoriteFoldersResult = await folderMethods.GetAllFavoriteFolders();
                    await SetDisplayFoldersForRefreshFolders(allFavoriteFoldersResult);
                    break;
                case Filters.AllFoldersWithNoImportedImages:
                    (List<Folder> folders, List<FolderTag> tags) allFoldersWithNoImportedImagesResult = await folderMethods.GetAllFoldersWithNoImportedImages(FilterInCurrentDirectory, CurrentDirectory);
                    await SetDisplayFoldersForRefreshFolders(allFoldersWithNoImportedImagesResult);
                    break;
                case Filters.AllFoldersWithMetadataNotScanned:
                    (List<Folder> folders, List<FolderTag> tags) allFoldersWithMetadataNotScannedResult = await folderMethods.GetAllFoldersWithMetadataNotScanned(FilterInCurrentDirectory, CurrentDirectory);
                    await SetDisplayFoldersForRefreshFolders(allFoldersWithMetadataNotScannedResult);
                    break;
                case Filters.AllFoldersWithoutCovers:
                    (List<Folder> folders, List<FolderTag> tags) allFoldersWithoutCoversResult = await folderMethods.GetAllFoldersWithoutCovers(FilterInCurrentDirectory, CurrentDirectory);
                    await SetDisplayFoldersForRefreshFolders(allFoldersWithoutCoversResult);
                    break;
            }
            _mainWindowViewModel.ShowLoading = false;
        }
        private async Task MapTagsToSingleFolderUpdateObservable(FolderViewModel folderVm)
        {
            for (int i = 0; i < displayFolders.Count; i++)
            {
                //only map the one that is being updated
                if (displayFolders[i].FolderId == folderVm.FolderId)
                {
                    //need to map tags to folders 
                    displayFolders[i] = FolderMapper.MapTagsToFolder(displayFolders[i], displayFolderTags);
                    FolderViewModel folderViewModel = await FolderMapper.GetFolderVm(displayFolders[i]);
                    //will be in the same order unless delete/move or next back folder
                    //Any non destructive operation that does not affect the number or order of items returned from
                    //the sql query will be in the same order so just modify props for a much cleaner UI refresh
                    _mainWindowViewModel.LibraryFolders[i] = folderViewModel;
                    return;
                }
            }
        }

        private async Task SetDisplayFoldersForRefreshFolderProps((List<Folder> folders, List<FolderTag> tags) data, FolderViewModel folderVm)
        {
            displayFolders = data.folders;
            displayFolderTags = data.tags;
            displayFolders = FolderPagination();
            /*
             * For FolderDateModifiedFilter, if a folder gets modified (e.g., a cover is copied to it from a child folder),
             * a subsequent DB pull in RefreshFolderProps will return folders in a different order.
             * That causes displayFolders to fall out of sync with LibraryFolders, and
             * MapTagsToSingleFolderUpdateObservable will update the wrong folder.
             * 
             * To prevent this, for a quick refresh we preserve the original LibraryFolders order
             * instead of using the new order from the DB pull.
             */
            if (currentFilter == Filters.FolderDateModifiedFilter)
            {
                // Build lookup of LibraryFolder order by FolderId
                // this is the order of LibraryFolders in a Dictionary<FolderId, index>
                Dictionary<int,int> libraryOrder = _mainWindowViewModel.LibraryFolders
                    .Select((f, index) => new { f.FolderId, index })
                    .ToDictionary(x => x.FolderId, x => x.index);

                // Reorder display folders to match existing LibraryFolders order (if possible)
                displayFolders = displayFolders.OrderBy(f => libraryOrder.TryGetValue(f.FolderId, out var idx) ? idx : int.MaxValue).ToList();
            }
            await MapTagsToSingleFolderUpdateObservable(folderVm);
        }
        //public so we can call from other view models
        public async Task RefreshFolderProps(string path, FolderViewModel folderVm, UnitOfWork? uow = null)
        {
            if (uow != null)
            {
                await RefreshFolderPropsInternal(path, folderVm, uow);
            }
            else 
            {
                await using UnitOfWork localUow = await UnitOfWork.CreateAsync(_dataSource, _configuration);
                await RefreshFolderPropsInternal(path, folderVm, localUow);
            }
        }
        private async Task RefreshFolderPropsInternal(string path, FolderViewModel folderVm, UnitOfWork uow)
        {
            FolderMethods folderMethods = new FolderMethods(uow);
            /*
             * Do not call UpdateSavedHistoryDirectoryCache() in RefreshFolderProps() as this only incrementally updates the live UI
             * So calling here will only capture the 1st UI change. 
             */
            _mainWindowViewModel.ShowLoading = true;
            switch (currentFilter)
            {
                case Filters.None:
                    (List<Folder> folders, List<FolderTag> tags) folderResult = await folderMethods.GetFoldersInDirectory(path, LoadFoldersAscending);
                    await SetDisplayFoldersForRefreshFolderProps(folderResult, folderVm);
                    break;
                case Filters.FolderDateModifiedFilter:
                    (List<Folder> folders, List<FolderTag> tags) foldersInCurrentDirectoryResult = await folderMethods.GetFoldersInDirectory(CurrentDirectory, LoadFoldersAscending);
                    //sort in C# on Date Modified
                    foldersInCurrentDirectoryResult.folders = SortFoldersByDateModified(foldersInCurrentDirectoryResult.folders);
                    await SetDisplayFoldersForRefreshFolderProps(foldersInCurrentDirectoryResult, folderVm);
                    break;
                case Filters.FolderAlphabeticalFilter:
                    (List<Folder> folders, List<FolderTag> tags) folderAlphabeticalResult = await folderMethods.GetFoldersInDirectoryByStartingLetter(CurrentDirectory, LoadFoldersAscending, selectedLetterForFilter);
                    await SetDisplayFoldersForRefreshFolderProps(folderAlphabeticalResult, folderVm);
                    break;
                case Filters.FolderRatingFilter:
                    (List<Folder> folders, List<FolderTag> tags) folderRatingResult = await folderMethods.GetAllFoldersAtRating(selectedRatingForFilter, FilterInCurrentDirectory, CurrentDirectory);
                    await SetDisplayFoldersForRefreshFolderProps(folderRatingResult, folderVm);
                    break;
                case Filters.FolderTagFilter:
                    (List<Folder> folders, List<FolderTag> tags) folderTagResult = await folderMethods.GetAllFoldersWithTag(tagForFilter, FilterInCurrentDirectory, CurrentDirectory);
                    await SetDisplayFoldersForRefreshFolderProps(folderTagResult, folderVm);
                    break;
                case Filters.FolderTagAndRatingFilter:
                    (List<Folder> folders, List<FolderTag> tags) folderRatingAndTagResult = await folderMethods.GetAllFoldersWithRatingAndTag(ComboFolderFilterRating, ComboFolderFilterTagOne, ComboFolderFilterTagTwo, FilterInCurrentDirectory, CurrentDirectory);
                    await SetDisplayFoldersForRefreshFolderProps(folderRatingAndTagResult, folderVm);
                    break;
                case Filters.FolderDescriptionFilter:
                    (List<Folder> folders, List<FolderTag> tags) folderDescriptionResult = await folderMethods.GetAllFoldersWithDescriptionText(textForFilter, FilterInCurrentDirectory, CurrentDirectory);
                    await SetDisplayFoldersForRefreshFolderProps(folderDescriptionResult, folderVm);
                    break;
                case Filters.AllFavoriteFolders:
                    (List<Folder> folders, List<FolderTag> tags) allFavoriteFoldersResult = await folderMethods.GetAllFavoriteFolders();
                    await SetDisplayFoldersForRefreshFolderProps(allFavoriteFoldersResult, folderVm);
                    break;
                case Filters.AllFoldersWithNoImportedImages:
                    (List<Folder> folders, List<FolderTag> tags) allFoldersWithNoImportedImagesResult = await folderMethods.GetAllFoldersWithNoImportedImages(FilterInCurrentDirectory, CurrentDirectory);
                    await SetDisplayFoldersForRefreshFolderProps(allFoldersWithNoImportedImagesResult, folderVm);
                    break;
                case Filters.AllFoldersWithMetadataNotScanned:
                    (List<Folder> folders, List<FolderTag> tags) allFoldersWithMetadataNotScannedResult = await folderMethods.GetAllFoldersWithMetadataNotScanned(FilterInCurrentDirectory, CurrentDirectory);
                    await SetDisplayFoldersForRefreshFolderProps(allFoldersWithMetadataNotScannedResult, folderVm);
                    break;
                case Filters.AllFoldersWithoutCovers:
                    (List<Folder> folders, List<FolderTag> tags) allFoldersWithoutCoversResult = await folderMethods.GetAllFoldersWithoutCovers(FilterInCurrentDirectory, CurrentDirectory);
                    await SetDisplayFoldersForRefreshFolderProps(allFoldersWithoutCoversResult, folderVm);
                    break;
            }
            _mainWindowViewModel.ShowLoading = false;
        }

        private List<Folder> SortFoldersByDateModified(List<Folder> folders)
        {
            Parallel.ForEach(folders, new ParallelOptions { MaxDegreeOfParallelism = 4 }, folder => {
                try
                {
                    folder.DateModified = Directory.GetLastWriteTime(folder.FolderPath);
                }
                catch
                {
                    folder.DateModified = DateTime.MinValue; // fallback for missing/deleted folders
                }
            });
            return folders.OrderByDescending(f => f.DateModified).ToList();
        }
        //loads the previous X elements in CurrentDirectory
        public async Task PreviousPage()
        {
            if (CurrentFolderPage > 1)
            {
                CurrentFolderPage = CurrentFolderPage - 1;
                await RefreshFolders();
            }
            if (CurrentImagePage > 1)
            {
                CurrentImagePage = CurrentImagePage - 1;
                await RefreshImages(CurrentDirectory);
            }
        }

        //loads the next X elements in CurrentDirectory
        public async Task NextPage()
        {
            if (CurrentFolderPage < TotalFolderPages)
            {
                CurrentFolderPage = CurrentFolderPage + 1;
                await RefreshFolders();
            }
            if (CurrentImagePage < TotalImagePages)
            {
                CurrentImagePage = CurrentImagePage + 1;
                await RefreshImages(CurrentDirectory);
            }
        }

        public async Task GoToPage(int pageNumber)
        {
            if (pageNumber <= TotalFolderPages)
            {
                CurrentFolderPage = pageNumber;
                await RefreshFolders();
            }
            if (pageNumber <= TotalImagePages)
            {
                CurrentImagePage = pageNumber;
                await RefreshImages(CurrentDirectory);
            }
        }
    }
}