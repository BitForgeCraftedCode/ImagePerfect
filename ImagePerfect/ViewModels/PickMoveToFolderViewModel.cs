using Avalonia.Controls;
using ImagePerfect.Helpers;
using ImagePerfect.Models;
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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
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
			SelectMoveToFolderCommand = ReactiveCommand.CreateFromTask((FolderViewModel folderVm) => SelectMoveToFolder(folderVm));
		}

		private List<string>? _MoveToFolderPath;

		private Interaction<string, List<string>?> _SelectMoveToFolderInteration;

		public Interaction<string, List<string>?> SelectMoveToFolderInteration { get { return _SelectMoveToFolderInteration; } }

		public ReactiveCommand<FolderViewModel, Unit> SelectMoveToFolderCommand { get; }

		private async Task SelectMoveToFolder(FolderViewModel folderVm)
		{
            await using UnitOfWork uow = await UnitOfWork.CreateAsync(_dataSource, _configuration);
            FolderMethods folderMethods = new FolderMethods(uow);
            ImageMethods imageMethods = new ImageMethods(uow);
            Folder? rootFolder = await folderMethods.GetRootFolder();
            if (rootFolder == null)
            {
                await MessageBoxManager.GetMessageBoxCustom(
                    new MessageBoxCustomParams
                    {
                        ButtonDefinitions = new List<ButtonDefinition>
                        {
                            new ButtonDefinition { Name = "Ok", },
                        },
                        ContentTitle = "Move Folder",
                        ContentMessage = $"You need to add a root library folder first before you can move a folder in it.",
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        SizeToContent = SizeToContent.WidthAndHeight,  // <-- lets it grow with content
                        MinWidth = 500  // optional, so it doesn’t wrap too soon
                    }
                ).ShowWindowDialogAsync(Globals.MainWindow);
                return;
            }
            //pull current folder and sub folders from db
            List<Folder> folders = await folderMethods.GetDirectoryTree(folderVm.FolderPath);
            List<Image> images = await imageMethods.GetAllImagesInDirectoryTree(folderVm.FolderPath);
            if (!images.Any()) 
            {
                await MessageBoxManager.GetMessageBoxCustom(
                    new MessageBoxCustomParams
                    {
                        ButtonDefinitions = new List<ButtonDefinition>
                        {
                            new ButtonDefinition { Name = "Ok", },
                        },
                        ContentTitle = "Move Folder",
                        ContentMessage = $"The folder must have images imported to move it.",
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        SizeToContent = SizeToContent.WidthAndHeight,  // <-- lets it grow with content
                        MinWidth = 500  // optional, so it doesn’t wrap too soon
                    }
                ).ShowWindowDialogAsync(Globals.MainWindow);
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
                await MessageBoxManager.GetMessageBoxCustom(
                    new MessageBoxCustomParams
                    {
                        ButtonDefinitions = new List<ButtonDefinition>
                        {
                            new ButtonDefinition { Name = "Ok", },
                        },
                        ContentTitle = "Move Folder",
                        ContentMessage = $"You can only move folders that are within your root library folder.",
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        SizeToContent = SizeToContent.WidthAndHeight,  // <-- lets it grow with content
                        MinWidth = 500  // optional, so it doesn’t wrap too soon
                    }
                ).ShowWindowDialogAsync(Globals.MainWindow);
                return;
            }
            //Cannot move folder to one of its subfolders
            if (pathCheck.Contains(folderVm.FolderPath))
            {
                await MessageBoxManager.GetMessageBoxCustom(
                    new MessageBoxCustomParams
                    {
                        ButtonDefinitions = new List<ButtonDefinition>
                        {
                            new ButtonDefinition { Name = "Ok", },
                        },
                        ContentTitle = "Move Folder",
                        ContentMessage = $"The destination folder is a subfolder of the source folder. Cannot do this.",
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        SizeToContent = SizeToContent.WidthAndHeight,  // <-- lets it grow with content
                        MinWidth = 500  // optional, so it doesn’t wrap too soon
                    }
                ).ShowWindowDialogAsync(Globals.MainWindow);
                return;
            }
            //move folder in db
            string newFolderPath = PathHelper.FormatPathFromFolderPicker(_MoveToFolderPath[0]);
            if (PathHelper.AddNewFolderNameToPathForDirectoryMoveFolder(newFolderPath, folderVm.FolderName) == folderVm.FolderPath)
            {
                await MessageBoxManager.GetMessageBoxCustom(
                    new MessageBoxCustomParams
                    {
                        ButtonDefinitions = new List<ButtonDefinition>
                        {
                            new ButtonDefinition { Name = "Ok", },
                        },
                        ContentTitle = "Move Folder",
                        ContentMessage = $"The folder is already in this location.",
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        SizeToContent = SizeToContent.WidthAndHeight,  // <-- lets it grow with content
                        MinWidth = 500  // optional, so it doesn’t wrap too soon
                    }
                ).ShowWindowDialogAsync(Globals.MainWindow);
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
                    await MessageBoxManager.GetMessageBoxCustom(
                        new MessageBoxCustomParams
                        {
                            ButtonDefinitions = new List<ButtonDefinition>
                            {
                                new ButtonDefinition { Name = "Ok", },
                            },
                            ContentTitle = "Move Folder",
                            ContentMessage = $"Sorry something went wrong. \n {e}",
                            WindowStartupLocation = WindowStartupLocation.CenterOwner,
                            SizeToContent = SizeToContent.WidthAndHeight,  // <-- lets it grow with content
                            MinWidth = 500  // optional, so it doesn’t wrap too soon
                        }
                    ).ShowWindowDialogAsync(Globals.MainWindow);
                    _mainWindowViewModel.ShowLoading = false;
                    return;
                }
            }
            else
            {
                await MessageBoxManager.GetMessageBoxCustom(
                    new MessageBoxCustomParams
                    {
                        ButtonDefinitions = new List<ButtonDefinition>
                        {
                            new ButtonDefinition { Name = "Ok", },
                        },
                        ContentTitle = "Move Folder",
                        ContentMessage = $"Sorry something went wrong",
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        SizeToContent = SizeToContent.WidthAndHeight,  // <-- lets it grow with content
                        MinWidth = 500  // optional, so it doesn’t wrap too soon
                    }
                ).ShowWindowDialogAsync(Globals.MainWindow);
                _mainWindowViewModel.ShowLoading = false;
                return;
            }
            _mainWindowViewModel.ShowLoading = false;
        }
    }
}