using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Image = ImagePerfect.Models.Image;
using System.Threading.Tasks;
using ImagePerfect.Models;
using ImagePerfect.Repository.IRepository;
using ReactiveUI;
using System.Linq;
using ImagePerfect.ObjectMappers;
using MsBox.Avalonia.Enums;
using MsBox.Avalonia;
using ImagePerfect.Helpers;
using System.IO;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Models;

namespace ImagePerfect.ViewModels
{
	public class CoverImageViewModel : ViewModelBase
	{
        private readonly IUnitOfWork _unitOfWork;
        private readonly FolderMethods _folderMethods;
		private readonly ImageMethods _imageMethods;
        private readonly MainWindowViewModel _mainWindowViewModel;
        public CoverImageViewModel(IUnitOfWork unitOfWork, MainWindowViewModel mainWindowViewModel) 
		{
            _unitOfWork = unitOfWork;
            _mainWindowViewModel = mainWindowViewModel;
            _folderMethods = new FolderMethods(_unitOfWork);
            _imageMethods = new ImageMethods(_unitOfWork);
        }

        public async Task CopyCoverImageToContainingFolder(FolderViewModel folderVm)
        {
            if (PathHelper.RemoveOneFolderFromPath(folderVm.FolderPath) == _mainWindowViewModel.InitializeVm.RootFolderLocation)
            {
                await MessageBoxManager.GetMessageBoxCustom(
                    new MessageBoxCustomParams
                    {
                        ButtonDefinitions = new List<ButtonDefinition>
                        {
                            new ButtonDefinition { Name = "Ok", },
                        },
                        ContentTitle = "Copy Cover",
                        ContentMessage = $"Cannot copy cover image from root folder.",
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        SizeToContent = SizeToContent.WidthAndHeight,  // <-- lets it grow with content
                        MinWidth = 500  // optional, so it doesn’t wrap too soon
                    }
                ).ShowWindowDialogAsync(Globals.MainWindow);
                return;
            }
            if (folderVm.CoverImagePath == "" || folderVm.CoverImagePath == null)
            {
                await MessageBoxManager.GetMessageBoxCustom(
                    new MessageBoxCustomParams
                    {
                        ButtonDefinitions = new List<ButtonDefinition>
                        {
                            new ButtonDefinition { Name = "Ok", },
                        },
                        ContentTitle = "Copy Cover",
                        ContentMessage = $"The folder must have a cover selected to copy.",
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        SizeToContent = SizeToContent.WidthAndHeight,  // <-- lets it grow with content
                        MinWidth = 500  // optional, so it doesn’t wrap too soon
                    }
                ).ShowWindowDialogAsync(Globals.MainWindow);
                return;
            }


            string coverImageCurrentPath = folderVm.CoverImagePath;
            string coverImageNewPath = PathHelper.GetCoverImagePathForCopyCoverImageToContainingFolder(folderVm);
            Folder containingFolder = await _folderMethods.GetFolderAtDirectory(PathHelper.RemoveOneFolderFromPath(folderVm.FolderPath));
            if (containingFolder.CoverImagePath != "")
            {
                var boxYesNo = MessageBoxManager.GetMessageBoxCustom(
                    new MessageBoxCustomParams
                    {
                        ButtonDefinitions = new List<ButtonDefinition>
                            {
                                new ButtonDefinition { Name = "Yes", },
                                new ButtonDefinition { Name = "No", },
                            },
                        ContentTitle = "Copy Cover",
                        ContentMessage = $"Containing folder already has a cover. Do you want to copy another?",
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        SizeToContent = SizeToContent.WidthAndHeight,  // <-- lets it grow with content
                        MinWidth = 500  // optional, so it doesn’t wrap too soon
                    }
                );
                var boxResult = await boxYesNo.ShowWindowDialogAsync(Globals.MainWindow);
                if (boxResult == "No")
                {
                    return;
                }
            }
            if (File.Exists(coverImageNewPath))
            {
                await MessageBoxManager.GetMessageBoxCustom(
                    new MessageBoxCustomParams
                    {
                        ButtonDefinitions = new List<ButtonDefinition>
                        {
                            new ButtonDefinition { Name = "Ok", },
                        },
                        ContentTitle = "Copy Cover",
                        ContentMessage = $"A cover image in the destination has the same file name. Pick a different cover.",
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        SizeToContent = SizeToContent.WidthAndHeight,  // <-- lets it grow with content
                        MinWidth = 500  // optional, so it doesn’t wrap too soon
                    }
                ).ShowWindowDialogAsync(Globals.MainWindow);
                return;
            }
            //add cover image path to containing folder
            bool success = await _folderMethods.UpdateCoverImage(coverImageNewPath, containingFolder.FolderId);
            //copy file in file system
            if (success)
            {
                File.Copy(coverImageCurrentPath, coverImageNewPath);
            }
        }

        public async Task AddCoverImageOnCurrentPage(ItemsControl foldersItemsControl)
        {
            List<FolderViewModel> allFolders = foldersItemsControl.Items.OfType<FolderViewModel>().ToList();
            Random random = new Random();
            foreach (FolderViewModel folder in allFolders)
            {
                if (folder.HasFiles == true && folder.AreImagesImported == true)
                {
                    (List<Image> images, List<ImageTag> tags) imageResult = await _imageMethods.GetAllImagesInFolder(folder.FolderId);
                    List<Image> images = imageResult.images;
                    int randomIndex = random.Next(0, images.Count - 1);
                    //set random fall back cover
                    string cover = images[randomIndex].ImagePath;
                    //get cover clean
                    foreach (Image image in images)
                    {
                        if (image.ImagePath.ToLower().Contains("cover") && image.ImagePath.ToLower().Contains("clean"))
                        {
                            cover = image.ImagePath;
                            break;
                        }
                    }
                    //if no cover clean get poster
                    if (!(cover.ToLower().Contains("cover") && cover.ToLower().Contains("clean")))
                    {
                        foreach (Image image in images)
                        {
                            if (image.ImagePath.ToLower().Contains("poster"))
                            {
                                cover = image.ImagePath;
                                break;
                            }
                        }
                    }
                    //if no poster or no cover clean get cover
                    if (!(cover.ToLower().Contains("poster") || (cover.ToLower().Contains("cover") && cover.ToLower().Contains("clean"))))
                    {
                        foreach (Image image in images)
                        {
                            if (image.ImagePath.ToLower().Contains("cover"))
                            {
                                cover = image.ImagePath;
                                break;
                            }
                        }
                    }
                    folder.CoverImagePath = cover;
                    await _folderMethods.UpdateFolder(FolderMapper.GetFolderFromVm(folder));
                }
            }
            await _mainWindowViewModel.ExplorerVm.RefreshFolders();
        }

    }
}