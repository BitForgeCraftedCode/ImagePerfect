using Avalonia.Controls;
using ImagePerfect.Helpers;
using ImagePerfect.Models;
using ImagePerfect.ObjectMappers;
using ImagePerfect.Repository;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using Serilog;
using System;
using System.Collections.Generic;
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
        /*
         * The logs here narrowed this down:
         * DB update cover image for folder {FolderId}, Success={Success}
         * DB read-back cover image for folder {FolderId} at {FolderPath}: Requested={RequestedCoverImagePath}, Stored={StoredCoverImagePath}, Matches={Matches}
         * those two were always showing as true which told us the cover image path was being overwritten eleswhere by a stale Folder Object
         * this happend when the user Saved Dir went into a folder copied cover image to containing 
         * reloading Saved Dir than hit the folder rating on the folder without a cover. That would override the cover image back to null
         * if this still happens check the remaing UpdateFolder calls in FolderMethods those use the generic repo and overwrite the whold object.
         */
        public async Task CopyCoverImageToContainingFolder(FolderViewModel folderVm)
        {
            Log.Information("Starting CopyCoverImageToContainingFolder for FolderPath: {FolderPath}, CoverImagePath: {CoverImagePath}", folderVm.FolderPath, folderVm.CoverImagePath);
            if (PathHelper.RemoveOneFolderFromPath(folderVm.FolderPath) == _mainWindowViewModel.InitializeVm.RootFolderLocation)
            {
                await MessageBoxHelper.ShowAsync(
                    "Copy Cover",
                    $"Cannot copy cover image from root folder."
                );
                return;
            }
            if (string.IsNullOrEmpty(folderVm.CoverImagePath))
            {
                await MessageBoxHelper.ShowAsync(
                    "Copy Cover",
                    $"The folder must have a cover selected to copy."
                );
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
                bool boxResult = await MessageBoxHelper.ShowYesNoAsync(
                    "Copy Cover",
                    $"Containing folder already has a cover. Do you want to copy another?"
                );
                if (!boxResult)
                {
                    return;
                }
            }
            if (File.Exists(coverImageNewPath))
            {
                await MessageBoxHelper.ShowAsync(
                    "Copy Cover",
                    $"A cover image in the destination has the same file name. Pick a different cover."
                );
                return;
            }
            try
            {
                //add cover image path to containing folder
                bool success = await folderMethods.UpdateCoverImage(coverImageNewPath, containingFolder.FolderId);
                Log.Information("DB update cover image for folder {FolderId}, Success={Success}", containingFolder.FolderId, success);
                //oddly sometimes the above reports success but folder doesnt have the new cover. 
                //get the folder and log info
                Folder updatedContainingFolder = await folderMethods.GetFolderById(containingFolder.FolderId);
                Log.Information(
                    "DB read-back cover image for folder {FolderId} at {FolderPath}: Requested={RequestedCoverImagePath}, Stored={StoredCoverImagePath}, Matches={Matches}",
                    containingFolder.FolderId,
                    containingFolder.FolderPath,
                    coverImageNewPath,
                    updatedContainingFolder.CoverImagePath,
                    string.Equals(updatedContainingFolder.CoverImagePath, coverImageNewPath, StringComparison.Ordinal));
                if (!success)
                {
                    Log.Warning("Failed to update cover image in DB for folder {FolderId}", containingFolder.FolderId);
                    await MessageBoxHelper.ShowAsync(
                        "Copy Cover",
                        "The containing folder's cover image path could not be verified in the database. The image was not copied."
                    );
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
                    (List<Image> images, List<ImageTag> tags) imageResult = await imageMethods.GetAllImagesInFolder(folder.FolderId, _mainWindowViewModel.ExplorerVm.LoadImagesAscending);
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
