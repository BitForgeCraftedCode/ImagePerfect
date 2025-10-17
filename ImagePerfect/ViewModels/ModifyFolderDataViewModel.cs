using System;
using System.Collections.Generic;
using ImagePerfect.Models;
using ImagePerfect.Repository.IRepository;
using MsBox.Avalonia.Enums;
using MsBox.Avalonia;
using ReactiveUI;
using ImagePerfect.ObjectMappers;
using System.Linq;
using System.Threading.Tasks;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Models;
using Avalonia.Controls;
using System.Diagnostics;
using ImagePerfect.Helpers;

namespace ImagePerfect.ViewModels
{
	public class ModifyFolderDataViewModel : ViewModelBase
	{
        private readonly IUnitOfWork _unitOfWork;
        private readonly FolderMethods _folderMethods;
        private readonly MainWindowViewModel _mainWindowViewModel;
        public ModifyFolderDataViewModel(IUnitOfWork unitOfWork, MainWindowViewModel mainWindowViewModel) 
		{
            _unitOfWork = unitOfWork;
            _mainWindowViewModel = mainWindowViewModel;
            _folderMethods = new FolderMethods(_unitOfWork);
        }

        public async void UpdateFolder(FolderViewModel folderVm, string fieldUpdated)
        {
            Folder folder = FolderMapper.GetFolderFromVm(folderVm);
            bool success = await _folderMethods.UpdateFolder(folder);
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

        public async void EditFolderTag(FolderViewModel folderVm)
        {
            if (folderVm.FolderTags == null || folderVm.FolderTags == "")
            {
                if (folderVm.Tags.Count == 1)
                {
                    await _folderMethods.DeleteFolderTag(folderVm.Tags[0]);
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
                await _folderMethods.DeleteFolderTag(tagToRemove);
            }
        }

        public async void AddFolderTag(FolderViewModel folderVm)
        {
            //click submit with empty input just return
            if (folderVm.NewTag == "" || folderVm.NewTag == null)
            {
                return;
            }
            Folder folder = FolderMapper.GetFolderFromVm(folderVm);
            //update folder table and tags table in db -- success will be false if you try to input a duplicate tag
            bool success = await _folderMethods.UpdateFolderTags(folder, folderVm.NewTag);
            if (success)
            {
                //Update TagsList to show in UI AutoCompleteBox clear NewTag in box as well and refresh folders to show new tag
                await _mainWindowViewModel.GetTagsList();
                folderVm.NewTag = "";
                //refresh UI
                await _mainWindowViewModel.ExplorerVm.RefreshFolderProps(_mainWindowViewModel.ExplorerVm.CurrentDirectory, folderVm);
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

            _mainWindowViewModel.ShowLoading = true;
            try
            {
                //select all folders from db with tag as List<Folder>
                (List<Folder> folders, List<FolderTag> tags) folderTagResult = await _folderMethods.GetAllFoldersWithTag(selectedTag.TagName, false, _mainWindowViewModel.ExplorerVm.CurrentDirectory);
                List<Folder> taggedFolders = folderTagResult.folders;
                //no taggedFolders returned just exit
                if(taggedFolders == null || taggedFolders.Count == 0)
                    return;

                //remove tag from database
                await _folderMethods.RemoveTagOnAllFolders(selectedTag);
                //Update TagsList to show in UI
                await _mainWindowViewModel.GetTagsList();
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

            // Get all folders in current directory
            (List<Folder> folders, List<FolderTag> tags) folderResults = await _folderMethods.GetFoldersInDirectory(_mainWindowViewModel.ExplorerVm.CurrentDirectory, _mainWindowViewModel.ExplorerVm.LoadFoldersAscending);
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
                await _folderMethods.AddTagToAllFoldersInCurrentDirectory(batches);
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