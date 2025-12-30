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
using Serilog;
using SixLabors.ImageSharp;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Image = ImagePerfect.Models.Image;

namespace ImagePerfect.ViewModels
{
	public class CoverImageViewModel : ViewModelBase
	{
        private readonly MySqlDataSource _dataSource;
        private readonly IConfiguration _configuration;
        private readonly MainWindowViewModel _mainWindowViewModel;
        public CoverImageViewModel(MySqlDataSource dataSource, IConfiguration config, MainWindowViewModel mainWindowViewModel) 
		{
            _dataSource = dataSource;
            _configuration = config;
            _mainWindowViewModel = mainWindowViewModel;
        }

        public async Task CopyCoverImageToContainingFolder(FolderViewModel folderVm)
        {
            Log.Information("Starting CopyCoverImageToContainingFolder for FolderPath: {FolderPath}, CoverImagePath: {CoverImagePath}", folderVm.FolderPath, folderVm.CoverImagePath);
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
            if (string.IsNullOrEmpty(folderVm.CoverImagePath))
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
            await using UnitOfWork uow = await UnitOfWork.CreateAsync(_dataSource, _configuration);
            FolderMethods folderMethods = new FolderMethods(uow);

            string coverImageCurrentPath = folderVm.CoverImagePath;
            string coverImageNewPath = PathHelper.GetCoverImagePathForCopyCoverImageToContainingFolder(folderVm);
            //Folder containingFolder = await folderMethods.GetFolderAtDirectory(PathHelper.RemoveOneFolderFromPath(folderVm.FolderPath));
            Log.Information("Calculated new cover path: {NewPath}", coverImageNewPath);

            Folder containingFolder = null;
            try
            {
                containingFolder = await folderMethods.GetFolderAtDirectory(PathHelper.RemoveOneFolderFromPath(folderVm.FolderPath));
                if (containingFolder == null)
                {
                    Log.Warning("No containing folder found for path: {FolderPath}", folderVm.FolderPath);
                    return;
                }
                Log.Information("Found containing folder: {ContainingFolderPath} (ID: {FolderId})", containingFolder.FolderPath, containingFolder.FolderId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error retrieving containing folder for path: {FolderPath}", folderVm.FolderPath);
                return;
            }
            if (!string.IsNullOrEmpty(containingFolder.CoverImagePath))
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
            try
            {
                //add cover image path to containing folder
                bool success = await folderMethods.UpdateCoverImage(coverImageNewPath, containingFolder.FolderId);
                Log.Information("DB update cover image for folder {FolderId}, Success={Success}", containingFolder.FolderId, success);
                if (!success)
                {
                    Log.Warning("Failed to update cover image in DB for folder {FolderId}", containingFolder.FolderId);
                    return;
                }
                //copy file in file system
                File.Copy(coverImageCurrentPath, coverImageNewPath);
                Log.Information("Copied cover image from {Source} to {Destination}", coverImageCurrentPath, coverImageNewPath);
            }
            catch (Exception ex) 
            {
                Log.Error(ex, "Failed to copy cover image from {Source} to {Destination}", coverImageCurrentPath, coverImageNewPath);
            }
            
        }

        public async Task AddCoverImageOnCurrentPage(ItemsControl foldersItemsControl)
        {
            List<FolderViewModel> allFolders = foldersItemsControl.Items.OfType<FolderViewModel>().ToList();
            Random random = new Random();
            await using UnitOfWork uow = await UnitOfWork.CreateAsync(_dataSource, _configuration);
            ImageMethods imageMethods = new ImageMethods(uow);
            FolderMethods folderMethods = new FolderMethods(uow);
            foreach (FolderViewModel folder in allFolders)
            {
                if (folder.HasFiles == true && folder.AreImagesImported == true)
                {
                    (List<Image> images, List<ImageTag> tags) imageResult = await imageMethods.GetAllImagesInFolder(folder.FolderId);
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
                    await folderMethods.UpdateFolder(FolderMapper.GetFolderFromVm(folder));
                }
            }
            await _mainWindowViewModel.ExplorerVm.RefreshFolders("", uow);
        }

    }
}