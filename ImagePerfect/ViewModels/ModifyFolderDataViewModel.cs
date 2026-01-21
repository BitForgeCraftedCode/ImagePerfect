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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace ImagePerfect.ViewModels
{
	public class ModifyFolderDataViewModel : ViewModelBase
	{
        private readonly MySqlDataSource _dataSource;
        private readonly IConfiguration _configuration;
        private readonly MainWindowViewModel _mainWindowViewModel;
        public ModifyFolderDataViewModel(MySqlDataSource dataSource, IConfiguration config, MainWindowViewModel mainWindowViewModel) 
		{
            _dataSource = dataSource;
            _configuration = config;
            _mainWindowViewModel = mainWindowViewModel;
        }

        public async Task UpdateFolder(FolderViewModel folderVm, string fieldUpdated)
        {
            await using UnitOfWork uow = await UnitOfWork.CreateAsync(_dataSource, _configuration);
            FolderMethods folderMethods = new FolderMethods(uow);

            Folder folder = FolderMapper.GetFolderFromVm(folderVm);
            bool success = await folderMethods.UpdateFolder(folder);
            if (!success)
            {
                await MessageBoxManager.GetMessageBoxCustom(
                    new MessageBoxCustomParams
                    {
                        ButtonDefinitions = new List<ButtonDefinition>
                        {
                            new ButtonDefinition { Name = "Ok", },
                        },
                        ContentTitle = $"Add {fieldUpdated}",
                        ContentMessage = $"Folder {fieldUpdated} update error. Try again",
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        SizeToContent = SizeToContent.WidthAndHeight,  // <-- lets it grow with content
                        MinWidth = 500  // optional, so it doesn’t wrap too soon
                    }
                ).ShowWindowDialogAsync(Globals.MainWindow);
                return;
            }
        }

        public async Task EditFolderTag(FolderViewModel folderVm)
        {
            await using UnitOfWork uow = await UnitOfWork.CreateAsync(_dataSource, _configuration);
            FolderMethods folderMethods = new FolderMethods(uow);

            if (folderVm.FolderTags == null || folderVm.FolderTags == "")
            {
                if (folderVm.Tags.Count == 1)
                {
                    await folderMethods.DeleteFolderTag(folderVm.Tags[0]);
                }
                else if (folderVm.Tags.Count == 0)
                {
                    return;
                }
            }
            List<string> folderTags = folderVm.FolderTags.Split(",").ToList();
            FolderTag? tagToRemove = null;
            foreach (FolderTag tag in folderVm.Tags)
            {
                if (!folderTags.Contains(tag.TagName))
                {
                    tagToRemove = tag;
                }
            }
            if (tagToRemove != null)
            {
                await folderMethods.DeleteFolderTag(tagToRemove);
            }
        }

        public async Task AddFolderTag(FolderViewModel folderVm)
        {
            //click submit with empty input just return
            if (folderVm.NewTag == "" || folderVm.NewTag == null)
            {
                return;
            }

            await using UnitOfWork uow = await UnitOfWork.CreateAsync(_dataSource, _configuration);
            FolderMethods folderMethods = new FolderMethods(uow);

            Folder folder = FolderMapper.GetFolderFromVm(folderVm);
            //update folder table and tags table in db -- success will be false if you try to input a duplicate tag
            bool success = await folderMethods.UpdateFolderTags(folder, folderVm.NewTag);
            if (success)
            {
                //Update TagsList to show in UI AutoCompleteBox clear NewTag in box as well and refresh folders to show new tag
                await _mainWindowViewModel.GetTagsList(uow);
                folderVm.NewTag = "";
                //refresh UI
                await _mainWindowViewModel.ExplorerVm.RefreshFolderProps(_mainWindowViewModel.ExplorerVm.CurrentDirectory, folderVm, uow);
            }
            else
            {
                folderVm.NewTag = "";
            }
        }

        public async Task RemoveTagOnAllFolders(Tag selectedTag)
        {
            //nothing selected just return
            if (selectedTag == null)
                return;
            var boxYesNo = MessageBoxManager.GetMessageBoxCustom(
            new MessageBoxCustomParams
                {
                    ButtonDefinitions = new List<ButtonDefinition>
                        {
                            new ButtonDefinition { Name = "Yes", },
                            new ButtonDefinition { Name = "No", },
                        },
                    ContentTitle = "Remove Tag",
                    ContentMessage = $"CAUTION you are about to remove a tag this could take a long time are you sure?",
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    SizeToContent = SizeToContent.WidthAndHeight,  // <-- lets it grow with content
                    MinWidth = 500  // optional, so it doesn’t wrap too soon
                }
            );
            var boxResult = await boxYesNo.ShowWindowDialogAsync(Globals.MainWindow);
            if (boxResult != "Yes")
                return;

            await using UnitOfWork uow = await UnitOfWork.CreateAsync(_dataSource, _configuration);
            FolderMethods folderMethods = new FolderMethods(uow);

            _mainWindowViewModel.ShowLoading = true;
            try
            {
                //select all folders from db with tag as List<Folder>
                (List<Folder> folders, List<FolderTag> tags) folderTagResult = await folderMethods.GetAllFoldersWithTags(new List<string> { selectedTag.TagName }, false, _mainWindowViewModel.ExplorerVm.CurrentDirectory);
                List<Folder> taggedFolders = folderTagResult.folders;
                //no taggedFolders returned just exit
                if(taggedFolders == null || taggedFolders.Count == 0)
                    return;

                //remove tag from database
                await folderMethods.RemoveTagOnAllFolders(selectedTag);
                //Update TagsList to show in UI
                await _mainWindowViewModel.GetTagsList(uow);
            }
            finally
            {
                _mainWindowViewModel.ShowLoading = false;
            }
        }

        public async Task AddTagToAllFoldersInCurrentDirectory(Tag selectedTag)
        {
            //nothing selected just return
            if (selectedTag == null)
                return;

            await using UnitOfWork uow = await UnitOfWork.CreateAsync(_dataSource, _configuration);
            FolderMethods folderMethods = new FolderMethods(uow);

            // Get all folders in current directory
            (List<Folder> folders, List<FolderTag> tags) folderResults = await folderMethods.GetFoldersInDirectory(_mainWindowViewModel.ExplorerVm.CurrentDirectory, _mainWindowViewModel.ExplorerVm.LoadFoldersAscending);
            List<Folder> folders = folderResults.folders;

            var boxYesNo = MessageBoxManager.GetMessageBoxCustom(
            new MessageBoxCustomParams
            {
                ButtonDefinitions = new List<ButtonDefinition>
                        {
                            new ButtonDefinition { Name = "Yes", },
                            new ButtonDefinition { Name = "No", },
                        },
                ContentTitle = "Add Tag To All Folders",
                ContentMessage = $"You're about to add the tag {selectedTag.TagName} to all {folders.Count} folders in the current directory.\n\n"
                + "Important: This action affects only the folders in the current directory - not any folders shown because of filters or searches. \n\n"
                + "Make sure you have the current directory loaded before continuing.\nDo you want to proceed?",
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                SizeToContent = SizeToContent.WidthAndHeight,  // <-- lets it grow with content
                MinWidth = 500  // optional, so it doesn’t wrap too soon
            }
            );
            var boxResult = await boxYesNo.ShowWindowDialogAsync(Globals.MainWindow);
            if (boxResult != "Yes")
                return;
            _mainWindowViewModel.ShowLoading = true;
            try
            {
                const int batchSize = 500;
                List<string> batches = new List<string>();
                for (int i = 0; i < folders.Count; i += batchSize) 
                { 
                    List<Folder> batch = folders.Skip(i).Take(batchSize).ToList();
                    string batchSql = SqlStringBuilder.BuildSqlForBulkInsertFolderTag(selectedTag, batch);
                    batches.Add(batchSql);
                }
                await folderMethods.AddTagToAllFoldersInCurrentDirectory(batches);
                // Refresh UI
                await _mainWindowViewModel.DirectoryNavigationVm.LoadCurrentDirectory();
            }
            finally
            {
                _mainWindowViewModel.ShowLoading = false;
            }

        }
    }
}