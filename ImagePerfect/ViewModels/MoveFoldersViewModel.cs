using ImagePerfect.Helpers;
using ImagePerfect.Models;
using ImagePerfect.Repository;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImagePerfect.ViewModels
{
    public class MoveFoldersViewModel : ViewModelBase
    {
        private readonly MySqlDataSource _dataSource;
        private readonly IConfiguration _configuration;
        private readonly MainWindowViewModel _mainWindowViewModel;

        public MoveFoldersViewModel(MySqlDataSource dataSource, IConfiguration config, MainWindowViewModel mainWindowViewModel)
        {
            _dataSource = dataSource;
            _configuration = config;
            _mainWindowViewModel = mainWindowViewModel;
        }

        public async Task MoveFolders(List<FolderViewModel> foldersToMove, string newFolderPath)
        {
            List<FolderViewModel> validFolders = new List<FolderViewModel>();
            List<string> errors = new List<string>();
            try
            {
                _mainWindowViewModel.ShowLoading = true;
                foreach (FolderViewModel folder in foldersToMove)
                {
                    string? error = await ValidateFolderMove(folder, newFolderPath);
                    if (error == null)
                        validFolders.Add(folder);
                    else
                        errors.Add($"{folder.FolderName}: {error}");
                }

                if (validFolders.Count > 0)
                    await MoveFoldersCore(validFolders, newFolderPath);

                if (errors.Count > 0)
                {
                    await MessageBoxHelper.ShowAsync("Move Folders", string.Join(Environment.NewLine, errors));
                }
                //update lib folders to show the folder has moved
                if(validFolders.Count > 0) 
                    await _mainWindowViewModel.ExplorerVm.RefreshFolders();
                _mainWindowViewModel.ShowLoading = false;
            }
            catch (Exception ex) 
            {
                Log.Error(ex, "Unexpected error moving folders");
                await MessageBoxHelper.ShowAsync(
                   "Move Folder",
                   $"Error moving the folder check the logs for more information."
               );
                return;
            }
            finally
            {
               
                _mainWindowViewModel.ShowLoading = false;
            }
            
        }

        private async Task<string?> ValidateFolderMove(FolderViewModel folderVm, string newFolderPath)
        {
            await using UnitOfWork uow = await UnitOfWork.CreateAsync(_dataSource, _configuration);
            FolderMethods folderMethods = new FolderMethods(uow);
            ImageMethods imageMethods = new ImageMethods(uow);
            Folder? rootFolder = await folderMethods.GetRootFolder();
            if (rootFolder == null)
                return "You need to add a root library folder first before you can move a folder in it.";
            if (!newFolderPath.Contains(rootFolder.FolderPath, StringComparison.OrdinalIgnoreCase)) //add check to make sure user is picking folders within the root libary directory
                return "The destination must be within your root library folder.";
            if (newFolderPath.Contains(folderVm.FolderPath, StringComparison.OrdinalIgnoreCase)) //Cannot move folder to one of its subfolders
                return "The destination is a subfolder of the source folder.";
            string destinationFolderPath = PathHelper.AddNewFolderNameToPathForDirectoryMoveFolder(newFolderPath, folderVm.FolderName);
            if (destinationFolderPath == folderVm.FolderPath) //checks if user selected the current location of the folder
                return "The folder is already in this location.";
            if (Directory.Exists(destinationFolderPath)) //checks if a folder with that name already exists in the chosen location
                return "A folder with this name already exists in the destination location.";
            try
            {
                List<Image> images = await imageMethods.GetAllImagesInDirectoryTree(folderVm.FolderPath);
                if (!images.Any())
                    return "The folder must have images imported to move it.";
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error validating folder move for {FolderPath}", folderVm.FolderPath);
                return "Could not validate this folder; check the logs for more information.";
            }
            return null;
        }

        private async Task MoveFoldersCore(List<FolderViewModel> foldersToMove, string newFolderPath)
        {
            await using UnitOfWork uow = await UnitOfWork.CreateAsync(_dataSource, _configuration);
            FolderMethods folderMethods = new FolderMethods(uow);
            ImageMethods imageMethods = new ImageMethods(uow);

            List<Folder> allFolders = new List<Folder>();
            List<Image> allImages = new List<Image>();

            foreach (FolderViewModel folderVm in foldersToMove)
            {
                try
                {
                    //GetAllImageInDirectoryTree casued a fatal error once. MySqlException Timeout expired before the operation completed
                    //pull current folder and sub folders from db
                    List<Folder> folders = await folderMethods.GetDirectoryTree(folderVm.FolderPath);
                    List<Image> images = await imageMethods.GetAllImagesInDirectoryTree(folderVm.FolderPath);
                    folders = PathHelper.ModifyFolderPathsForFolderMove(folders, folderVm.FolderName, newFolderPath);
                    images = PathHelper.ModifyImagePathsForFolderMove(images, folderVm.FolderName, newFolderPath);
                    allFolders.AddRange(folders);
                    allImages.AddRange(images);
                }
                catch (MySqlException ex)
                {
                    Log.Error(ex, "Database error while preparing folder move for {FolderPath}", folderVm.FolderPath);
                    await MessageBoxHelper.ShowAsync(
                        "Move Folder",
                        $"Error moving the folder check the logs for more information."
                    );
                    return;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Unexpected error while moving {FolderPath}", folderVm.FolderPath);
                    await MessageBoxHelper.ShowAsync(
                        "Move Folder",
                        $"Error moving the folder check the logs for more information."
                    );
                    return;
                }

            }

            if (allFolders.Count == 0 || allImages.Count == 0)
            {
                await MessageBoxHelper.ShowAsync("Move Folders", "Could not find the folders or images to move.");
                return;
            }

            string folderMoveSql = SqlStringBuilder.BuildFolderSqlForFolderMove(allFolders);
            string imageMoveSql = SqlStringBuilder.BuildImageSqlForFolderMove(allImages);
            bool success = await folderMethods.MoveFolder(folderMoveSql, imageMoveSql);
            if (!success)
            {
                await MessageBoxHelper.ShowAsync("Move Folders", "Sorry, something went wrong updating the database.");
                return;
            }
            //physically move the folders in the filesystem
            try
            {
                foreach (FolderViewModel folderVm in foldersToMove)
                    MoveFolder(folderVm, newFolderPath);
            }
            catch (Exception ex)
            {
                await MessageBoxHelper.ShowAsync(
                    "Move Folders",
                    $"The database was updated, but a folder could not be moved on disk. Check the logs for details.\n{ex.Message}");
                return;
            }

            //update the moveToFolder and parentOfTheFolderToMove HasChildren propery
            Folder moveToFolder = await folderMethods.GetFolderAtDirectory(newFolderPath);
            moveToFolder.HasChildren = true;
            await folderMethods.UpdateFolder(moveToFolder);
            //.Select(...) gets each folder's parent path, and .Distinct(...) removes duplicates
            //thus since foldersToMove will always all be in the same directory UpdateFolder is only called once to pudate the perentOfTheFolderToMove
            foreach (string parentPath in foldersToMove
                         .Select(folder => PathHelper.RemoveOneFolderFromPath(folder.FolderPath))
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                Folder parentOfTheFolderToMove = await folderMethods.GetFolderAtDirectory(parentPath);
                parentOfTheFolderToMove.HasChildren = Directory.GetDirectories(parentPath).Any();
                await folderMethods.UpdateFolder(parentOfTheFolderToMove);
            }
        }

        private void MoveFolder(FolderViewModel folderVm, string newFolderPath)
        {
            string destinationFolderPath = PathHelper.AddNewFolderNameToPathForDirectoryMoveFolder(newFolderPath, folderVm.FolderName);
            try
            {
                Directory.Move(folderVm.FolderPath, destinationFolderPath);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error moving folder from {SourcePath} to {DestinationPath}", folderVm.FolderPath, destinationFolderPath);
                throw;
            }
        }
    }
}
