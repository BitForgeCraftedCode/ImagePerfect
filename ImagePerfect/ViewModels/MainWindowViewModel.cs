using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using ImagePerfect.Helpers;
using ImagePerfect.Models;
using ImagePerfect.ObjectMappers;
using ImagePerfect.Repository.IRepository;
using MsBox.Avalonia;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Enums;
using MsBox.Avalonia.Models;
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
        private readonly IUnitOfWork _unitOfWork;
        private readonly FolderMethods _folderMethods;
        private readonly ImageMethods _imageMethods;
        private bool _showLoading;
        private bool _suppressImageRefresh = false;
        private int _totalImages = 0;
        private bool _copyFolderTextToParentFolder = true;
        private string _selectedImagesNewDirectory = string.Empty;
        private List<Tag> _tagsList = new List<Tag>();

        public MainWindowViewModel() { }
        public MainWindowViewModel(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
            _folderMethods = new FolderMethods(_unitOfWork);
            _imageMethods = new ImageMethods(_unitOfWork);
            _showLoading = false;

            DirectoryNavigationVm = new DirectoryNavigationViewModel(this);
            ExplorerVm = new ExplorerViewModel(_unitOfWork, this);
            ModifyFolderDataVm = new ModifyFolderDataViewModel(_unitOfWork, this);
            ModifyImageDataVm = new ModifyImageDataViewModel(_unitOfWork, this);
            ExternalProgramVm = new ExternalProgramViewModel(this);
            CoverImageVm = new CoverImageViewModel(_unitOfWork, this);
            FolderDescriptionTextFileVm = new FolderDescriptionTextFileViewModel(_unitOfWork, this);
            ScanImagesForMetaDataVm = new ScanImagesForMetaDataViewModel(_unitOfWork, this);
            ImportImagesVm = new ImportImagesViewModel(_unitOfWork, this);
            InitializeVm = new InitializeViewModel(_unitOfWork, this);
            SavedDirectoryVm = new SavedDirectoryViewModel(_unitOfWork, this);
            FavoriteFoldersVm = new FavoriteFoldersViewModel(_unitOfWork);
            SettingsVm = new SettingsViewModel(_unitOfWork, this);
            MoveImages = new MoveImagesViewModel(_unitOfWork, this);
            MoveFolderToTrash = new MoveFolderToTrashViewModel(_unitOfWork, this);
            CreateNewFolder = new CreateNewFolderViewModel(_unitOfWork, this);

            NextFolderCommand = ReactiveCommand.Create(async (FolderViewModel currentFolder) => {
                await DirectoryNavigationVm.NextFolder(currentFolder);
            });
            BackFolderCommand = ReactiveCommand.Create(async (FolderViewModel currentFolder) => {
                await DirectoryNavigationVm.BackFolder(currentFolder);
            });
            BackFolderFromImageCommand = ReactiveCommand.Create(async (ImageViewModel imageVm) => {
                await DirectoryNavigationVm.BackFolderFromImage(imageVm);
            });
            BackFolderFromDirectoryOptionsPanelCommand = ReactiveCommand.Create(async () => {
                await DirectoryNavigationVm.BackFolderFromDirectoryOptionsPanel();
            });
            ImportImagesCommand = ReactiveCommand.Create(async (FolderViewModel imageFolder) => {
                await ImportImagesVm.ImportImages(imageFolder, false);
            });
            AddFolderDescriptionCommand = ReactiveCommand.Create((FolderViewModel folderVm) => {
                ModifyFolderDataVm.UpdateFolder(folderVm, "Description");
            });
            AddFolderTagsCommand = ReactiveCommand.Create((FolderViewModel folderVm) => {
                ModifyFolderDataVm.AddFolderTag(folderVm);
            });
            EditFolderTagsCommand = ReactiveCommand.Create((FolderViewModel folderVm) => {
                ModifyFolderDataVm.EditFolderTag(folderVm);
            });
            AddFolderRatingCommand = ReactiveCommand.Create((FolderViewModel folderVm) => {
                ModifyFolderDataVm.UpdateFolder(folderVm, "Rating");
            });
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
            RemoveTagOnAllFoldersCommand = ReactiveCommand.Create(async (Tag selectedTag) => { 
                await ModifyFolderDataVm.RemoveTagOnAllFolders(selectedTag);
            });
            AddTagToAllFoldersInCurrentDirectoryCommand = ReactiveCommand.Create(async (Tag selectedTag) => { 
                await ModifyFolderDataVm.AddTagToAllFoldersInCurrentDirectory(selectedTag);
            });
            DeleteLibraryCommand = ReactiveCommand.Create(() => {
                DeleteLibrary();
            });
            OpenImageInExternalViewerCommand = ReactiveCommand.Create((ImageViewModel imageVm) => {
                ExternalProgramVm.OpenImageInExternalViewer(imageVm);
            });
            OpenCurrentDirectoryWithExplorerCommand = ReactiveCommand.Create(() => {
                ExternalProgramVm.OpenCurrentDirectoryWithExplorer();
            });
            MoveImageToTrashCommand = ReactiveCommand.Create(async (ImageViewModel imageVm) => {
                await MoveImages.MoveImageToTrash(imageVm);
            });
            MoveFolderToTrashCommand = ReactiveCommand.Create(async (FolderViewModel folderVm) => {
                await MoveFolderToTrash.MoveFolderToTrash(folderVm);
            });
            ScanFolderImagesForMetaDataCommand = ReactiveCommand.Create(async (FolderViewModel folderVm) => {
                await ScanImagesForMetaDataVm.ScanFolderImagesForMetaData(folderVm, false);
            });
            NextPageCommand = ReactiveCommand.Create(async () => {
                await ExplorerVm.NextPage();
            });
            PreviousPageCommand = ReactiveCommand.Create(async () => {
                await ExplorerVm.PreviousPage();
            });
            GoToPageCommand = ReactiveCommand.Create(async (decimal pageNumber) => {
                await ExplorerVm.GoToPage(Decimal.ToInt32(pageNumber));
            });
            ToggleSettingsCommand = ReactiveCommand.Create(() => {
                ToggleUI.ToggleSettings();
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
                    TotalImages = await _imageMethods.GetTotalImages();
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
            FilterFoldersInCurrentDirectoryByStartingLetterCommand = ReactiveCommand.Create(async (string letter) => {
                ExplorerVm.ResetPagination();
                ExplorerVm.selectedLetterForFilter = letter;
                ExplorerVm.currentFilter = ExplorerViewModel.Filters.FolderAlphabeticalFilter;
                await ExplorerVm.RefreshFolders();
            });
            FilterFolderOnRatingAndTagCommand = ReactiveCommand.Create(async () => {
                if (!string.IsNullOrEmpty(ExplorerVm.ComboFolderFilterTagOne))
                {
                    ExplorerVm.ResetPagination();
                    ExplorerVm.currentFilter = ExplorerViewModel.Filters.FolderTagAndRatingFilter;
                    await ExplorerVm.RefreshFolders();
                }
            });
            FilterFoldersOnRatingCommand = ReactiveCommand.Create(async (decimal rating) => {
                ExplorerVm.ResetPagination();
                ExplorerVm.selectedRatingForFilter = Decimal.ToInt32(rating);
                ExplorerVm.currentFilter = ExplorerViewModel.Filters.FolderRatingFilter;
                await ExplorerVm.RefreshFolders();
            });
            FilterImagesOnTagCommand = ReactiveCommand.Create(async (string tag) => {
                ExplorerVm.ResetPagination();
                ExplorerVm.tagForFilter = tag;
                ExplorerVm.currentFilter = ExplorerViewModel.Filters.ImageTagFilter;
                await ExplorerVm.RefreshImages();
            });
            FilterFoldersOnTagCommand = ReactiveCommand.Create(async (string tag) => {
                ExplorerVm.ResetPagination();
                ExplorerVm.tagForFilter = tag;
                ExplorerVm.currentFilter = ExplorerViewModel.Filters.FolderTagFilter;
                await ExplorerVm.RefreshFolders();
            });
            FilterFoldersOnDescriptionCommand = ReactiveCommand.Create(async (string text) => {
                ExplorerVm.ResetPagination();
                ExplorerVm.textForFilter = text;
                ExplorerVm.currentFilter = ExplorerViewModel.Filters.FolderDescriptionFilter;
                await ExplorerVm.RefreshFolders();
            });
            UpdateImageDatesCommand = ReactiveCommand.Create(async () => { 
                await _imageMethods.UpdateImageDates();
                ImageDatesVm = await _imageMethods.GetImageDates();
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
            LoadCurrentDirectoryCommand = ReactiveCommand.Create(async () => {
                await DirectoryNavigationVm.LoadCurrentDirectory();
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
            SaveDirectoryCommand = ReactiveCommand.Create(async (ScrollViewer scrollViewer) => {
                await SavedDirectoryVm.SaveDirectory(scrollViewer);
            });
            LoadSavedDirectoryCommand = ReactiveCommand.Create(async (ScrollViewer scrollViewer) => {
                await SavedDirectoryVm.LoadSavedDirectory(scrollViewer);
            });
            SaveFolderAsFavoriteCommand = ReactiveCommand.Create(async (FolderViewModel folderVm) => {
                await FavoriteFoldersVm.SaveFolderAsFavorite(folderVm);
            });
            GetAllFavoriteFoldersCommand = ReactiveCommand.Create(async () => {
                ExplorerVm.ResetPagination();
                ExplorerVm.currentFilter = ExplorerViewModel.Filters.AllFavoriteFolders;
                await ExplorerVm.RefreshFolders();
            });
            RemoveAllFavoriteFoldersCommand = ReactiveCommand.Create(async () => {
                await FavoriteFoldersVm.RemoveAllFavoriteFolders();
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
            CopyCoverImageToContainingFolderCommand = ReactiveCommand.Create(async (FolderViewModel folderVm) => { 
                await CoverImageVm.CopyCoverImageToContainingFolder(folderVm);
            });
            GetFolderDescriptionFromTextFileOnCurrentPageCommand = ReactiveCommand.Create(async (ItemsControl folderItemsControl) => { 
                await FolderDescriptionTextFileVm.GetFolderDescriptionFromTextFileOnCurrentPage(folderItemsControl);
            });
            BackUpFolderDescriptionToTextFileOnCurrentPageCommand = ReactiveCommand.Create(async (ItemsControl folderItemsControl) => {
                await FolderDescriptionTextFileVm.BackUpFolderDescriptionToTextFileOnCurrentPage(folderItemsControl);
            });
            CopyFolderDescriptionToContainingFolderCommand = ReactiveCommand.Create(async (FolderViewModel folderVm) => {
                await FolderDescriptionTextFileVm.CopyFolderDescriptionToContainingFolder(folderVm);
            });
            CreateNewFolderCommand = ReactiveCommand.Create(async () => {
                await CreateNewFolder.CreateNewFolder();
            });
            ExitAppCommand = ReactiveCommand.Create(() => { 
                ExitApp();
            });
            InitializeVm.Initialize();
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
        public SavedDirectoryViewModel SavedDirectoryVm { get; }
        public FavoriteFoldersViewModel FavoriteFoldersVm { get; }
        public SettingsViewModel SettingsVm { get; }
        public MoveImagesViewModel MoveImages { get; }
        public MoveFolderToTrashViewModel MoveFolderToTrash { get; }
        public CreateNewFolderViewModel CreateNewFolder { get; }
        public ToggleUIViewModel ToggleUI { get; } = new ToggleUIViewModel();

        //pass in this MainWindowViewModel so we can refresh UI
        public PickRootFolderViewModel PickRootFolder { get => new PickRootFolderViewModel(_unitOfWork, this); }

        public PickNewFoldersViewModel PickNewFolders { get => new PickNewFoldersViewModel(_unitOfWork, this); }

        public PickFoldersToExtractZipsViewModel PickZipFolders { get => new PickFoldersToExtractZipsViewModel(_unitOfWork, this); }

        public PickMoveToFolderViewModel PickMoveToFolder { get => new PickMoveToFolderViewModel(_unitOfWork, this); }

        public PickImageMoveToFolderViewModel PickImageMoveToFolder { get => new PickImageMoveToFolderViewModel(_unitOfWork, this); }

        public PickFolderCoverImageViewModel PickCoverImage { get => new PickFolderCoverImageViewModel(_unitOfWork, this); }

        public PickExternalImageViewerExeViewModel PickExternalImageViewerExe { get => new PickExternalImageViewerExeViewModel(_unitOfWork, this); }

        public ObservableCollection<FolderViewModel> LibraryFolders { get; } = new ObservableCollection<FolderViewModel>();
       
        public ObservableCollection<ImageViewModel> Images { get; } = new ObservableCollection<ImageViewModel>();

        public ReactiveCommand<FolderViewModel, Task> NextFolderCommand { get; }

        public ReactiveCommand<FolderViewModel, Task> BackFolderCommand { get; }

        public ReactiveCommand<ImageViewModel, Task> BackFolderFromImageCommand { get; }

        public ReactiveCommand<Unit, Task> BackFolderFromDirectoryOptionsPanelCommand { get; }

        public ReactiveCommand<FolderViewModel, Task> ImportImagesCommand { get; }

        public ReactiveCommand<FolderViewModel, Unit> AddFolderDescriptionCommand { get; }

        public ReactiveCommand<FolderViewModel, Unit> AddFolderTagsCommand { get; }

        public ReactiveCommand<FolderViewModel, Unit> EditFolderTagsCommand { get; }

        public ReactiveCommand<FolderViewModel, Unit> AddFolderRatingCommand { get; }

        public ReactiveCommand<ImageViewModel, Task> AddImageTagsCommand { get; }

        public ReactiveCommand<ListBox, Task> AddMultipleImageTagsCommand { get; }

        public ReactiveCommand<ImageViewModel, Task> EditImageTagsCommand { get; }

        public ReactiveCommand<ImageViewModel, Task> AddImageRatingCommand { get; }

        public ReactiveCommand<Tag, Task> RemoveTagOnAllImagesCommand { get; }

        public ReactiveCommand<Tag, Task> RemoveTagOnAllFoldersCommand { get; }

        public ReactiveCommand<Tag, Task> AddTagToAllFoldersInCurrentDirectoryCommand { get; }

        public ReactiveCommand<Unit, Unit> DeleteLibraryCommand { get; }

        public ReactiveCommand<ImageViewModel, Unit> OpenImageInExternalViewerCommand { get; }

        public ReactiveCommand<Unit, Unit> OpenCurrentDirectoryWithExplorerCommand { get; }

        public ReactiveCommand<ImageViewModel, Task> MoveImageToTrashCommand { get; }

        public ReactiveCommand<FolderViewModel, Task> MoveFolderToTrashCommand { get; }

        public ReactiveCommand<FolderViewModel, Task> ScanFolderImagesForMetaDataCommand { get; }

        public ReactiveCommand<Unit, Task> NextPageCommand { get; }

        public ReactiveCommand<Unit, Task> PreviousPageCommand { get; }

        public ReactiveCommand<decimal, Task> GoToPageCommand { get; }

        public ReactiveCommand<Unit, Unit> ToggleSettingsCommand { get; }

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

        public ReactiveCommand<string, Task> FilterFoldersInCurrentDirectoryByStartingLetterCommand { get; }

        public ReactiveCommand<Unit, Task> FilterFolderOnRatingAndTagCommand { get; }

        public ReactiveCommand<decimal, Task> FilterFoldersOnRatingCommand { get; }

        public ReactiveCommand<string, Task> FilterImagesOnTagCommand { get; }

        public ReactiveCommand<string, Task> FilterFoldersOnTagCommand  { get; }

        public ReactiveCommand<string, Task> FilterFoldersOnDescriptionCommand { get; }

        public ReactiveCommand<Unit, Task> UpdateImageDatesCommand { get; }

        public ReactiveCommand<Unit, Task> GetAllFoldersWithNoImportedImagesCommand { get; }

        public ReactiveCommand<Unit, Task> GetAllFoldersWithMetadataNotScannedCommand { get; }

        public ReactiveCommand<Unit, Task> GetAllFoldersWithoutCoversCommand { get; }

        public ReactiveCommand<Unit, Task> LoadCurrentDirectoryCommand { get; }

        public ReactiveCommand<string, Task> PickImageWidthCommand { get; }
        
        public ReactiveCommand<decimal, Task> SelectImageWidthCommand { get; }

        public ReactiveCommand<string, Task> PickFolderPageSizeCommand { get; }

        public ReactiveCommand<string, Task> PickImagePageSizeCommand { get; }

        public ReactiveCommand<ScrollViewer, Task> SaveDirectoryCommand { get; }

        public ReactiveCommand<ScrollViewer, Task> LoadSavedDirectoryCommand { get; }

        public ReactiveCommand<FolderViewModel, Task> SaveFolderAsFavoriteCommand { get; }

        public ReactiveCommand<Unit, Task> GetAllFavoriteFoldersCommand {  get; } 

        public ReactiveCommand<Unit, Task> RemoveAllFavoriteFoldersCommand { get; }

        public ReactiveCommand<IList, Task> MoveSelectedImagesToTrashCommand { get; }

        public ReactiveCommand<IList, Task> MoveSelectedImagesUpOneDirectoryCommand { get; }

        public ReactiveCommand<FolderViewModel, Task> MoveAllImagesInFolderUpOneDirectoryCommand { get; }

        public ReactiveCommand<ItemsControl, Task> ImportAllFoldersOnCurrentPageCommand { get; }

        public ReactiveCommand<ItemsControl, Task> AddCoverImageOnCurrentPageCommand { get; }

        public ReactiveCommand<FolderViewModel, Task> CopyCoverImageToContainingFolderCommand { get; }

        public ReactiveCommand<ItemsControl, Task> GetFolderDescriptionFromTextFileOnCurrentPageCommand { get; }

        public ReactiveCommand<ItemsControl, Task> BackUpFolderDescriptionToTextFileOnCurrentPageCommand { get; }

        public ReactiveCommand<FolderViewModel, Task> CopyFolderDescriptionToContainingFolderCommand { get; }

        public ReactiveCommand<ItemsControl, Task> ScanAllFoldersOnCurrentPageCommand { get; }

        public ReactiveCommand<Unit, Task> CreateNewFolderCommand { get; }

        public ReactiveCommand<Unit, Unit> ExitAppCommand { get; }

        //should technically have its own repo but only plan on having only this one method just keeping it in images repo.
        public async Task GetTagsList()
        {
            TagsList = await _imageMethods.GetTagsList();
        }
        private void ExitApp()
        {
            //Application.Current.ApplicationLifetime;
            if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown();
            }
        }
       
        private async void DeleteLibrary()
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
                bool success = await _folderMethods.DeleteAllFolders();
                if (success) 
                {
                    //refresh UI
                    LibraryFolders.Clear();
                    Images.Clear();
                }
            }
            else 
            {
                return;
            }
        }
    }
}
