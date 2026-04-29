using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using ImagePerfect.Helpers;
using ImagePerfect.Models;
using ImagePerfect.ObjectMappers;
using ImagePerfect.Repository;
using ImagePerfect.Repository.IRepository;
using ImagePerfect.Views;
using Microsoft.Extensions.Configuration;
using MsBox.Avalonia;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Enums;
using MsBox.Avalonia.Models;
using MySqlConnector;
using ReactiveUI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using Image = ImagePerfect.Models.Image;

namespace ImagePerfect.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        private readonly MySqlDataSource _dataSource;
        private readonly IConfiguration _configuration;

        private FiltersWindow? _filtersWindow;
        private SettingsWindow? _settingsWindow;
        private bool _showLoading;
        private bool _suppressImageRefresh = false;
        private int _totalImages = 0;
        private bool _copyFolderTextToParentFolder = true;
        private string _selectedImagesNewDirectory = string.Empty;
        private List<Tag> _tagsList = new List<Tag>();

        //open Window backing fields
        private ReactiveCommand<Unit, Unit> _openSettingsWindowCommand;
        private ReactiveCommand<Unit, Unit> _openFiltersWindowCommand;
        //navigation backing fields
        private ReactiveCommand<FolderViewModel, Task> _nextFolderCommand;
        private ReactiveCommand<FolderViewModel, Task> _backFolderCommand;
        private ReactiveCommand<ImageViewModel, Task> _backFolderFromImageCommand;
        private ReactiveCommand<Unit, Task> _backFolderFromDirectoryOptionsPanelCommand;
        private ReactiveCommand<Unit, Task> _loadCurrentDirectoryCommand;
        private ReactiveCommand<Unit, Task> _nextPageCommand;
        private ReactiveCommand<Unit, Task> _previousPageCommand;
        private ReactiveCommand<decimal, Task> _goToPageCommand;
        //folder backing fields
        private ReactiveCommand<FolderViewModel, Task> _importImagesCommand;
        private ReactiveCommand<FolderViewModel, Task> _addFolderDescriptionCommand;
        private ReactiveCommand<FolderViewModel, Task> _addFolderTagsCommand;
        private ReactiveCommand<FolderViewModel, Task> _editFolderTagsCommand;
        private ReactiveCommand<FolderViewModel, Task> _addFolderRatingCommand;
        private ReactiveCommand<Tag, Task> _removeTagOnAllFoldersCommand;
        private ReactiveCommand<Tag, Task> _addTagToAllFoldersInCurrentDirectoryCommand;
        private ReactiveCommand<FolderViewModel, Task> _moveFolderToTrashCommand;
        private ReactiveCommand<FolderViewModel, Task> _scanFolderImagesForMetaDataCommand;
        private ReactiveCommand<FolderViewModel, Task> _saveFolderAsFavoriteCommand;
        private ReactiveCommand<Unit, Task> _removeAllFavoriteFoldersCommand;
        private ReactiveCommand<FolderViewModel, Task> _copyCoverImageToContainingFolderCommand;
        private ReactiveCommand<FolderViewModel, Task> _copyFolderDescriptionToContainingFolderCommand;
        private ReactiveCommand<Unit, Task> _createNewFolderCommand;
        public MainWindowViewModel() { }
        public MainWindowViewModel(MySqlDataSource dataSource, IConfiguration config)
        {
            _dataSource = dataSource;
            _configuration = config;

            _showLoading = false;

            HistoryVm = new HistoryViewModel(_dataSource, _configuration, this);
            DirectoryNavigationVm = new DirectoryNavigationViewModel(this);
            ExplorerVm = new ExplorerViewModel(_dataSource, _configuration, this);
            ModifyFolderDataVm = new ModifyFolderDataViewModel(_dataSource, _configuration, this);
            ModifyImageDataVm = new ModifyImageDataViewModel(_dataSource, _configuration, this);
            ExternalProgramVm = new ExternalProgramViewModel(this);
            CoverImageVm = new CoverImageViewModel(_dataSource, _configuration, this);
            FolderDescriptionTextFileVm = new FolderDescriptionTextFileViewModel(_dataSource, _configuration, this);
            ScanImagesForMetaDataVm = new ScanImagesForMetaDataViewModel(_dataSource, _configuration, this);
            ImportImagesVm = new ImportImagesViewModel(_dataSource, _configuration, this);
            InitializeVm = new InitializeViewModel(_dataSource, _configuration, this);
            FavoriteFoldersVm = new FavoriteFoldersViewModel(_dataSource, _configuration);
            SettingsVm = new SettingsViewModel(_dataSource, _configuration, this);
            MoveImages = new MoveImagesViewModel(_dataSource, _configuration, this);
            MoveFolderToTrash = new MoveFolderToTrashViewModel(_dataSource, _configuration, this);
            CreateNewFolder = new CreateNewFolderViewModel(_dataSource, _configuration, this);

            InitializeWindowCommands();
            InitializeNavigationCommands();
            InitializeFolderCommands();
            
            AddImageTagsCommand = ReactiveCommand.Create(async (ImageViewModel imageVm) => {
                await ModifyImageDataVm.AddImageTag(imageVm);
            });
            AddMultipleImageTagsCommand = ReactiveCommand.Create(async (ListBox selectedTagsListBox) => {
                await ModifyImageDataVm.AddMultipleImageTags(selectedTagsListBox);
            });
            EditImageTagsCommand = ReactiveCommand.Create(async (ImageViewModel imageVm) => {
                await ModifyImageDataVm.EditImageTag(imageVm);
            });
            AddImageRatingCommand = ReactiveCommand.Create(async (ImageViewModel imageVm) => {
                await ModifyImageDataVm.UpdateImage(imageVm, "Rating");
            });
            RemoveTagOnAllImagesCommand = ReactiveCommand.Create(async (Tag selectedTag) => { 
                await ModifyImageDataVm.RemoveTagOnAllImages(selectedTag);
            });
            DeleteLibraryCommand = ReactiveCommand.Create(async () => {
                await DeleteLibrary();
            });
            OpenImageInExternalViewerCommand = ReactiveCommand.Create(async (ImageViewModel imageVm) => {
                await ExternalProgramVm.OpenImageInExternalViewer(imageVm);
            });
            OpenCurrentDirectoryWithExplorerCommand = ReactiveCommand.Create(async () => {
                await ExternalProgramVm.OpenCurrentDirectoryWithExplorer();
            });
            MoveImageToTrashCommand = ReactiveCommand.Create(async (ImageViewModel imageVm) => {
                await MoveImages.MoveImageToTrash(imageVm);
            });
            ToggleManageImagesCommand = ReactiveCommand.Create(() => {
                ToggleUI.ToggleManageImages();
            });
            ToggleFiltersCommand = ReactiveCommand.Create((string showFilter) => {
                ToggleUI.ToggleFilters(showFilter);
            });
            ToggleCreateNewFolderCommand = ReactiveCommand.Create(() => {
                ToggleUI.ToggleCreateNewFolder();
            });
            ToggleGetTotalImagesCommand = ReactiveCommand.Create(async () => {
                ToggleUI.ToggleGetTotalImages();
                if (ToggleUI.ShowTotalImages == true)
                {
                    await using UnitOfWork uow = await UnitOfWork.CreateAsync(_dataSource, _configuration);
                    ImageMethods imageMethods = new ImageMethods(uow);
                    TotalImages = await imageMethods.GetTotalImages();
                }
            });
            ToggleImportAndScanCommand = ReactiveCommand.Create(() => {
                ToggleUI.ToggleImportAndScan();
            });
            ToggleListAllTagsCommand = ReactiveCommand.Create(() => {
                ToggleUI.ToggleListAllTags();
            });
            ToggleShowExtendedFolderControlsCommand = ReactiveCommand.Create(() => { 
                ToggleUI.ToggleShowExtendedFolderControls();
            });
            ToggleShowExtendedImageControlsCommand = ReactiveCommand.Create(() =>
            {
                ToggleUI.ToggleShowExtendedImageControls();
            });
            FilterGetAllImagesInFolderAndSubFoldersCommand = ReactiveCommand.Create(async () =>
            {
                ExplorerVm.ResetPagination();
                ExplorerVm.currentFilter = ExplorerViewModel.Filters.AllImagesInFolderAndSubFolders;
                await ExplorerVm.RefreshImages();
            });
            FilterImagesOnRatingCommand = ReactiveCommand.Create(async (decimal rating) => {
                ExplorerVm.ResetPagination();
                ExplorerVm.selectedRatingForFilter = Decimal.ToInt32(rating);
                ExplorerVm.currentFilter = ExplorerViewModel.Filters.ImageRatingFilter;
                await ExplorerVm.RefreshImages();
            });
            FilterFiveStarImagesInCurrentDirectoryCommand = ReactiveCommand.Create(async (decimal rating) => {
                ExplorerVm.ResetPagination();
                ExplorerVm.selectedRatingForFilter = Decimal.ToInt32(rating);
                ExplorerVm.currentFilter = ExplorerViewModel.Filters.FiveStarImagesInCurrentDirectory;
                await ExplorerVm.RefreshImages();
            });
            FilterImagesOnYearCommand = ReactiveCommand.Create(async (int year) => { 
                if(year == 0)
                    return;
                ExplorerVm.ResetPagination();
                ExplorerVm.selectedYearForFilter = year;
                ExplorerVm.currentFilter = ExplorerViewModel.Filters.ImageYearFilter;
                await ExplorerVm.RefreshImages();
            });
            FilterImagesOnYearMonthCommand = ReactiveCommand.Create(async (string yearMonth) => {
                if (yearMonth == null)
                    return;
                string[] parts = yearMonth.Split('-');
                int year = int.Parse(parts[0]);
                int month = int.Parse(parts[1]);
                ExplorerVm.ResetPagination();
                ExplorerVm.selectedYearForFilter = year;
                ExplorerVm.selectedMonthForFilter = month;
                ExplorerVm.currentFilter = ExplorerViewModel.Filters.ImageYearMonthFilter;
                await ExplorerVm.RefreshImages();
            });
            FilterImagesOnDateRangeCommand = ReactiveCommand.Create(async (ImageDatesViewModel imageDatesVm) => {
                if (imageDatesVm.StartDate != null && imageDatesVm.EndDate != null) 
                {
                    ExplorerVm.ResetPagination();
                    ExplorerVm.startDateForFilter = (DateTimeOffset)imageDatesVm.StartDate;
                    ExplorerVm.endDateForFilter = (DateTimeOffset)imageDatesVm.EndDate;
                    ExplorerVm.currentFilter = ExplorerViewModel.Filters.ImageDateRangeFilter;
                    await ExplorerVm.RefreshImages();
                }
            });
            FilterFoldersDateModifiedInCurrentDirectoryCommand = ReactiveCommand.Create(async () => {
                ExplorerVm.ResetPagination();
                ExplorerVm.currentFilter = ExplorerViewModel.Filters.FolderDateModifiedFilter;
                await ExplorerVm.RefreshFolders();
            });
            FilterFoldersInCurrentDirectoryByStartingLetterCommand = ReactiveCommand.Create(async (string letter) => {
                ExplorerVm.ResetPagination();
                ExplorerVm.selectedLetterForFilter = letter;
                ExplorerVm.currentFilter = ExplorerViewModel.Filters.FolderAlphabeticalFilter;
                await ExplorerVm.RefreshFolders();
            });
            FilterFolderOnRatingAndTagCommand = ReactiveCommand.Create(async (IList tags) => {
                List<Tag> selectedTags = tags.OfType<Tag>().ToList();
                if (!selectedTags.Any())
                    return;
                List<string> tagsForFilter = selectedTags.Select(t => t.TagName).ToList();
                ExplorerVm.tagsForFilter = tagsForFilter;
                ExplorerVm.ResetPagination();
                ExplorerVm.currentFilter = ExplorerViewModel.Filters.FolderTagAndRatingFilter;
                await ExplorerVm.RefreshFolders();
            });
            FilterFoldersOnRatingCommand = ReactiveCommand.Create(async (decimal rating) => {
                ExplorerVm.ResetPagination();
                ExplorerVm.selectedRatingForFilter = Decimal.ToInt32(rating);
                ExplorerVm.currentFilter = ExplorerViewModel.Filters.FolderRatingFilter;
                await ExplorerVm.RefreshFolders();
            });
            FilterImagesOnTagsCommand = ReactiveCommand.Create(async (IList tags) => {
                List<Tag> selectedTags = tags.OfType<Tag>().ToList();
                if (!selectedTags.Any())
                    return;
                List<string> tagsForFilter = selectedTags.Select(t => t.TagName).ToList();
                ExplorerVm.tagsForImageFilter = tagsForFilter;
                ExplorerVm.ResetPagination();
                ExplorerVm.currentFilter = ExplorerViewModel.Filters.ImageTagsFilter;
                await ExplorerVm.RefreshImages();
            });
            FilterFolderOnTagsCommand = ReactiveCommand.Create(async (IList tags) => 
            {
                List<Tag> selectedTags = tags.OfType<Tag>().ToList();
                if (!selectedTags.Any())
                    return;
                List<string> tagsForFilter = selectedTags.Select(t => t.TagName).ToList();
                ExplorerVm.tagsForFolderFilter = tagsForFilter;
                ExplorerVm.ResetPagination();
                ExplorerVm.currentFilter = ExplorerViewModel.Filters.FolderTagsFilter;
                await ExplorerVm.RefreshFolders();
            });
            FilterFoldersOnDescriptionCommand = ReactiveCommand.Create(async (string text) => {
                ExplorerVm.ResetPagination();
                ExplorerVm.textForFilter = text;
                ExplorerVm.currentFilter = ExplorerViewModel.Filters.FolderDescriptionFilter;
                await ExplorerVm.RefreshFolders();
            });
            FilterFoldersOnDescriptionAndTagsCommand = ReactiveCommand.Create(async (IList tags) =>
            {
                List<Tag> selectedTags = tags.OfType<Tag>().ToList();
                if (!selectedTags.Any() || String.IsNullOrEmpty(ExplorerVm.TextForFolderDescriptionAndTagsFilter))
                    return;
                List<string> tagsForFilter = selectedTags.Select(t => t.TagName).ToList();
                //TextForFolderDescriptionAndTagsFilter is bound to UI tags passed in as IList
                ExplorerVm.tagsForFolderDescriptionAndTagsFilter = tagsForFilter;
                ExplorerVm.ResetPagination();
                ExplorerVm.currentFilter = ExplorerViewModel.Filters.FolderDescriptionAndTagsFilter;
                await ExplorerVm.RefreshFolders();
            });
            UpdateImageDatesCommand = ReactiveCommand.Create(async () => {
                await using UnitOfWork uow = await UnitOfWork.CreateAsync(_dataSource, _configuration);
                ImageMethods imageMethods = new ImageMethods(uow);
                await imageMethods.UpdateImageDates();
                ImageDatesVm = await imageMethods.GetImageDates();
            });
            GetAllFoldersWithNoImportedImagesCommand = ReactiveCommand.Create(async () => {
                ExplorerVm.ResetPagination();
                ExplorerVm.currentFilter = ExplorerViewModel.Filters.AllFoldersWithNoImportedImages;
                await ExplorerVm.RefreshFolders();
            });
            GetAllFoldersWithMetadataNotScannedCommand = ReactiveCommand.Create(async () => {
                ExplorerVm.ResetPagination();
                ExplorerVm.currentFilter = ExplorerViewModel.Filters.AllFoldersWithMetadataNotScanned;
                await ExplorerVm.RefreshFolders();
            });
            GetAllFoldersWithoutCoversCommand = ReactiveCommand.Create(async () => {
                ExplorerVm.ResetPagination();
                ExplorerVm.currentFilter = ExplorerViewModel.Filters.AllFoldersWithoutCovers;
                await ExplorerVm.RefreshFolders();
            });
            
            PickImageWidthCommand = ReactiveCommand.Create(async (string size) => {
                await SettingsVm.PickImageWidth(size);
            });
            SelectImageWidthCommand = ReactiveCommand.Create(async (decimal size) => { 
                await SettingsVm.SelectImageWidth(size);
            });
            PickFolderPageSizeCommand = ReactiveCommand.Create(async (string size) => {
                await SettingsVm.PickFolderPageSize(size);
            });
            PickImagePageSizeCommand = ReactiveCommand.Create(async (string size) => {
                await SettingsVm.PickImagePageSize(size);
            });
            PickHistoryPointsSizeCommand = ReactiveCommand.Create(async (string size) => { 
                await SettingsVm.PickHistoryPointsSize(size);
            });
            SaveDirectoryToHistoryCommand = ReactiveCommand.Create(async (ScrollViewer scrollViewer) => { 
                await HistoryVm.SaveDirectoryToHistory(scrollViewer, false);
            });
            SaveDirectoryCommand = ReactiveCommand.Create(async (ScrollViewer scrollViewer) => {
                await HistoryVm.SaveDirectoryToHistory(scrollViewer, true);
            });
            LoadSavedDirectoryCommand = ReactiveCommand.Create(async (ScrollViewer scrollViewer) => {
                await HistoryVm.LoadMainSavedDirectory(scrollViewer);
            });
            GetAllFavoriteFoldersCommand = ReactiveCommand.Create(async () => {
                ExplorerVm.ResetPagination();
                ExplorerVm.currentFilter = ExplorerViewModel.Filters.AllFavoriteFolders;
                await ExplorerVm.RefreshFolders();
            });
            MoveSelectedImagesToTrashCommand = ReactiveCommand.Create(async (IList selectedImages) =>
            {
                await MoveImages.MoveSelectedImagesToTrash(selectedImages);
            });
            MoveSelectedImagesUpOneDirectoryCommand = ReactiveCommand.Create(async (IList selectedImages) => {
                await MoveImages.MoveSelectedImageUpOneDirectory(selectedImages);
            });
            MoveAllImagesInFolderUpOneDirectoryCommand = ReactiveCommand.Create(async (FolderViewModel folderVm) =>
            {
                await MoveImages.MoveAllImagesInFolderUpOneDirectory(folderVm);
            });
            ImportAllFoldersOnCurrentPageCommand = ReactiveCommand.Create(async (ItemsControl foldersItemsControl) => { 
                await ImportImagesVm.ImportAllFoldersOnCurrentPage(foldersItemsControl);
            });
            AddCoverImageOnCurrentPageCommand = ReactiveCommand.Create(async (ItemsControl folderItemsControl) => { 
                await CoverImageVm.AddCoverImageOnCurrentPage(folderItemsControl);
            });
            ScanAllFoldersOnCurrentPageCommand = ReactiveCommand.Create(async (ItemsControl foldersItemsControl) => {
                await ScanImagesForMetaDataVm.ScanAllFoldersOnCurrentPage(foldersItemsControl);
            });
            GetFolderDescriptionFromTextFileOnCurrentPageCommand = ReactiveCommand.Create(async (ItemsControl folderItemsControl) => { 
                await FolderDescriptionTextFileVm.GetFolderDescriptionFromTextFileOnCurrentPage(folderItemsControl);
            });
            BackUpFolderDescriptionToTextFileOnCurrentPageCommand = ReactiveCommand.Create(async (ItemsControl folderItemsControl) => {
                await FolderDescriptionTextFileVm.BackUpFolderDescriptionToTextFileOnCurrentPage(folderItemsControl);
            });
            ExitAppCommand = ReactiveCommand.Create(() => { 
                ExitApp();
            });
            //_ = InitializeVm.Initialize() -- kicks off the async work but doesn't block the constructor
            _ = InitializeVm.Initialize();
        }

        private void InitializeWindowCommands()
        {
            _openSettingsWindowCommand = ReactiveCommand.Create(() =>
            {
                /*
                 * No window yet -> create a new one
                 * OR
                 * Window existed but is now closed -> create a new one
                 */
                if (_settingsWindow == null || !_settingsWindow.IsVisible)
                {
                    _settingsWindow = new SettingsWindow(this);

                    // subscribe to close and reset reference when window closes
                    _settingsWindow.Closed += (_, _) => _settingsWindow = null;

                    _settingsWindow.Show(); // Non-modal, user can continue to use MainWindow
                }
                else
                {
                    // If already open, un-minimize it and bring it to the front
                    if (_settingsWindow.WindowState == WindowState.Minimized)
                    {
                        _settingsWindow.WindowState = WindowState.Normal;
                    }
                    _settingsWindow.Activate();
                }
            });
            _openFiltersWindowCommand = ReactiveCommand.Create(() => {
                /*
                 * No window yet -> create a new one
                 * OR
                 * Window existed but is now closed -> create a new one
                 */
                if (_filtersWindow == null || !_filtersWindow.IsVisible)
                {
                    _filtersWindow = new FiltersWindow(this);

                    // subscribe to close and reset reference when window closes
                    _filtersWindow.Closed += (_, _) => _filtersWindow = null;

                    _filtersWindow.Show(); // Non-modal, user can continue to use MainWindow
                }
                else
                {
                    // If already open, un-minimize it and bring it to the front
                    if (_filtersWindow.WindowState == WindowState.Minimized)
                    {
                        _filtersWindow.WindowState = WindowState.Normal;
                    }
                    _filtersWindow.Activate();
                }
            });
        }

        private void InitializeNavigationCommands()
        {
            _nextFolderCommand = ReactiveCommand.Create(async (FolderViewModel currentFolder) => {
                await DirectoryNavigationVm.NextFolder(currentFolder);
            });
            _backFolderCommand = ReactiveCommand.Create(async (FolderViewModel currentFolder) => {
                await DirectoryNavigationVm.BackFolder(currentFolder);
            });
            _backFolderFromImageCommand = ReactiveCommand.Create(async (ImageViewModel imageVm) => {
                await DirectoryNavigationVm.BackFolderFromImage(imageVm);
            });
            _backFolderFromDirectoryOptionsPanelCommand = ReactiveCommand.Create(async () => {
                await DirectoryNavigationVm.BackFolderFromDirectoryOptionsPanel();
            });
            _loadCurrentDirectoryCommand = ReactiveCommand.Create(async () => {
                await DirectoryNavigationVm.LoadCurrentDirectory();
            });
            _nextPageCommand = ReactiveCommand.Create(async () => {
                await ExplorerVm.NextPage();
            });
            _previousPageCommand = ReactiveCommand.Create(async () => {
                await ExplorerVm.PreviousPage();
            });
            _goToPageCommand = ReactiveCommand.Create(async (decimal pageNumber) => {
                await ExplorerVm.GoToPage(Decimal.ToInt32(pageNumber));
            });
        }

        private void InitializeFolderCommands()
        {
            _importImagesCommand = ReactiveCommand.Create(async (FolderViewModel imageFolder) => {
                await ImportImagesVm.ImportImages(imageFolder, false);
            });
            _addFolderDescriptionCommand = ReactiveCommand.Create(async (FolderViewModel folderVm) => {
                await ModifyFolderDataVm.UpdateFolder(folderVm, "Description");
            });
            _addFolderTagsCommand = ReactiveCommand.Create(async (FolderViewModel folderVm) => {
                await ModifyFolderDataVm.AddFolderTag(folderVm);
            });
            _editFolderTagsCommand = ReactiveCommand.Create(async (FolderViewModel folderVm) => {
                await ModifyFolderDataVm.EditFolderTag(folderVm);
            });
            _addFolderRatingCommand = ReactiveCommand.Create(async (FolderViewModel folderVm) => {
                await ModifyFolderDataVm.UpdateFolder(folderVm, "Rating");
            });
            _removeTagOnAllFoldersCommand = ReactiveCommand.Create(async (Tag selectedTag) => {
                await ModifyFolderDataVm.RemoveTagOnAllFolders(selectedTag);
            });
            _addTagToAllFoldersInCurrentDirectoryCommand = ReactiveCommand.Create(async (Tag selectedTag) => {
                await ModifyFolderDataVm.AddTagToAllFoldersInCurrentDirectory(selectedTag);
            });
            _moveFolderToTrashCommand = ReactiveCommand.Create(async (FolderViewModel folderVm) => {
                await MoveFolderToTrash.MoveFolderToTrash(folderVm);
            });
            _scanFolderImagesForMetaDataCommand = ReactiveCommand.Create(async (FolderViewModel folderVm) => {
                await ScanImagesForMetaDataVm.ScanFolderImagesForMetaData(folderVm, false);
            });
            _saveFolderAsFavoriteCommand = ReactiveCommand.Create(async (FolderViewModel folderVm) => {
                await FavoriteFoldersVm.SaveFolderAsFavorite(folderVm);
            });
            _removeAllFavoriteFoldersCommand = ReactiveCommand.Create(async () => {
                await FavoriteFoldersVm.RemoveAllFavoriteFolders();
            });
            _copyCoverImageToContainingFolderCommand = ReactiveCommand.Create(async (FolderViewModel folderVm) => {
                await CoverImageVm.CopyCoverImageToContainingFolder(folderVm);
            });
            _copyFolderDescriptionToContainingFolderCommand = ReactiveCommand.Create(async (FolderViewModel folderVm) => {
                await FolderDescriptionTextFileVm.CopyFolderDescriptionToContainingFolder(folderVm);
            });
            _createNewFolderCommand = ReactiveCommand.Create(async () => {
                await CreateNewFolder.CreateNewFolder();
            });
        }
        public int TotalImages
        {
            get => _totalImages;
            set => this.RaiseAndSetIfChanged(ref _totalImages, value);  
        }
        public List<Tag> TagsList
        {
            get => _tagsList;
            set => this.RaiseAndSetIfChanged(ref _tagsList, value); 
        }
        public bool ShowLoading
        {
            get => _showLoading;
            set => this.RaiseAndSetIfChanged(ref _showLoading, value);
        }
       
        public bool SuppressImageRefresh
        {
            get => _suppressImageRefresh;
            set => this.RaiseAndSetIfChanged(ref _suppressImageRefresh, value);
        }
        public string SelectedImagesNewDirectory
        {
            get => _selectedImagesNewDirectory;
            set => this.RaiseAndSetIfChanged(ref _selectedImagesNewDirectory, value);
        }

        public bool CopyFolderTextToParentFolder
        {
            get => _copyFolderTextToParentFolder;
            set => this.RaiseAndSetIfChanged(ref _copyFolderTextToParentFolder, value);
        }

        private ImageDatesViewModel _imageDatesVm = new ImageDatesViewModel();
        public ImageDatesViewModel ImageDatesVm
        {
            get => _imageDatesVm;
            set => this.RaiseAndSetIfChanged(ref _imageDatesVm, value);
        }

        public HistoryViewModel HistoryVm { get; }
        public ExplorerViewModel ExplorerVm { get; }
        public DirectoryNavigationViewModel DirectoryNavigationVm { get; }
        public ModifyFolderDataViewModel ModifyFolderDataVm { get; }
        public ModifyImageDataViewModel ModifyImageDataVm { get; }
        public ExternalProgramViewModel ExternalProgramVm { get; }
        public CoverImageViewModel CoverImageVm { get; }
        public FolderDescriptionTextFileViewModel FolderDescriptionTextFileVm { get; }
        public ScanImagesForMetaDataViewModel ScanImagesForMetaDataVm { get; }
        public ImportImagesViewModel ImportImagesVm { get; }
        public InitializeViewModel InitializeVm { get; }
        public FavoriteFoldersViewModel FavoriteFoldersVm { get; }
        public SettingsViewModel SettingsVm { get; }
        public MoveImagesViewModel MoveImages { get; }
        public MoveFolderToTrashViewModel MoveFolderToTrash { get; }
        public CreateNewFolderViewModel CreateNewFolder { get; }
        public ToggleUIViewModel ToggleUI { get; } = new ToggleUIViewModel();

        //pass in this MainWindowViewModel so we can refresh UI
        public PickRootFolderViewModel PickRootFolder { get => new PickRootFolderViewModel(_dataSource, _configuration, this); }

        public PickNewFoldersViewModel PickNewFolders { get => new PickNewFoldersViewModel(_dataSource, _configuration, this); }

        public PickFoldersToExtractZipsViewModel PickZipFolders { get => new PickFoldersToExtractZipsViewModel(_dataSource, _configuration, this); }

        public PickMoveToFolderViewModel PickMoveToFolder { get => new PickMoveToFolderViewModel(_dataSource, _configuration, this); }

        public PickImageMoveToFolderViewModel PickImageMoveToFolder { get => new PickImageMoveToFolderViewModel(_dataSource, _configuration, this); }

        public PickFolderCoverImageViewModel PickCoverImage { get => new PickFolderCoverImageViewModel(_dataSource, _configuration, this); }

        public PickExternalImageViewerExeViewModel PickExternalImageViewerExe { get => new PickExternalImageViewerExeViewModel(this); }

        private ObservableCollection<FolderViewModel> _libraryFolders = new();
        public ObservableCollection<FolderViewModel> LibraryFolders
        {
            get => _libraryFolders;
            set => this.RaiseAndSetIfChanged(ref _libraryFolders, value);
        }

        private ObservableCollection<ImageViewModel> _images = new();
        public ObservableCollection<ImageViewModel> Images
        { 
            get => _images;
            set => this.RaiseAndSetIfChanged(ref _images, value);
        }

        //Open Window Commands
        public ReactiveCommand<Unit, Unit> OpenSettingsWindowCommand { get => _openSettingsWindowCommand; }
        public ReactiveCommand<Unit, Unit> OpenFiltersWindowCommand { get => _openFiltersWindowCommand; }

        //Navigation Commands
        public ReactiveCommand<FolderViewModel, Task> NextFolderCommand { get => _nextFolderCommand; }

        public ReactiveCommand<FolderViewModel, Task> BackFolderCommand { get => _backFolderCommand; }

        public ReactiveCommand<ImageViewModel, Task> BackFolderFromImageCommand { get => _backFolderFromImageCommand; }

        public ReactiveCommand<Unit, Task> BackFolderFromDirectoryOptionsPanelCommand { get => _backFolderFromDirectoryOptionsPanelCommand; }

        public ReactiveCommand<Unit, Task> LoadCurrentDirectoryCommand { get => _loadCurrentDirectoryCommand; }

        public ReactiveCommand<Unit, Task> NextPageCommand { get => _nextPageCommand; }

        public ReactiveCommand<Unit, Task> PreviousPageCommand { get => _previousPageCommand; }

        public ReactiveCommand<decimal, Task> GoToPageCommand { get => _goToPageCommand; }

        //Folder Commands
        public ReactiveCommand<FolderViewModel, Task> ImportImagesCommand { get => _importImagesCommand; }

        public ReactiveCommand<FolderViewModel, Task> AddFolderDescriptionCommand { get => _addFolderDescriptionCommand; }

        public ReactiveCommand<FolderViewModel, Task> AddFolderTagsCommand { get => _addFolderTagsCommand; }

        public ReactiveCommand<FolderViewModel, Task> EditFolderTagsCommand { get => _editFolderTagsCommand; }

        public ReactiveCommand<FolderViewModel, Task> AddFolderRatingCommand { get => _addFolderRatingCommand; }

        public ReactiveCommand<Tag, Task> RemoveTagOnAllFoldersCommand { get => _removeTagOnAllFoldersCommand; }

        public ReactiveCommand<Tag, Task> AddTagToAllFoldersInCurrentDirectoryCommand { get => _addTagToAllFoldersInCurrentDirectoryCommand; }

        public ReactiveCommand<FolderViewModel, Task> MoveFolderToTrashCommand { get => _moveFolderToTrashCommand; }

        public ReactiveCommand<FolderViewModel, Task> ScanFolderImagesForMetaDataCommand { get => _scanFolderImagesForMetaDataCommand; }

        public ReactiveCommand<FolderViewModel, Task> SaveFolderAsFavoriteCommand { get => _saveFolderAsFavoriteCommand; }

        public ReactiveCommand<Unit, Task> RemoveAllFavoriteFoldersCommand { get => _removeAllFavoriteFoldersCommand; }

        public ReactiveCommand<FolderViewModel, Task> CopyCoverImageToContainingFolderCommand { get => _copyCoverImageToContainingFolderCommand; }

        public ReactiveCommand<FolderViewModel, Task> CopyFolderDescriptionToContainingFolderCommand { get => _copyFolderDescriptionToContainingFolderCommand; }

        public ReactiveCommand<Unit, Task> CreateNewFolderCommand { get => _createNewFolderCommand; }

        //Image Command

        public ReactiveCommand<ImageViewModel, Task> AddImageTagsCommand { get; }

        public ReactiveCommand<ListBox, Task> AddMultipleImageTagsCommand { get; }

        public ReactiveCommand<ImageViewModel, Task> EditImageTagsCommand { get; }

        public ReactiveCommand<ImageViewModel, Task> AddImageRatingCommand { get; }

        public ReactiveCommand<Tag, Task> RemoveTagOnAllImagesCommand { get; }

        public ReactiveCommand<Unit, Task> DeleteLibraryCommand { get; }

        public ReactiveCommand<ImageViewModel, Task> OpenImageInExternalViewerCommand { get; }

        public ReactiveCommand<Unit, Task> OpenCurrentDirectoryWithExplorerCommand { get; }

        public ReactiveCommand<ImageViewModel, Task> MoveImageToTrashCommand { get; }

        public ReactiveCommand<string, Unit> ToggleFiltersCommand { get; }

        public ReactiveCommand<Unit, Unit> ToggleManageImagesCommand { get; }

        public ReactiveCommand<Unit, Unit> ToggleCreateNewFolderCommand { get; }

        public ReactiveCommand<Unit, Task> ToggleGetTotalImagesCommand { get; }

        public ReactiveCommand<Unit, Unit> ToggleImportAndScanCommand { get; }

        public ReactiveCommand<Unit, Unit> ToggleListAllTagsCommand { get; }

        public ReactiveCommand<Unit, Unit> ToggleShowExtendedFolderControlsCommand { get; }

        public ReactiveCommand<Unit, Unit> ToggleShowExtendedImageControlsCommand { get; }

        public ReactiveCommand<Unit, Task> FilterGetAllImagesInFolderAndSubFoldersCommand {  get; }

        public ReactiveCommand<decimal, Task> FilterImagesOnRatingCommand { get; }

        public ReactiveCommand<decimal, Task> FilterFiveStarImagesInCurrentDirectoryCommand { get; }

        public ReactiveCommand<int, Task> FilterImagesOnYearCommand { get; }

        public ReactiveCommand<string, Task> FilterImagesOnYearMonthCommand {  get; }

        public ReactiveCommand<ImageDatesViewModel, Task> FilterImagesOnDateRangeCommand { get; }

        public ReactiveCommand<Unit, Task> FilterFoldersDateModifiedInCurrentDirectoryCommand { get; }

        public ReactiveCommand<string, Task> FilterFoldersInCurrentDirectoryByStartingLetterCommand { get; }

        public ReactiveCommand<IList, Task> FilterFolderOnRatingAndTagCommand { get; }

        public ReactiveCommand<decimal, Task> FilterFoldersOnRatingCommand { get; }

        public ReactiveCommand<IList, Task> FilterImagesOnTagsCommand { get; }

        public ReactiveCommand<IList, Task> FilterFolderOnTagsCommand { get; }

        public ReactiveCommand<string, Task> FilterFoldersOnDescriptionCommand { get; }

        public ReactiveCommand<IList, Task> FilterFoldersOnDescriptionAndTagsCommand { get; }

        public ReactiveCommand<Unit, Task> UpdateImageDatesCommand { get; }

        public ReactiveCommand<Unit, Task> GetAllFoldersWithNoImportedImagesCommand { get; }

        public ReactiveCommand<Unit, Task> GetAllFoldersWithMetadataNotScannedCommand { get; }

        public ReactiveCommand<Unit, Task> GetAllFoldersWithoutCoversCommand { get; }

        public ReactiveCommand<string, Task> PickImageWidthCommand { get; }
        
        public ReactiveCommand<decimal, Task> SelectImageWidthCommand { get; }

        public ReactiveCommand<string, Task> PickFolderPageSizeCommand { get; }

        public ReactiveCommand<string, Task> PickImagePageSizeCommand { get; }

        public ReactiveCommand<string, Task> PickHistoryPointsSizeCommand {  get; }

        public ReactiveCommand<ScrollViewer, Task> SaveDirectoryToHistoryCommand { get; }

        public ReactiveCommand<ScrollViewer, Task> SaveDirectoryCommand { get; }

        public ReactiveCommand<ScrollViewer, Task> LoadSavedDirectoryCommand { get; }

        public ReactiveCommand<Unit, Task> GetAllFavoriteFoldersCommand {  get; } 

        public ReactiveCommand<IList, Task> MoveSelectedImagesToTrashCommand { get; }

        public ReactiveCommand<IList, Task> MoveSelectedImagesUpOneDirectoryCommand { get; }

        public ReactiveCommand<FolderViewModel, Task> MoveAllImagesInFolderUpOneDirectoryCommand { get; }

        public ReactiveCommand<ItemsControl, Task> ImportAllFoldersOnCurrentPageCommand { get; }

        public ReactiveCommand<ItemsControl, Task> AddCoverImageOnCurrentPageCommand { get; }

        public ReactiveCommand<ItemsControl, Task> GetFolderDescriptionFromTextFileOnCurrentPageCommand { get; }

        public ReactiveCommand<ItemsControl, Task> BackUpFolderDescriptionToTextFileOnCurrentPageCommand { get; }

        public ReactiveCommand<ItemsControl, Task> ScanAllFoldersOnCurrentPageCommand { get; }

        public ReactiveCommand<Unit, Unit> ExitAppCommand { get; }

        //should technically have its own repo but only plan on having only this one method just keeping it in images repo.
        public async Task GetTagsList(UnitOfWork? uow = null)
        {
            if (uow != null)
            {
                // Use the provided UnitOfWork
                var imageMethods = new ImageMethods(uow);
                TagsList = await imageMethods.GetTagsList();
                return;
            }
            // Create and dispose automatically using await using
            await using UnitOfWork localUow = await UnitOfWork.CreateAsync(_dataSource, _configuration);
            ImageMethods localImageMethods = new ImageMethods(localUow);
            TagsList = await localImageMethods.GetTagsList();
        }
        private void ExitApp()
        {
            //Application.Current.ApplicationLifetime;
            if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown();
            }
        }
       
        private async Task DeleteLibrary()
        {
            var boxYesNo = MessageBoxManager.GetMessageBoxCustom(
                new MessageBoxCustomParams
                {
                    ButtonDefinitions = new List<ButtonDefinition>
                        {
                            new ButtonDefinition { Name = "Yes", },
                            new ButtonDefinition { Name = "No", },
                        },
                    ContentTitle = "Delete Library",
                    ContentMessage = $"Are you sure you want to delete your library? The images on the file system will remain.",
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    SizeToContent = SizeToContent.WidthAndHeight,  // <-- lets it grow with content
                    MinWidth = 500  // optional, so it doesn’t wrap too soon
                }
            );
            var boxResult = await boxYesNo.ShowWindowDialogAsync(Globals.MainWindow);
            if (boxResult == "Yes")
            {
                //remove all folders -- this will drop images as well. 
                await using UnitOfWork uow = await UnitOfWork.CreateAsync(_dataSource, _configuration);
                FolderMethods folderMethods = new FolderMethods(uow);
                bool success = await folderMethods.DeleteAllFolders();
                if (success) 
                {
                    //refresh UI
                    LibraryFolders = new ObservableCollection<FolderViewModel>();
                    Images = new ObservableCollection<ImageViewModel>();
                    InitializeVm.HasRootLibrary = false;
                }
            }
            else 
            {
                return;
            }
        }
    }
}
