using Avalonia.Controls;
using ImagePerfect.Helpers;
using ImagePerfect.Models;
using ImagePerfect.Repository.IRepository;
using MsBox.Avalonia;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Enums;
using MsBox.Avalonia.Models;
using ReactiveUI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Image = ImagePerfect.Models.Image;

namespace ImagePerfect.ViewModels
{
	public class MoveImagesViewModel : ViewModelBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly FolderMethods _folderMethods;
        private readonly ImageMethods _imageMethods;
        private readonly MainWindowViewModel _mainWindowViewModel;
        public MoveImagesViewModel(IUnitOfWork unitOfWork, MainWindowViewModel mainWindowViewModel) 
		{
            _unitOfWork = unitOfWork;
            _mainWindowViewModel = mainWindowViewModel;
            _folderMethods = new FolderMethods(_unitOfWork);
            _imageMethods = new ImageMethods(_unitOfWork);
        }

        public async Task MoveImageToTrash(ImageViewModel imageVm) 
        {
            var boxYesNo = MessageBoxManager.GetMessageBoxCustom(
            new MessageBoxCustomParams
                {
                    ButtonDefinitions = new List<ButtonDefinition>
                        {
                            new ButtonDefinition { Name = "Yes", },
                            new ButtonDefinition { Name = "No", },
                        },
                    ContentTitle = "Delete Image",
                    ContentMessage = $"Are you sure you want to delete your image?",
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    SizeToContent = SizeToContent.WidthAndHeight,  // <-- lets it grow with content
                    MinWidth = 500  // optional, so it doesn’t wrap too soon
                }
            );
            var boxResult = await boxYesNo.ShowWindowDialogAsync(Globals.MainWindow);
            if (boxResult == "Yes")
            {
                _mainWindowViewModel.ShowLoading = true;
                (List<Folder> folders, List<FolderTag> tags) folderResult = await _folderMethods.GetFoldersInDirectory(imageVm.ImageFolderPath);
                _mainWindowViewModel.displayFolders = folderResult.folders;
                (List<Image> images, List<ImageTag> tags) imageResultA = await _imageMethods.GetAllImagesInFolder(imageVm.FolderId);
                _mainWindowViewModel.displayImages = imageResultA.images;
                if (_mainWindowViewModel.displayImages.Count == 1 && _mainWindowViewModel.displayFolders.Count == 0)
                {
                    await MessageBoxManager.GetMessageBoxCustom(
                        new MessageBoxCustomParams
                        {
                            ButtonDefinitions = new List<ButtonDefinition>
                            {
                                new ButtonDefinition { Name = "Ok", },
                            },
                            ContentTitle = "Delete Image",
                            ContentMessage = $"This is the last image in the folder go back and delete the folder.",
                            WindowStartupLocation = WindowStartupLocation.CenterOwner,
                            SizeToContent = SizeToContent.WidthAndHeight,  // <-- lets it grow with content
                            MinWidth = 500  // optional, so it doesn’t wrap too soon
                        }
                    ).ShowWindowDialogAsync(Globals.MainWindow);
                    return;
                }
                Folder? rootFolder = await _folderMethods.GetRootFolder();
                string trashFolderPath = PathHelper.GetTrashFolderPath(rootFolder.FolderPath);

                //create ImagePerfectTRASH if it doesnt exist
                if (!Directory.Exists(trashFolderPath))
                {
                    Directory.CreateDirectory(trashFolderPath);
                }
                if (File.Exists(imageVm.ImagePath))
                {
                    //delete image from db
                    bool success = await _imageMethods.DeleteImage(imageVm.ImageId);

                    if (success)
                    {
                        //move file to trash folder
                        string newImagePath = PathHelper.GetImageFileTrashPath(imageVm, trashFolderPath);
                        File.Move(imageVm.ImagePath, newImagePath);

                        //refresh UI
                        if(!_mainWindowViewModel.SuppressImageRefresh)
                            await _mainWindowViewModel.RefreshImages("", imageVm.FolderId);
                        _mainWindowViewModel.ShowLoading = false;
                    }
                }
                _mainWindowViewModel.ShowLoading = false;
            }
        }

        public async Task MoveSelectedImagesToTrash(IList selectedImages)
        {
            List<ImageViewModel> imagesToDelete = selectedImages.OfType<ImageViewModel>().ToList();

            if (imagesToDelete.Count == 0)
            {
                await MessageBoxManager.GetMessageBoxCustom(
                    new MessageBoxCustomParams
                    {
                        ButtonDefinitions = new List<ButtonDefinition>
                        {
                            new ButtonDefinition { Name = "Ok", },
                        },
                        ContentTitle = "Delete Images",
                        ContentMessage = $"You need to select images to delete.",
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        SizeToContent = SizeToContent.WidthAndHeight,  // <-- lets it grow with content
                        MinWidth = 500  // optional, so it doesn’t wrap too soon
                    }
                ).ShowWindowDialogAsync(Globals.MainWindow);
                return;
            }
            var boxYesNo = MessageBoxManager.GetMessageBoxCustom(
                new MessageBoxCustomParams
                {
                    ButtonDefinitions = new List<ButtonDefinition>
                        {
                            new ButtonDefinition { Name = "Yes", },
                            new ButtonDefinition { Name = "No", },
                        },
                    ContentTitle = "Delete Images",
                    ContentMessage = $"Are you sure you want to delete these images?",
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    SizeToContent = SizeToContent.WidthAndHeight,  // <-- lets it grow with content
                    MinWidth = 500  // optional, so it doesn’t wrap too soon
                }
            );
            var boxResult = await boxYesNo.ShowWindowDialogAsync(Globals.MainWindow);
            if (boxResult == "Yes")
            {
                _mainWindowViewModel.ShowLoading = true;
                Folder imagesFolder = await _folderMethods.GetFolderAtDirectory(imagesToDelete[0].ImageFolderPath);
                Folder? rootFolder = await _folderMethods.GetRootFolder();
                string trashFolderPath = PathHelper.GetTrashFolderPath(rootFolder.FolderPath);

                //create ImagePerfectTRASH if it doesnt exist
                if (!Directory.Exists(trashFolderPath))
                {
                    Directory.CreateDirectory(trashFolderPath);
                }

                string sql = SqlStringBuilder.BuildSqlForMoveImagesToTrash(imagesToDelete);
                bool success = await _imageMethods.DeleteSelectedImages(sql);
                if (success)
                {
                    foreach (ImageViewModel image in imagesToDelete)
                    {
                        //move file to trash folder
                        string newImagePath = PathHelper.GetImageFileTrashPath(image, trashFolderPath);
                        File.Move(image.ImagePath, newImagePath);
                    }
                    //update imagesFolder HasFiles, AreImagesImported, and FolderContentMetaDataScanned
                    //set all back to false if moved all images to trash
                    IEnumerable<string> folderFiles = Directory.EnumerateFiles(imagesFolder.FolderPath).Where(s => s.ToLower().EndsWith(".jpeg") || s.ToLower().EndsWith(".jpg") || s.ToLower().EndsWith(".png") || s.ToLower().EndsWith(".gif"));
                    if (!folderFiles.Any())
                    {
                        imagesFolder.HasFiles = false;
                        imagesFolder.AreImagesImported = false;
                        imagesFolder.FolderContentMetaDataScanned = false;
                        await _folderMethods.UpdateFolder(imagesFolder);
                    }
                    //refresh UI
                    if (!_mainWindowViewModel.SuppressImageRefresh)
                        await _mainWindowViewModel.RefreshImages("", imagesToDelete[0].FolderId);
                    _mainWindowViewModel.ShowLoading = false;
                }
                _mainWindowViewModel.ShowLoading = false;
            }
        }

        public async Task MoveSelectedImageUpOneDirectory(IList selectedImages)
        {
            if (selectedImages is null || selectedImages.Count == 0)
            {
                await MessageBoxManager.GetMessageBoxCustom(
                    new MessageBoxCustomParams
                    {
                        ButtonDefinitions = new List<ButtonDefinition>
                        {
                            new ButtonDefinition { Name = "Ok", },
                        },
                        ContentTitle = "Move Images",
                        ContentMessage = $"You need to select images to move.",
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        SizeToContent = SizeToContent.WidthAndHeight,  // <-- lets it grow with content
                        MinWidth = 500  // optional, so it doesn’t wrap too soon
                    }
                ).ShowWindowDialogAsync(Globals.MainWindow);
                return;
            }
            _mainWindowViewModel.SelectedImagesNewDirectory = PathHelper.RemoveOneFolderFromPath(_mainWindowViewModel.CurrentDirectory);
            if (_mainWindowViewModel.SelectedImagesNewDirectory == null || _mainWindowViewModel.SelectedImagesNewDirectory == "")
                return;
            await MoveSelectedImagesToNewFolder(selectedImages);
        }

        public async Task MoveSelectedImagesToNewFolder(IList selectedImages)
        {
            List<ImageViewModel> imagesToMove = selectedImages.OfType<ImageViewModel>().ToList();
            if (imagesToMove.Count == 0)
                return;
            
            Folder imagesCurrentFolder = await _folderMethods.GetFolderAtDirectory(imagesToMove[0].ImageFolderPath);

            //get folder at SelectedImagesNewDirectory
            Folder imagesNewFolder = await _folderMethods.GetFolderAtDirectory(_mainWindowViewModel.SelectedImagesNewDirectory);
            if (imagesNewFolder.FolderPath == imagesCurrentFolder.FolderPath)
            {
                await MessageBoxManager.GetMessageBoxCustom(
                    new MessageBoxCustomParams
                    {
                        ButtonDefinitions = new List<ButtonDefinition>
                        {
                            new ButtonDefinition { Name = "Ok", },
                        },
                        ContentTitle = "Move Images",
                        ContentMessage = $"New folder path cannot be the same as the current folder path.",
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        SizeToContent = SizeToContent.WidthAndHeight,  // <-- lets it grow with content
                        MinWidth = 500  // optional, so it doesn’t wrap too soon
                    }
                ).ShowWindowDialogAsync(Globals.MainWindow);
                return;
            }
            //prevent a double import and only allow move to folders that are already imported
            if (imagesNewFolder.HasFiles == true && imagesNewFolder.AreImagesImported == false)
            {
                await MessageBoxManager.GetMessageBoxCustom(
                    new MessageBoxCustomParams
                    {
                        ButtonDefinitions = new List<ButtonDefinition>
                        {
                            new ButtonDefinition { Name = "Ok", },
                        },
                        ContentTitle = "Move Images",
                        ContentMessage = $"The move to folder has to have its current images imported first.",
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        SizeToContent = SizeToContent.WidthAndHeight,  // <-- lets it grow with content
                        MinWidth = 500  // optional, so it doesn’t wrap too soon
                    }
                ).ShowWindowDialogAsync(Globals.MainWindow);
                return;
            }
            var boxYesNo = MessageBoxManager.GetMessageBoxCustom(
                new MessageBoxCustomParams
                {
                    ButtonDefinitions = new List<ButtonDefinition>
                    {
                        new ButtonDefinition { Name = "Yes", },
                        new ButtonDefinition { Name = "No", },
                    },
                    ContentTitle = "Move Images",
                    ContentMessage = $"Are you sure you want to move these images to: \n{_mainWindowViewModel.SelectedImagesNewDirectory}?",
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    SizeToContent = SizeToContent.WidthAndHeight,  // <-- lets it grow with content
                    MinWidth = 500  // optional, so it doesn’t wrap too soon
                }
            );
            var boxResult = await boxYesNo.ShowWindowDialogAsync(Globals.MainWindow);
            if (boxResult == "Yes")
            {
                _mainWindowViewModel.ShowLoading = true;
                //modify ImagePath, ImageFolderPath and FolderId for each image in imagesToMove 
                List<ImageViewModel> imagesToMoveModifiedPaths = PathHelper.ModifyImagePathsForMoveImagesToNewFolder(imagesToMove, imagesNewFolder);
                
                //get image move sql
                string imageMoveSql = SqlStringBuilder.BuildSqlForMoveImagesToNewFolder(imagesToMoveModifiedPaths);
                //move images in db
                bool success = await _imageMethods.MoveSelectedImageToNewFolder(imageMoveSql);
                //move images on disk
                if (success)
                {
                    for (int i = 0; i < imagesToMoveModifiedPaths.Count; i++)
                    {
                        File.Move(imagesToMove[i].ImagePath, imagesToMoveModifiedPaths[i].ImagePath);
                    }
                    //after adding new images to a folder make sure the user is alerted to rescann them for metadata 
                    imagesNewFolder.FolderContentMetaDataScanned = false;
                    await _folderMethods.UpdateFolder(imagesNewFolder);
                    //if new folder did not have images before it does now so set to true
                    if (imagesNewFolder.HasFiles == false)
                    {
                        imagesNewFolder.HasFiles = true;
                        imagesNewFolder.AreImagesImported = true;
                        await _folderMethods.UpdateFolder(imagesNewFolder);
                    }
                    //update imagesCurrentFolder HasFiles, AreImagesImported, and FolderContentMetaDataScanned
                    //set all back to false if moved all images to new folder
                    IEnumerable<string> folderFiles = Directory.EnumerateFiles(imagesCurrentFolder.FolderPath).Where(s => s.ToLower().EndsWith(".jpeg") || s.ToLower().EndsWith(".jpg") || s.ToLower().EndsWith(".png") || s.ToLower().EndsWith(".gif"));
                    if (!folderFiles.Any())
                    {
                        imagesCurrentFolder.HasFiles = false;
                        imagesCurrentFolder.AreImagesImported = false;
                        imagesCurrentFolder.FolderContentMetaDataScanned = false;
                        await _folderMethods.UpdateFolder(imagesCurrentFolder);
                    }
                    //reset SelectedImageNewDirectory
                    _mainWindowViewModel.SelectedImagesNewDirectory = string.Empty;
                    //refresh UI
                    await _mainWindowViewModel.RefreshImages("", imagesToMove[0].FolderId);
                    await _mainWindowViewModel.RefreshFolders(imagesCurrentFolder.FolderPath);
                    _mainWindowViewModel.ShowLoading = false;
                }
                _mainWindowViewModel.ShowLoading = false;
            }
        }
	}
}