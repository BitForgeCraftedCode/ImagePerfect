using Avalonia.Threading;
using ImagePerfect.Models;
using ImagePerfect.ObjectMappers;
using ImagePerfect.Repository.IRepository;
using ReactiveUI;
using System;
using System.Collections.Generic;
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
        private readonly IUnitOfWork _unitOfWork;
        private readonly FolderMethods _folderMethods;
        private readonly ImageMethods _imageMethods;
        private readonly MainWindowViewModel _mainWindowViewModel;

        public List<Image> displayImages = new List<Image>();
        private List<ImageTag> displayImageTags = new List<ImageTag>();
        public List<Folder> displayFolders = new List<Folder>();
        private List<FolderTag> displayFolderTags = new List<FolderTag>();

        public ExplorerViewModel(IUnitOfWork unitOfWork, MainWindowViewModel mainWindowViewModel)
        {
            _unitOfWork = unitOfWork;
            _mainWindowViewModel = mainWindowViewModel;
            _folderMethods = new FolderMethods(_unitOfWork);
            _imageMethods = new ImageMethods(_unitOfWork);
        }

        private List<Image> ImagePagination()
        {
            //same as FolderPagination
            int offset = _mainWindowViewModel.SettingsVm.ImagePageSize * (_mainWindowViewModel.CurrentImagePage - 1);
            int totalImageCount = displayImages.Count;
            if (totalImageCount == 0 || totalImageCount <= _mainWindowViewModel.SettingsVm.ImagePageSize)
                return displayImages;
            _mainWindowViewModel.TotalImagePages = (int)Math.Ceiling(totalImageCount / (double)_mainWindowViewModel.SettingsVm.ImagePageSize);
            List<Image> displayImagesTemp;
            if (_mainWindowViewModel.CurrentImagePage == _mainWindowViewModel.TotalImagePages)
            {
                displayImagesTemp = displayImages.GetRange(offset, (totalImageCount - (_mainWindowViewModel.TotalImagePages - 1) * _mainWindowViewModel.SettingsVm.ImagePageSize));
            }
            else
            {
                displayImagesTemp = displayImages.GetRange(offset, _mainWindowViewModel.SettingsVm.ImagePageSize);
            }
            _mainWindowViewModel.MaxPage = Math.Max(_mainWindowViewModel.TotalImagePages, _mainWindowViewModel.TotalFolderPages);
            _mainWindowViewModel.MaxCurrentPage = Math.Max(_mainWindowViewModel.CurrentImagePage, _mainWindowViewModel.CurrentFolderPage);
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
            // This must be on the UI thread
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                foreach (ImageViewModel imageViewModel in results)
                {
                    _mainWindowViewModel.Images.Add(imageViewModel);
                }
            });
        }
        public async Task RefreshImages(string path = "", int folderId = 0)
        {
            // Before clearing/reloading, capture the current UI state into cache
            if (_mainWindowViewModel.IsSavedDirectoryLoaded && _mainWindowViewModel.LoadSavedDirectoryFromCache)
            {
                _mainWindowViewModel.SavedDirectoryVm.UpdateSavedDirectoryCache();
            }
            _mainWindowViewModel.ShowLoading = true;
            switch (_mainWindowViewModel.currentFilter)
            {
                case MainWindowViewModel.Filters.None:
                    (List<Image> images, List<ImageTag> tags) imageResult;
                    if (string.IsNullOrEmpty(path))
                    {
                        imageResult = await _imageMethods.GetAllImagesInFolder(folderId);
                    }
                    else
                    {
                        imageResult = await _imageMethods.GetAllImagesInFolder(path);
                    }
                    displayImages = imageResult.images;
                    displayImageTags = imageResult.tags;

                    _mainWindowViewModel.Images.Clear();
                    displayImages = ImagePagination();
                    await MapTagsToImagesAddToObservable();
                    break;
                case MainWindowViewModel.Filters.AllImagesInFolderAndSubFolders:
                    (List<Image> images, List<ImageTag> tags) allImagesInFolderAndSubFoldersResult = await _imageMethods.GetAllImagesInFolderAndSubFolders(_mainWindowViewModel.CurrentDirectory);
                    displayImages = allImagesInFolderAndSubFoldersResult.images;
                    displayImageTags = allImagesInFolderAndSubFoldersResult.tags;

                    _mainWindowViewModel.Images.Clear();
                    _mainWindowViewModel.LibraryFolders.Clear();
                    displayImages = ImagePagination();
                    await MapTagsToImagesAddToObservable();
                    break;
                case MainWindowViewModel.Filters.ImageRatingFilter:
                    (List<Image> images, List<ImageTag> tags) imageRatingResult = await _imageMethods.GetAllImagesAtRating(_mainWindowViewModel.selectedRatingForFilter, _mainWindowViewModel.FilterInCurrentDirectory, _mainWindowViewModel.CurrentDirectory);
                    displayImages = imageRatingResult.images;
                    displayImageTags = imageRatingResult.tags;

                    _mainWindowViewModel.Images.Clear();
                    _mainWindowViewModel.LibraryFolders.Clear();
                    displayImages = ImagePagination();
                    await MapTagsToImagesAddToObservable();
                    break;
                case MainWindowViewModel.Filters.FiveStarImagesInCurrentDirectory:
                    (List<Image> images, List<ImageTag> tags) fiveStarImageRatingResult = await _imageMethods.GetAllImagesAtRating(_mainWindowViewModel.selectedRatingForFilter, true, _mainWindowViewModel.CurrentDirectory);
                    displayImages = fiveStarImageRatingResult.images;
                    displayImageTags = fiveStarImageRatingResult.tags;

                    _mainWindowViewModel.Images.Clear();
                    _mainWindowViewModel.LibraryFolders.Clear();
                    displayImages = ImagePagination();
                    await MapTagsToImagesAddToObservable();
                    break;
                case MainWindowViewModel.Filters.ImageTagFilter:
                    (List<Image> images, List<ImageTag> tags) imageTagResult = await _imageMethods.GetAllImagesWithTag(_mainWindowViewModel.tagForFilter, _mainWindowViewModel.FilterInCurrentDirectory, _mainWindowViewModel.CurrentDirectory);
                    displayImages = imageTagResult.images;
                    displayImageTags = imageTagResult.tags;

                    _mainWindowViewModel.Images.Clear();
                    _mainWindowViewModel.LibraryFolders.Clear();
                    displayImages = ImagePagination();
                    await MapTagsToImagesAddToObservable();
                    break;
                case MainWindowViewModel.Filters.ImageYearFilter:
                    (List<Image> images, List<ImageTag> tags) imageYearResult = await _imageMethods.GetAllImagesAtYear(_mainWindowViewModel.selectedYearForFilter, _mainWindowViewModel.FilterInCurrentDirectory, _mainWindowViewModel.CurrentDirectory);
                    displayImages = imageYearResult.images;
                    displayImageTags = imageYearResult.tags;

                    _mainWindowViewModel.Images.Clear();
                    _mainWindowViewModel.LibraryFolders.Clear();
                    displayImages = ImagePagination();
                    await MapTagsToImagesAddToObservable();
                    break;
                case MainWindowViewModel.Filters.ImageYearMonthFilter:
                    (List<Image> images, List<ImageTag> tags) imageYearMonthResult = await _imageMethods.GetAllImagesAtYearMonth(_mainWindowViewModel.selectedYearForFilter, _mainWindowViewModel.selectedMonthForFilter, _mainWindowViewModel.FilterInCurrentDirectory, _mainWindowViewModel.CurrentDirectory);
                    displayImages = imageYearMonthResult.images;
                    displayImageTags = imageYearMonthResult.tags;

                    _mainWindowViewModel.Images.Clear();
                    _mainWindowViewModel.LibraryFolders.Clear();
                    displayImages = ImagePagination();
                    await MapTagsToImagesAddToObservable();
                    break;
                case MainWindowViewModel.Filters.ImageDateRangeFilter:
                    (List<Image> images, List<ImageTag> tags) imageDateRangeResult = await _imageMethods.GetAllImagesInDateRange(_mainWindowViewModel.startDateForFilter, _mainWindowViewModel.endDateForFilter, _mainWindowViewModel.FilterInCurrentDirectory, _mainWindowViewModel.CurrentDirectory);
                    displayImages = imageDateRangeResult.images;
                    displayImageTags = imageDateRangeResult.tags;

                    _mainWindowViewModel.Images.Clear();
                    _mainWindowViewModel.LibraryFolders.Clear();
                    displayImages = ImagePagination();
                    await MapTagsToImagesAddToObservable();
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
            int offest = _mainWindowViewModel.SettingsVm.FolderPageSize * (_mainWindowViewModel.CurrentFolderPage - 1);
            int totalFolderCount = displayFolders.Count;
            if (totalFolderCount == 0 || totalFolderCount <= _mainWindowViewModel.SettingsVm.FolderPageSize)
                return displayFolders;
            _mainWindowViewModel.TotalFolderPages = (int)Math.Ceiling(totalFolderCount / (double)_mainWindowViewModel.SettingsVm.FolderPageSize);
            List<Folder> displayFoldersTemp;
            if (_mainWindowViewModel.CurrentFolderPage == _mainWindowViewModel.TotalFolderPages)
            {
                //on last page GetRange count CANNOT be FolderPageSize or index out of range 
                //thus following logical example above in a array of 14 elements the range count on the last page is 14 - 10
                //formul used: totalFolderCount - ((TotalFolderPages - 1)*FolderPageSize)
                //folderCount minus total folders on all but last page
                //14 - 10
                displayFoldersTemp = displayFolders.GetRange(offest, (totalFolderCount - (_mainWindowViewModel.TotalFolderPages - 1) * _mainWindowViewModel.SettingsVm.FolderPageSize));
            }
            else
            {
                displayFoldersTemp = displayFolders.GetRange(offest, _mainWindowViewModel.SettingsVm.FolderPageSize);
            }
            _mainWindowViewModel.MaxPage = Math.Max(_mainWindowViewModel.TotalImagePages, _mainWindowViewModel.TotalFolderPages);
            _mainWindowViewModel.MaxCurrentPage = Math.Max(_mainWindowViewModel.CurrentImagePage, _mainWindowViewModel.CurrentFolderPage);
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

            // This must be on the UI thread
            await Dispatcher.UIThread.InvokeAsync(() => {
                foreach (FolderViewModel folderViewModel in results)
                {
                    _mainWindowViewModel.LibraryFolders.Add(folderViewModel);
                }
            });
        }

        //public so we can call from other view models
        public async Task RefreshFolders(string path = "")
        {
            /*
             * Do not call UpdateSavedDirectoryCache() in RefreshFolderProps() as that only incrementally updates the live UI
             * So calling there will only capture the 1st UI change. 
             */
            // Before clearing/reloading, capture the current UI state into cache
            if (_mainWindowViewModel.IsSavedDirectoryLoaded && _mainWindowViewModel.LoadSavedDirectoryFromCache)
            {
                _mainWindowViewModel.SavedDirectoryVm.UpdateSavedDirectoryCache();
            }

            _mainWindowViewModel.ShowLoading = true;
            switch (_mainWindowViewModel.currentFilter)
            {
                case MainWindowViewModel.Filters.None:
                    (List<Folder> folders, List<FolderTag> tags) folderResult;
                    if (String.IsNullOrEmpty(path))
                    {
                        folderResult = await _folderMethods.GetFoldersInDirectory(_mainWindowViewModel.CurrentDirectory, _mainWindowViewModel.LoadFoldersAscending);
                    }
                    else
                    {
                        folderResult = await _folderMethods.GetFoldersInDirectory(path, _mainWindowViewModel.LoadFoldersAscending);
                    }
                    displayFolders = folderResult.folders;
                    displayFolderTags = folderResult.tags;
                    _mainWindowViewModel.LibraryFolders.Clear();
                    displayFolders = FolderPagination();
                    await MapTagsToFoldersAddToObservable();
                    break;
                case MainWindowViewModel.Filters.FolderAlphabeticalFilter:
                    (List<Folder> folders, List<FolderTag> tags) folderAlphabeticalResult = await _folderMethods.GetFoldersInDirectoryByStartingLetter(_mainWindowViewModel.CurrentDirectory, _mainWindowViewModel.LoadFoldersAscending, _mainWindowViewModel.selectedLetterForFilter);
                    displayFolders = folderAlphabeticalResult.folders;
                    displayFolderTags = folderAlphabeticalResult.tags;

                    _mainWindowViewModel.Images.Clear();
                    _mainWindowViewModel.LibraryFolders.Clear();
                    displayFolders = FolderPagination();
                    await MapTagsToFoldersAddToObservable();
                    break;
                case MainWindowViewModel.Filters.FolderRatingFilter:
                    (List<Folder> folders, List<FolderTag> tags) folderRatingResult = await _folderMethods.GetAllFoldersAtRating(_mainWindowViewModel.selectedRatingForFilter, _mainWindowViewModel.FilterInCurrentDirectory, _mainWindowViewModel.CurrentDirectory);
                    displayFolders = folderRatingResult.folders;
                    displayFolderTags = folderRatingResult.tags;

                    _mainWindowViewModel.Images.Clear();
                    _mainWindowViewModel.LibraryFolders.Clear();
                    displayFolders = FolderPagination();
                    await MapTagsToFoldersAddToObservable();
                    break;
                case MainWindowViewModel.Filters.FolderTagFilter:
                    (List<Folder> folders, List<FolderTag> tags) folderTagResult = await _folderMethods.GetAllFoldersWithTag(_mainWindowViewModel.tagForFilter, _mainWindowViewModel.FilterInCurrentDirectory, _mainWindowViewModel.CurrentDirectory);
                    displayFolders = folderTagResult.folders;
                    displayFolderTags = folderTagResult.tags;

                    _mainWindowViewModel.Images.Clear();
                    _mainWindowViewModel.LibraryFolders.Clear();
                    displayFolders = FolderPagination();
                    await MapTagsToFoldersAddToObservable();
                    break;
                case MainWindowViewModel.Filters.FolderTagAndRatingFilter:
                    (List<Folder> folders, List<FolderTag> tags) folderRatingAndTagResult = await _folderMethods.GetAllFoldersWithRatingAndTag(_mainWindowViewModel.ComboFolderFilterRating, _mainWindowViewModel.ComboFolderFilterTag, _mainWindowViewModel.FilterInCurrentDirectory, _mainWindowViewModel.CurrentDirectory);
                    displayFolders = folderRatingAndTagResult.folders;
                    displayFolderTags = folderRatingAndTagResult.tags;

                    _mainWindowViewModel.Images.Clear();
                    _mainWindowViewModel.LibraryFolders.Clear();
                    displayFolders = FolderPagination();
                    await MapTagsToFoldersAddToObservable();
                    break;
                case MainWindowViewModel.Filters.FolderDescriptionFilter:
                    (List<Folder> folders, List<FolderTag> tags) folderDescriptionResult = await _folderMethods.GetAllFoldersWithDescriptionText(_mainWindowViewModel.textForFilter, _mainWindowViewModel.FilterInCurrentDirectory, _mainWindowViewModel.CurrentDirectory);
                    displayFolders = folderDescriptionResult.folders;
                    displayFolderTags = folderDescriptionResult.tags;

                    _mainWindowViewModel.Images.Clear();
                    _mainWindowViewModel.LibraryFolders.Clear();
                    displayFolders = FolderPagination();
                    await MapTagsToFoldersAddToObservable();
                    break;
                case MainWindowViewModel.Filters.AllFavoriteFolders:
                    (List<Folder> folders, List<FolderTag> tags) allFavoriteFoldersResult = await _folderMethods.GetAllFavoriteFolders();
                    displayFolders = allFavoriteFoldersResult.folders;
                    displayFolderTags = allFavoriteFoldersResult.tags;

                    _mainWindowViewModel.Images.Clear();
                    _mainWindowViewModel.LibraryFolders.Clear();
                    displayFolders = FolderPagination();
                    await MapTagsToFoldersAddToObservable();
                    break;
                case MainWindowViewModel.Filters.AllFoldersWithNoImportedImages:
                    (List<Folder> folders, List<FolderTag> tags) allFoldersWithNoImportedImagesResult = await _folderMethods.GetAllFoldersWithNoImportedImages(_mainWindowViewModel.FilterInCurrentDirectory, _mainWindowViewModel.CurrentDirectory);
                    displayFolders = allFoldersWithNoImportedImagesResult.folders;
                    displayFolderTags = allFoldersWithNoImportedImagesResult.tags;

                    _mainWindowViewModel.Images.Clear();
                    _mainWindowViewModel.LibraryFolders.Clear();
                    displayFolders = FolderPagination();
                    await MapTagsToFoldersAddToObservable();
                    break;
                case MainWindowViewModel.Filters.AllFoldersWithMetadataNotScanned:
                    (List<Folder> folders, List<FolderTag> tags) allFoldersWithMetadataNotScannedResult = await _folderMethods.GetAllFoldersWithMetadataNotScanned(_mainWindowViewModel.FilterInCurrentDirectory, _mainWindowViewModel.CurrentDirectory);
                    displayFolders = allFoldersWithMetadataNotScannedResult.folders;
                    displayFolderTags = allFoldersWithMetadataNotScannedResult.tags;

                    _mainWindowViewModel.Images.Clear();
                    _mainWindowViewModel.LibraryFolders.Clear();
                    displayFolders = FolderPagination();
                    await MapTagsToFoldersAddToObservable();
                    break;
                case MainWindowViewModel.Filters.AllFoldersWithoutCovers:
                    (List<Folder> folders, List<FolderTag> tags) allFoldersWithoutCoversResult = await _folderMethods.GetAllFoldersWithoutCovers(_mainWindowViewModel.FilterInCurrentDirectory, _mainWindowViewModel.CurrentDirectory);
                    displayFolders = allFoldersWithoutCoversResult.folders;
                    displayFolderTags = allFoldersWithoutCoversResult.tags;

                    _mainWindowViewModel.Images.Clear();
                    _mainWindowViewModel.LibraryFolders.Clear();
                    displayFolders = FolderPagination();
                    await MapTagsToFoldersAddToObservable();
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

        //public so we can call from other view models
        public async Task RefreshFolderProps(string path, FolderViewModel folderVm)
        {
            /*
             * Do not call UpdateSavedDirectoryCache() in RefreshFolderProps() as this only incrementally updates the live UI
             * So calling here will only capture the 1st UI change. 
             */
            _mainWindowViewModel.ShowLoading = true;
            switch (_mainWindowViewModel.currentFilter)
            {
                case MainWindowViewModel.Filters.None:
                    (List<Folder> folders, List<FolderTag> tags) folderResult = await _folderMethods.GetFoldersInDirectory(path, _mainWindowViewModel.LoadFoldersAscending);
                    displayFolders = folderResult.folders;
                    displayFolderTags = folderResult.tags;
                    displayFolders = FolderPagination();
                    await MapTagsToSingleFolderUpdateObservable(folderVm);
                    break;
                case MainWindowViewModel.Filters.FolderAlphabeticalFilter:
                    (List<Folder> folders, List<FolderTag> tags) folderAlphabeticalResult = await _folderMethods.GetFoldersInDirectoryByStartingLetter(_mainWindowViewModel.CurrentDirectory, _mainWindowViewModel.LoadFoldersAscending, _mainWindowViewModel.selectedLetterForFilter);
                    displayFolders = folderAlphabeticalResult.folders;
                    displayFolderTags = folderAlphabeticalResult.tags;
                    displayFolders = FolderPagination();
                    await MapTagsToSingleFolderUpdateObservable(folderVm);
                    break;
                case MainWindowViewModel.Filters.FolderRatingFilter:
                    (List<Folder> folders, List<FolderTag> tags) folderRatingResult = await _folderMethods.GetAllFoldersAtRating(_mainWindowViewModel.selectedRatingForFilter, _mainWindowViewModel.FilterInCurrentDirectory, _mainWindowViewModel.CurrentDirectory);
                    displayFolders = folderRatingResult.folders;
                    displayFolderTags = folderRatingResult.tags;
                    displayFolders = FolderPagination();
                    await MapTagsToSingleFolderUpdateObservable(folderVm);
                    break;
                case MainWindowViewModel.Filters.FolderTagFilter:
                    (List<Folder> folders, List<FolderTag> tags) folderTagResult = await _folderMethods.GetAllFoldersWithTag(_mainWindowViewModel.tagForFilter, _mainWindowViewModel.FilterInCurrentDirectory, _mainWindowViewModel.CurrentDirectory);
                    displayFolders = folderTagResult.folders;
                    displayFolderTags = folderTagResult.tags;
                    displayFolders = FolderPagination();
                    await MapTagsToSingleFolderUpdateObservable(folderVm);
                    break;
                case MainWindowViewModel.Filters.FolderTagAndRatingFilter:
                    (List<Folder> folders, List<FolderTag> tags) folderRatingAndTagResult = await _folderMethods.GetAllFoldersWithRatingAndTag(_mainWindowViewModel.ComboFolderFilterRating, _mainWindowViewModel.ComboFolderFilterTag, _mainWindowViewModel.FilterInCurrentDirectory, _mainWindowViewModel.CurrentDirectory);
                    displayFolders = folderRatingAndTagResult.folders;
                    displayFolderTags = folderRatingAndTagResult.tags;
                    displayFolders = FolderPagination();
                    await MapTagsToSingleFolderUpdateObservable(folderVm);
                    break;
                case MainWindowViewModel.Filters.FolderDescriptionFilter:
                    (List<Folder> folders, List<FolderTag> tags) folderDescriptionResult = await _folderMethods.GetAllFoldersWithDescriptionText(_mainWindowViewModel.textForFilter, _mainWindowViewModel.FilterInCurrentDirectory, _mainWindowViewModel.CurrentDirectory);
                    displayFolders = folderDescriptionResult.folders;
                    displayFolderTags = folderDescriptionResult.tags;
                    displayFolders = FolderPagination();
                    await MapTagsToSingleFolderUpdateObservable(folderVm);
                    break;
                case MainWindowViewModel.Filters.AllFavoriteFolders:
                    (List<Folder> folders, List<FolderTag> tags) allFavoriteFoldersResult = await _folderMethods.GetAllFavoriteFolders();
                    displayFolders = allFavoriteFoldersResult.folders;
                    displayFolderTags = allFavoriteFoldersResult.tags;
                    displayFolders = FolderPagination();
                    await MapTagsToSingleFolderUpdateObservable(folderVm);
                    break;
                case MainWindowViewModel.Filters.AllFoldersWithNoImportedImages:
                    (List<Folder> folders, List<FolderTag> tags) allFoldersWithNoImportedImagesResult = await _folderMethods.GetAllFoldersWithNoImportedImages(_mainWindowViewModel.FilterInCurrentDirectory, _mainWindowViewModel.CurrentDirectory);
                    displayFolders = allFoldersWithNoImportedImagesResult.folders;
                    displayFolderTags = allFoldersWithNoImportedImagesResult.tags;
                    displayFolders = FolderPagination();
                    await MapTagsToSingleFolderUpdateObservable(folderVm);
                    break;
                case MainWindowViewModel.Filters.AllFoldersWithMetadataNotScanned:
                    (List<Folder> folders, List<FolderTag> tags) allFoldersWithMetadataNotScannedResult = await _folderMethods.GetAllFoldersWithMetadataNotScanned(_mainWindowViewModel.FilterInCurrentDirectory, _mainWindowViewModel.CurrentDirectory);
                    displayFolders = allFoldersWithMetadataNotScannedResult.folders;
                    displayFolderTags = allFoldersWithMetadataNotScannedResult.tags;
                    displayFolders = FolderPagination();
                    await MapTagsToSingleFolderUpdateObservable(folderVm);
                    break;
                case MainWindowViewModel.Filters.AllFoldersWithoutCovers:
                    (List<Folder> folders, List<FolderTag> tags) allFoldersWithoutCoversResult = await _folderMethods.GetAllFoldersWithoutCovers(_mainWindowViewModel.FilterInCurrentDirectory, _mainWindowViewModel.CurrentDirectory);
                    displayFolders = allFoldersWithoutCoversResult.folders;
                    displayFolderTags = allFoldersWithoutCoversResult.tags;
                    displayFolders = FolderPagination();
                    await MapTagsToSingleFolderUpdateObservable(folderVm);
                    break;
            }
            _mainWindowViewModel.ShowLoading = false;
        }

        //loads the previous X elements in CurrentDirectory
        public async void PreviousPage()
        {
            if (_mainWindowViewModel.CurrentFolderPage > 1)
            {
                _mainWindowViewModel.CurrentFolderPage = _mainWindowViewModel.CurrentFolderPage - 1;
                await RefreshFolders();
            }
            if (_mainWindowViewModel.CurrentImagePage > 1)
            {
                _mainWindowViewModel.CurrentImagePage = _mainWindowViewModel.CurrentImagePage - 1;
                await RefreshImages(_mainWindowViewModel.CurrentDirectory);
            }
        }

        //loads the next X elements in CurrentDirectory
        public async void NextPage()
        {
            if (_mainWindowViewModel.CurrentFolderPage < _mainWindowViewModel.TotalFolderPages)
            {
                _mainWindowViewModel.CurrentFolderPage = _mainWindowViewModel.CurrentFolderPage + 1;
                await RefreshFolders();
            }
            if (_mainWindowViewModel.CurrentImagePage < _mainWindowViewModel.TotalImagePages)
            {
                _mainWindowViewModel.CurrentImagePage = _mainWindowViewModel.CurrentImagePage + 1;
                await RefreshImages(_mainWindowViewModel.CurrentDirectory);
            }
        }

        public async Task GoToPage(int pageNumber)
        {
            if (pageNumber <= _mainWindowViewModel.TotalFolderPages)
            {
                _mainWindowViewModel.CurrentFolderPage = pageNumber;
                await RefreshFolders();
            }
            if (pageNumber <= _mainWindowViewModel.TotalImagePages)
            {
                _mainWindowViewModel.CurrentImagePage = pageNumber;
                await RefreshImages(_mainWindowViewModel.CurrentDirectory);
            }
        }
    }
}