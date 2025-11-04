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
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace ImagePerfect.ViewModels
{
	public class MoveFolderToTrashViewModel : ViewModelBase
    {
        private readonly MySqlDataSource _dataSource;
        private readonly IConfiguration _configuration;
        private readonly MainWindowViewModel _mainWindowViewModel;
        public MoveFolderToTrashViewModel(MySqlDataSource dataSource, IConfiguration config, MainWindowViewModel mainWindowViewModel) 
		{
            _dataSource = dataSource;
            _configuration = config;
            _mainWindowViewModel = mainWindowViewModel;
        }

        public async Task MoveFolderToTrash(FolderViewModel folderVm)
        {
            await using UnitOfWork uow = await UnitOfWork.CreateAsync(_dataSource, _configuration);
            FolderMethods folderMethods = new FolderMethods(uow);
            //only allow delete if folder does not contain children/sub directories
            List<Folder> folderAndSubFolders = await folderMethods.GetDirectoryTree(folderVm.FolderPath);
            if (folderAndSubFolders.Count > 1) 
            {
                await MessageBoxManager.GetMessageBoxCustom(
                    new MessageBoxCustomParams
                    {
                        ButtonDefinitions = new List<ButtonDefinition>
                        {
                            new ButtonDefinition { Name = "Ok", },
                        },
                        ContentTitle = "Delete Folder",
                        ContentMessage = $"\"{folderVm.FolderName}\"\n\nThe above folder contains subfolders.\nPlease delete or move those first before removing this folder.",
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        SizeToContent = SizeToContent.Manual,
                        Width = 500,
                        Height = double.NaN,
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
                    ContentTitle = "Delete Folder",
                    ContentMessage = $"\"{folderVm.FolderName}\"\n\nAre you sure you want to move the above folder to trash?",
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    SizeToContent = SizeToContent.Manual, 
                    Width = 500,
                    Height= double.NaN,
                }
            );
            var boxResult = await boxYesNo.ShowWindowDialogAsync(Globals.MainWindow);
            if (boxResult == "Yes") 
            {
                _mainWindowViewModel.ShowLoading = true;
                //the folders parent
                string pathThatContainsFolder = PathHelper.RemoveOneFolderFromPath(folderVm.FolderPath);                
                Folder? rootFolder = await folderMethods.GetRootFolder();
                string trashFolderPath = PathHelper.GetTrashFolderPath(rootFolder.FolderPath);

                //create ImagePerfectTRASH if it doesnt exist
                if (!Directory.Exists(trashFolderPath))
                {
                    Directory.CreateDirectory(trashFolderPath);
                }
                if (Directory.Exists(folderVm.FolderPath))
                {
                    //delete folder from db -- does not delete sub folders.
                    //images table child of folders ON DELETE CASCADE is applied on sql to delete all images if a folder is deleted
                    bool success = await folderMethods.DeleteFolder(folderVm.FolderId);
                    if (success) 
                    {
                        //move folder to trash folder
                        string newFolderPath = PathHelper.GetFolderTrashPath(folderVm, trashFolderPath);
                        Directory.Move(folderVm.FolderPath, newFolderPath);
                        //update the parent folder HasChildren prop
                        List<Folder> parentFolderDirTree = await folderMethods.GetDirectoryTree(pathThatContainsFolder);
                        Folder parentFolder = await folderMethods.GetFolderAtDirectory(pathThatContainsFolder);
                        if (parentFolderDirTree.Count > 1)
                        {
                            parentFolder.HasChildren = true;
                            await folderMethods.UpdateFolder(parentFolder);
                        }
                        else
                        {
                            parentFolder.HasChildren = false;
                            await folderMethods.UpdateFolder(parentFolder);
                        }
                        //refresh UI
                        await _mainWindowViewModel.ExplorerVm.RefreshFolders(pathThatContainsFolder, uow);
                        _mainWindowViewModel.ShowLoading = false;
                    }
                }
            }
        }
	}
}