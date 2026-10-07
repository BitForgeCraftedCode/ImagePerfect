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

        /*
         * a separate UnitOfWork per folder can isolate failures somewhat: each folder gets its own database connection and lifetime, 
         * so a connection or unit-of-work problem for one move is less likely to affect the next.
         */
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

                foreach (FolderViewModel folder in validFolders)
                {
                    await MoveFolder(folder, newFolderPath);
                }

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
            if (destinationFolderPath == folderVm.FolderPath)
                return "The folder is already in this location.";
            if (Directory.Exists(destinationFolderPath))
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

        private async Task MoveFolder(FolderViewModel folderVm, string newFolderPath)
        {

            await using UnitOfWork uow = await UnitOfWork.CreateAsync(_dataSource, _configuration);
            FolderMethods folderMethods = new FolderMethods(uow);
            ImageMethods imageMethods = new ImageMethods(uow);
           
            List<Folder> folders = new List<Folder>();
            List<Image> images = new List<Image>();
            try
            {
                //GetAllImageInDirectoryTree casued a fatal error once. MySqlException Timeout expired before the operation completed
                //pull current folder and sub folders from db
                folders = await folderMethods.GetDirectoryTree(folderVm.FolderPath);
                images = await imageMethods.GetAllImagesInDirectoryTree(folderVm.FolderPath);
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
           
            //move folder in db
            string destinationFolderPath = PathHelper.AddNewFolderNameToPathForDirectoryMoveFolder(newFolderPath, folderVm.FolderName);
            //checks if user selected the current location of the folder
            if (destinationFolderPath == folderVm.FolderPath)
            {
                await MessageBoxHelper.ShowAsync(
                    "Move Folder",
                    $"The folder is already in this location."
                );
                return;
            }
            //checks if a folder with that name already exists in the chosen location
            if (Directory.Exists(destinationFolderPath))
            {
                await MessageBoxHelper.ShowAsync(
                    "Move Folder",
                    $"A folder with this name already exists in the destination location."
                );
                return;
            }

            //modify folder path and folder, cover image path, and images
            folders = PathHelper.ModifyFolderPathsForFolderMove(folders, folderVm.FolderName, newFolderPath);
            images = PathHelper.ModifyImagePathsForFolderMove(images, folderVm.FolderName, newFolderPath);

            //build sql string and update db
            string folderMoveSql = SqlStringBuilder.BuildFolderSqlForFolderMove(folders);
            string imageMoveSql = SqlStringBuilder.BuildImageSqlForFolderMove(images);

            Folder moveToFolder = await folderMethods.GetFolderAtDirectory(newFolderPath);
            Folder parentOfTheFolderToMove = await folderMethods.GetFolderAtDirectory(PathHelper.RemoveOneFolderFromPath(folderVm.FolderPath));
            //move images and folders in db do both in a transaction
            bool success = await folderMethods.MoveFolder(folderMoveSql, imageMoveSql);
            //move folder in filesystem if db move is successfull
            if (success)
            {
                try
                {
                    Directory.Move(folderVm.FolderPath, PathHelper.AddNewFolderNameToPathForDirectoryMoveFolder(newFolderPath, folderVm.FolderName));
                    //update the moveToFolder and parentOfTheFolderToMove HasChildren propery
                    moveToFolder.HasChildren = true;
                    parentOfTheFolderToMove.HasChildren = Directory.GetDirectories(parentOfTheFolderToMove.FolderPath).Any();
                    await folderMethods.UpdateFolder(moveToFolder);
                    await folderMethods.UpdateFolder(parentOfTheFolderToMove);
                }
                catch (Exception e)
                {
                    await MessageBoxHelper.ShowAsync(
                        "Move Folder",
                        $"Sorry something went wrong. \n {e}"
                    );
                    return;
                }
            }
            else
            {
                await MessageBoxHelper.ShowAsync(
                    "Move Folder",
                    $"Sorry something went wrong"
                );
                return;
            }

        }


    }
}
