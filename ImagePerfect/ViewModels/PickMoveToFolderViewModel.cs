using ImagePerfect.Helpers;
using ImagePerfect.Models;
using ImagePerfect.Repository;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using ReactiveUI;
using ReactiveUI.Primitives;
using Serilog;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Image = ImagePerfect.Models.Image;

namespace ImagePerfect.ViewModels
{
	public class PickMoveToFolderViewModel : ViewModelBase
	{
        private readonly MySqlDataSource _dataSource;
        private readonly IConfiguration _configuration;
        private readonly MainWindowViewModel _mainWindowViewModel;
        public PickMoveToFolderViewModel(MySqlDataSource dataSource, IConfiguration config, MainWindowViewModel mainWindowViewModel) 
		{
            _dataSource = dataSource;
            _configuration = config;
            _mainWindowViewModel = mainWindowViewModel;

            _SelectMoveToFolderInteration = new Interaction<string, List<string>?>();
			SelectMoveToFolderCommand = ReactiveCommand.Create(async (IList? selectedFolders) => await SelectMoveToFolder(selectedFolders));
		}

		private List<string>? _MoveToFolderPath;

		private Interaction<string, List<string>?> _SelectMoveToFolderInteration;

		public Interaction<string, List<string>?> SelectMoveToFolderInteration { get { return _SelectMoveToFolderInteration; } }

		public ReactiveCommand<IList?, Task> SelectMoveToFolderCommand { get; }

		private async Task SelectMoveToFolder(IList? selectedFolders)
		{
            if (selectedFolders is null || selectedFolders.Count == 0)
            {
                await MessageBoxHelper.ShowAsync(
                    "Move Folders",
                    $"You need to select folders to move."
                );
                return;
            }
            List<FolderViewModel> foldersToMove = selectedFolders.OfType<FolderViewModel>().ToList();
            //foreach (var folder in foldersToMove) 
            //{
            //    Debug.WriteLine(folder.FolderName);
            //}
            //for now this is just move 1 folder
            if (selectedFolders.Count > 1)
                return;

            
            FolderViewModel folderVm = foldersToMove.First();

            await using UnitOfWork uow = await UnitOfWork.CreateAsync(_dataSource, _configuration);
            FolderMethods folderMethods = new FolderMethods(uow);
            ImageMethods imageMethods = new ImageMethods(uow);
            Folder? rootFolder = await folderMethods.GetRootFolder();
            if (rootFolder == null)
            {
                await MessageBoxHelper.ShowAsync(
                    "Move Folder",
                    $"You need to add a root library folder first before you can move a folder in it."
                );
                return;
            }
            List<Folder> folders = new List<Folder>();
            List<Image> images = new List<Image>();
            try
            {
                //GetAllImageInDirectoryTree casued a fatal error once. MySqlException Timeout expired before the operation completed
                //pull current folder and sub folders from db
                folders = await folderMethods.GetDirectoryTree(folderVm.FolderPath);
                images = await imageMethods.GetAllImagesInDirectoryTree(folderVm.FolderPath); 
            }
            catch(MySqlException ex)
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
            
            if (!images.Any()) 
            {
                await MessageBoxHelper.ShowAsync(
                    "Move Folder",
                    $"The folder must have images imported to move it."
                );
                return;
            }

            _MoveToFolderPath = await _SelectMoveToFolderInteration.Handle(_mainWindowViewModel.ExplorerVm.CurrentDirectory);
			//list will be empty if Cancel is pressed exit method
			if (_MoveToFolderPath.Count == 0) 
			{ 
				return;
			}
            //add check to make sure user is picking folders within the root libary directory
            string pathCheck = PathHelper.FormatPathFromFolderPicker(_MoveToFolderPath[0]);
            if (!pathCheck.Contains(rootFolder.FolderPath))
            {
                await MessageBoxHelper.ShowAsync(
                    "Move Folder",
                    $"You can only move folders that are within your root library folder."
                );
                return;
            }
            //Cannot move folder to one of its subfolders
            if (pathCheck.Contains(folderVm.FolderPath))
            {
                await MessageBoxHelper.ShowAsync(
                    "Move Folder",
                    $"The destination folder is a subfolder of the source folder. Cannot do this."
                );
                return;
            }
            //move folder in db
            string newFolderPath = PathHelper.FormatPathFromFolderPicker(_MoveToFolderPath[0]);
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
            _mainWindowViewModel.ShowLoading = true;
            
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
                    //update lib folders to show the folder has moved
                    string foldersDirectoryPath = PathHelper.RemoveOneFolderFromPath(folderVm.FolderPath);
                    await _mainWindowViewModel.ExplorerVm.RefreshFolders(foldersDirectoryPath, uow);
                }
                catch (Exception e)
                {
                    await MessageBoxHelper.ShowAsync(
                        "Move Folder",
                        $"Sorry something went wrong. \n {e}"
                    );
                    _mainWindowViewModel.ShowLoading = false;
                    return;
                }
            }
            else
            {
                await MessageBoxHelper.ShowAsync(
                    "Move Folder",
                    $"Sorry something went wrong"
                );
                _mainWindowViewModel.ShowLoading = false;
                return;
            }
            _mainWindowViewModel.ShowLoading = false;
        }
    }
}