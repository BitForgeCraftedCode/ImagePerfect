using ImagePerfect.Helpers;
using ImagePerfect.Models;
using ImagePerfect.ObjectMappers;
using ImagePerfect.Repository;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using Serilog;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Image = ImagePerfect.Models.Image;

namespace ImagePerfect.ViewModels
{
	public class MoveImagesViewModel : ViewModelBase
    {
        private readonly MySqlDataSource _dataSource;
        private readonly IConfiguration _configuration;
        private readonly MainWindowViewModel _mainWindowViewModel;
        public MoveImagesViewModel(MySqlDataSource dataSource, IConfiguration config, MainWindowViewModel mainWindowViewModel) 
		{
            _dataSource = dataSource;
            _configuration = config;
            _mainWindowViewModel = mainWindowViewModel;
        }

        public async Task MoveImageToTrash(ImageViewModel imageVm) 
        {
            bool boxResult = await MessageBoxHelper.ShowYesNoAsync(
                "Move Image To Trash",
                $"Are you sure you want to move image to trash?"
            );
            if (boxResult)
            {
                string imageFileTrashPath = string.Empty;
                bool imageMoved = false;
                bool databaseDeleteSucceeded = false;
                try
                {
                    await using UnitOfWork uow = await UnitOfWork.CreateAsync(_dataSource, _configuration);
                    ImageMethods imageMethods = new ImageMethods(uow);
                    FolderMethods folderMethods = new FolderMethods(uow);

                    _mainWindowViewModel.ShowLoading = true;
                    (List<Folder> folders, List<FolderTag> tags) folderResult = await folderMethods.GetFoldersInDirectory(imageVm.ImageFolderPath, _mainWindowViewModel.ExplorerVm.LoadFoldersAscending);
                    _mainWindowViewModel.ExplorerVm.displayFolders = folderResult.folders;
                    (List<Image> images, List<ImageTag> tags) imageResultA = await imageMethods.GetAllImagesInFolder(imageVm.FolderId, _mainWindowViewModel.ExplorerVm.LoadImagesAscending);
                    _mainWindowViewModel.ExplorerVm.displayImages = imageResultA.images;
                    if (_mainWindowViewModel.ExplorerVm.displayImages.Count == 1 && _mainWindowViewModel.ExplorerVm.displayFolders.Count == 0)
                    {
                        await MessageBoxHelper.ShowAsync(
                            "Move Image To Trash",
                            $"This is the last image in the folder go back and delete the folder."
                        );
                        return;
                    }
                    Folder? rootFolder = await folderMethods.GetRootFolder();
                    string trashFolderPath = PathHelper.GetTrashFolderPath(rootFolder.FolderPath);
                    imageFileTrashPath = PathHelper.GetImageFileTrashPath(imageVm, trashFolderPath);

                    //create ImagePerfectTRASH if it doesnt exist
                    if (!Directory.Exists(trashFolderPath))
                    {
                        Directory.CreateDirectory(trashFolderPath);
                    }
                    if (File.Exists(imageVm.ImagePath))
                    {
                        //move file to trash folder
                        File.Move(imageVm.ImagePath, imageFileTrashPath);
                        imageMoved = true;

                        //delete image from db
                        databaseDeleteSucceeded = await imageMethods.DeleteImage(imageVm.ImageId);

                        if (databaseDeleteSucceeded)
                        {
                            try
                            {
                                //refresh UI
                                if (!_mainWindowViewModel.SuppressImageRefresh)
                                    await _mainWindowViewModel.ExplorerVm.RefreshImages("", imageVm.FolderId, uow);
                            }
                            catch (Exception ex)
                            {
                                Log.Error(ex, "Image was moved to trash but refreshing the view failed");
                            }
                        }
                        else
                        {
                            //move image back
                            bool imageRestored = TryMoveImageBackFromTrash(imageFileTrashPath, imageVm.ImagePath);
                            await MessageBoxHelper.ShowAsync(
                                "Move Image To Trash",
                                imageRestored
                                    ? "Could not move the image to trash. Please try again."
                                    : $"Could not move the image to trash or restore it. The image may still be at: {imageFileTrashPath}"
                            );
                        }
                    }
                    else
                    {
                        await MessageBoxHelper.ShowAsync(
                            "Move Image To Trash",
                            "The selected image does not exist on disk."
                        );
                    }
                }
                catch(Exception ex)
                {
                    Log.Error(ex, "Failed to move the image to trash. {imagePath}", imageVm.ImagePath);
                    //needed only if the file was moved and the database deletion did not succeed
                    if (imageMoved && !databaseDeleteSucceeded)
                    {
                        //move image back
                        bool imageRestored = TryMoveImageBackFromTrash(imageFileTrashPath, imageVm.ImagePath);
                        if (!imageRestored)
                        {
                            await MessageBoxHelper.ShowAsync(
                                "Move Image To Trash",
                                $"The operation failed and the image could not be restored. The image may still be at: {imageFileTrashPath}"
                            );
                            return;
                        }
                    }
                    await MessageBoxHelper.ShowAsync(
                        "Move Image To Trash",
                        "Error moving the image to trash. Check the logs for details."
                    );
                }
                finally
                {
                    _mainWindowViewModel.ShowLoading = false;
                }
            }
        }

        private static bool TryMoveImageBackFromTrash(string imageFileTrashPath, string imageFileOriginalPath)
        {
            try
            {
                File.Move(imageFileTrashPath, imageFileOriginalPath);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to move image back from trash afeter move to trash failed. {imagePath}", imageFileOriginalPath);
                return false;
            }
        }

        public async Task MoveSelectedImagesToTrash(IList selectedImages)
        {
            List<ImageViewModel> imagesToDelete = selectedImages.OfType<ImageViewModel>().ToList();

            if (imagesToDelete.Count == 0)
            {
                await MessageBoxHelper.ShowAsync(
                    "Delete Images",
                    $"You need to select images to delete."
                );
                return;
            }
            bool boxResult = await MessageBoxHelper.ShowYesNoAsync(
                "Delete Images",
                $"Are you sure you want to delete these images?"
            );
            if (boxResult)
            {
                List<(string OriginalPath, string TrashPath)> movedImages = new List<(string OriginalPath, string TrashPath)>();
                bool databaseDeleteSucceeded = false;
                try
                {
                    await using UnitOfWork uow = await UnitOfWork.CreateAsync(_dataSource, _configuration);
                    ImageMethods imageMethods = new ImageMethods(uow);
                    FolderMethods folderMethods = new FolderMethods(uow);
                    _mainWindowViewModel.ShowLoading = true;
                    Folder imagesFolder = await folderMethods.GetFolderAtDirectory(imagesToDelete[0].ImageFolderPath);
                    Folder? rootFolder = await folderMethods.GetRootFolder();
                    string trashFolderPath = PathHelper.GetTrashFolderPath(rootFolder.FolderPath);

                    //create ImagePerfectTRASH if it doesnt exist
                    if (!Directory.Exists(trashFolderPath))
                    {
                        Directory.CreateDirectory(trashFolderPath);
                    }
                    // Validate files exists
                    foreach (ImageViewModel image in imagesToDelete)
                    {
                        if (!File.Exists(image.ImagePath))
                        {
                            await MessageBoxHelper.ShowAsync("Move Images To Trash", "One or more image files is missing from the folder");
                            return;
                        }
                    }
                    // Move each file first and keep its paths so the batch can be rolled back.
                    foreach (ImageViewModel image in imagesToDelete)
                    {
                        string trashPath = PathHelper.GetImageFileTrashPath(image, trashFolderPath);
                        File.Move(image.ImagePath, trashPath);
                        movedImages.Add((image.ImagePath, trashPath));
                    }
                    string sql = SqlStringBuilder.BuildSqlForMoveImagesToTrash(imagesToDelete);
                    databaseDeleteSucceeded = await imageMethods.DeleteSelectedImages(sql);
                    if (!databaseDeleteSucceeded)
                    {
                        List<string> failedRestores = RestoreImagesFromTrash(movedImages);
                        string message = failedRestores.Count == 0
                            ? "Could not move the selected images to trash. Please try again."
                            : $"Could not move the selected images to trash or restore all of them. These files may still be in trash:\n{string.Join("\n", failedRestores)}";
                        await MessageBoxHelper.ShowAsync("Move Images To Trash", message);
                        return;
                    }

                    try
                    {
                        //update imagesFolder HasFiles, AreImagesImported, and FolderContentMetaDataScanned
                        //set all back to false if moved all images to trash
                        IEnumerable<string> folderFiles = Directory.EnumerateFiles(imagesFolder.FolderPath).Where(s => s.ToLower().EndsWith(".jpeg") || s.ToLower().EndsWith(".jpg") || s.ToLower().EndsWith(".png") || s.ToLower().EndsWith(".gif") || s.ToLower().EndsWith(".webp") || s.ToLower().EndsWith(".tif"));
                        if (!folderFiles.Any())
                        {
                            imagesFolder.HasFiles = false;
                            imagesFolder.AreImagesImported = false;
                            imagesFolder.FolderContentMetaDataScanned = false;
                            await folderMethods.UpdateFolder(imagesFolder);
                        }
                        if (!_mainWindowViewModel.SuppressImageRefresh)
                            await _mainWindowViewModel.ExplorerVm.RefreshImages("", imagesToDelete[0].FolderId, uow);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Images were moved to trash but folder metadata or the view could not be refreshed");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to move selected images to trash");
                    if (!databaseDeleteSucceeded && movedImages.Count > 0)
                    {
                        List<string> failedRestores = RestoreImagesFromTrash(movedImages);
                        if (failedRestores.Count > 0)
                        {
                            await MessageBoxHelper.ShowAsync(
                                "Move Images To Trash",
                                $"The operation failed and some images could not be restored. These files may still be in trash:\n{string.Join("\n", failedRestores)}"
                            );
                            return;
                        }
                    }
                    await MessageBoxHelper.ShowAsync(
                        "Move Images To Trash",
                        "Error moving the selected images to trash. Check the logs for details."
                    );
                }
                finally
                {
                    _mainWindowViewModel.ShowLoading = false;
                }
            }
        }

        private static List<string> RestoreImagesFromTrash(List<(string OriginalPath, string TrashPath)> movedImages)
        {
            List<string> failedRestores = new List<string>();
            foreach ((string originalPath, string trashPath) in movedImages)
            {
                if (!TryMoveImageBackFromTrash(trashPath, originalPath))
                    failedRestores.Add(trashPath);
            }
            return failedRestores;
        }

        public async Task MoveAllImagesInFolderUpOneDirectory(FolderViewModel folderVm)
        {
            await using UnitOfWork uow = await UnitOfWork.CreateAsync(_dataSource, _configuration);
            ImageMethods imageMethods = new ImageMethods(uow);
            (List<Image> images, List<ImageTag> tags) imageResult = await imageMethods.GetAllImagesInFolder(folderVm.FolderId, _mainWindowViewModel.ExplorerVm.LoadImagesAscending);
            List<Image> allImages = imageResult.images;
            if(allImages is null || allImages.Count == 0)
            {
                await MessageBoxHelper.ShowAsync(
                    "Move Images",
                    $"There are no images in that folder."
                );
                return;
            }
            //Up One will be the CurrentDirectory in this case
            _mainWindowViewModel.SelectedImagesNewDirectory = _mainWindowViewModel.ExplorerVm.CurrentDirectory;
            if (string.IsNullOrEmpty(_mainWindowViewModel.SelectedImagesNewDirectory))
                return;
            //map Images to ImageViewModel
            ImageViewModel[] allImagesVm = new ImageViewModel[allImages.Count];
            await Parallel.ForEachAsync(
                Enumerable.Range(0, allImages.Count), 
                new ParallelOptions { MaxDegreeOfParallelism = 4 }, 
                async (i, ct) => 
                {
                    ImageViewModel iVm = await ImageMapper.GetImageVm(allImages[i]);
                    allImagesVm[i] = iVm;
                });
            await MoveSelectedImagesToNewFolder(allImagesVm.ToList());
        }
        public async Task MoveSelectedImageUpOneDirectory(IList selectedImages)
        {
            if (selectedImages is null || selectedImages.Count == 0)
            {
                await MessageBoxHelper.ShowAsync(
                    "Move Images",
                    $"You need to select images to move."
                );
                return;
            }
            _mainWindowViewModel.SelectedImagesNewDirectory = PathHelper.RemoveOneFolderFromPath(_mainWindowViewModel.ExplorerVm.CurrentDirectory);
            if (_mainWindowViewModel.SelectedImagesNewDirectory == null || _mainWindowViewModel.SelectedImagesNewDirectory == "")
                return;
            await MoveSelectedImagesToNewFolder(selectedImages);
        }

        public async Task MoveSelectedImagesToNewFolder(IList selectedImages)
        {
            List<ImageViewModel> imagesToMove = selectedImages.OfType<ImageViewModel>().ToList();
            if (imagesToMove.Count == 0)
                return;

            await using UnitOfWork uow = await UnitOfWork.CreateAsync(_dataSource, _configuration);
            ImageMethods imageMethods = new ImageMethods(uow);
            FolderMethods folderMethods = new FolderMethods(uow);

            Folder imagesCurrentFolder = await folderMethods.GetFolderAtDirectory(imagesToMove[0].ImageFolderPath);

            //get folder at SelectedImagesNewDirectory
            Folder imagesNewFolder = await folderMethods.GetFolderAtDirectory(_mainWindowViewModel.SelectedImagesNewDirectory);
            if (imagesNewFolder.FolderPath == imagesCurrentFolder.FolderPath)
            {
                await MessageBoxHelper.ShowAsync(
                    "Move Images",
                    $"New folder path cannot be the same as the current folder path."
                );
                return;
            }
            //prevent a double import and only allow move to folders that are already imported
            if (imagesNewFolder.HasFiles == true && imagesNewFolder.AreImagesImported == false)
            {
                await MessageBoxHelper.ShowAsync(
                    "Move Images",
                    $"The move to folder has to have its current images imported first."
                );
                return;
            }
            bool boxResult = await MessageBoxHelper.ShowYesNoAsync(
                "Move Images",
                $"\"{imagesCurrentFolder.FolderName}\"\n\nAre you sure you want to move images in the above folder to: \n{_mainWindowViewModel.SelectedImagesNewDirectory}?"
            );
            if (boxResult)
            {
                _mainWindowViewModel.ShowLoading = true;
                //modify ImagePath, ImageFolderPath and FolderId for each image in imagesToMove 
                List<ImageViewModel> imagesToMoveModifiedPaths = PathHelper.ModifyImagePathsForMoveImagesToNewFolder(imagesToMove, imagesNewFolder);

                //get image move sql
                (string Sql, Dapper.DynamicParameters Parameters) imageMoveSql = SqlStringBuilder.BuildSqlForMoveImagesToNewFolder(imagesToMoveModifiedPaths);
                //move images in db
                bool success = await imageMethods.MoveSelectedImageToNewFolder(imageMoveSql);
                //move images on disk
                if (success)
                {
                    for (int i = 0; i < imagesToMoveModifiedPaths.Count; i++)
                    {
                        File.Move(imagesToMove[i].ImagePath, imagesToMoveModifiedPaths[i].ImagePath);
                    }
                    //after adding new images to a folder make sure the user is alerted to rescann them for metadata 
                    imagesNewFolder.FolderContentMetaDataScanned = false;
                    await folderMethods.UpdateFolder(imagesNewFolder);
                    //if new folder did not have images before it does now so set to true
                    if (imagesNewFolder.HasFiles == false)
                    {
                        imagesNewFolder.HasFiles = true;
                        imagesNewFolder.AreImagesImported = true;
                        await folderMethods.UpdateFolder(imagesNewFolder);
                    }
                    //update imagesCurrentFolder HasFiles, AreImagesImported, and FolderContentMetaDataScanned
                    //set all back to false if moved all images to new folder
                    IEnumerable<string> folderFiles = Directory.EnumerateFiles(imagesCurrentFolder.FolderPath).Where(s => s.ToLower().EndsWith(".jpeg") || s.ToLower().EndsWith(".jpg") || s.ToLower().EndsWith(".png") || s.ToLower().EndsWith(".gif") || s.ToLower().EndsWith(".webp") || s.ToLower().EndsWith(".tif"));
                    if (!folderFiles.Any())
                    {
                        imagesCurrentFolder.HasFiles = false;
                        imagesCurrentFolder.AreImagesImported = false;
                        imagesCurrentFolder.FolderContentMetaDataScanned = false;
                        await folderMethods.UpdateFolder(imagesCurrentFolder);
                    }
                    //reset SelectedImageNewDirectory
                    _mainWindowViewModel.SelectedImagesNewDirectory = string.Empty;
                    //refresh UI
                    await _mainWindowViewModel.DirectoryNavigationVm.LoadCurrentDirectory();
                    _mainWindowViewModel.ShowLoading = false;
                }
                _mainWindowViewModel.ShowLoading = false;
            }
        }
	}
}
