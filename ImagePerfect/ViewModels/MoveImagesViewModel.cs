using Avalonia.Controls;
using ImagePerfect.Helpers;
using ImagePerfect.Models;
using ImagePerfect.ObjectMappers;
using ImagePerfect.Repository;
using ImagePerfect.Repository.IRepository;
using Microsoft.Extensions.Configuration;
using MsBox.Avalonia;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Enums;
using MsBox.Avalonia.Models;
using MySqlConnector;
using ReactiveUI;
using System;
using System.Collections;
using System.Collections.Concurrent;
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
                await using UnitOfWork uow = await UnitOfWork.CreateAsync(_dataSource, _configuration);
                ImageMethods imageMethods = new ImageMethods(uow);
                FolderMethods folderMethods = new FolderMethods(uow);
                _mainWindowViewModel.ShowLoading = true;
                (List<Folder> folders, List<FolderTag> tags) folderResult = await folderMethods.GetFoldersInDirectory(imageVm.ImageFolderPath, _mainWindowViewModel.ExplorerVm.LoadFoldersAscending);
                _mainWindowViewModel.ExplorerVm.displayFolders = folderResult.folders;
                (List<Image> images, List<ImageTag> tags) imageResultA = await imageMethods.GetAllImagesInFolder(imageVm.FolderId);
                _mainWindowViewModel.ExplorerVm.displayImages = imageResultA.images;
                if (_mainWindowViewModel.ExplorerVm.displayImages.Count == 1 && _mainWindowViewModel.ExplorerVm.displayFolders.Count == 0)
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
                Folder? rootFolder = await folderMethods.GetRootFolder();
                string trashFolderPath = PathHelper.GetTrashFolderPath(rootFolder.FolderPath);

                //create ImagePerfectTRASH if it doesnt exist
                if (!Directory.Exists(trashFolderPath))
                {
                    Directory.CreateDirectory(trashFolderPath);
                }
                if (File.Exists(imageVm.ImagePath))
                {
                    //delete image from db
                    bool success = await imageMethods.DeleteImage(imageVm.ImageId);

                    if (success)
                    {
                        //move file to trash folder
                        string newImagePath = PathHelper.GetImageFileTrashPath(imageVm, trashFolderPath);
                        File.Move(imageVm.ImagePath, newImagePath);

                        //refresh UI
                        if(!_mainWindowViewModel.SuppressImageRefresh)
                            await _mainWindowViewModel.ExplorerVm.RefreshImages("", imageVm.FolderId, uow);
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

                string sql = SqlStringBuilder.BuildSqlForMoveImagesToTrash(imagesToDelete);
                bool success = await imageMethods.DeleteSelectedImages(sql);
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
                        await folderMethods.UpdateFolder(imagesFolder);
                    }
                    //refresh UI
                    if (!_mainWindowViewModel.SuppressImageRefresh)
                        await _mainWindowViewModel.ExplorerVm.RefreshImages("", imagesToDelete[0].FolderId, uow);
                    _mainWindowViewModel.ShowLoading = false;
                }
                _mainWindowViewModel.ShowLoading = false;
            }
        }

        public async Task MoveAllImagesInFolderUpOneDirectory(FolderViewModel folderVm)
        {
            await using UnitOfWork uow = await UnitOfWork.CreateAsync(_dataSource, _configuration);
            ImageMethods imageMethods = new ImageMethods(uow);
            (List<Image> images, List<ImageTag> tags) imageResult = await imageMethods.GetAllImagesInFolder(folderVm.FolderId);
            List<Image> allImages = imageResult.images;
            if(allImages is null || allImages.Count == 0)
            {
                await MessageBoxManager.GetMessageBoxCustom(
                   new MessageBoxCustomParams
                   {
                       ButtonDefinitions = new List<ButtonDefinition>
                       {
                            new ButtonDefinition { Name = "Ok", },
                       },
                       ContentTitle = "Move Images",
                       ContentMessage = $"There are no images in that folder.",
                       WindowStartupLocation = WindowStartupLocation.CenterOwner,
                       SizeToContent = SizeToContent.WidthAndHeight,  // <-- lets it grow with content
                       MinWidth = 500  // optional, so it doesn’t wrap too soon
                   }
               ).ShowWindowDialogAsync(Globals.MainWindow);
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
                    ContentMessage = $"\"{imagesCurrentFolder.FolderName}\"\n\nAre you sure you want to move images in the above folder to: \n{_mainWindowViewModel.SelectedImagesNewDirectory}?",
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    SizeToContent = SizeToContent.Manual,
                    Width = 600,
                    Height = double.NaN,
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
                    IEnumerable<string> folderFiles = Directory.EnumerateFiles(imagesCurrentFolder.FolderPath).Where(s => s.ToLower().EndsWith(".jpeg") || s.ToLower().EndsWith(".jpg") || s.ToLower().EndsWith(".png") || s.ToLower().EndsWith(".gif"));
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