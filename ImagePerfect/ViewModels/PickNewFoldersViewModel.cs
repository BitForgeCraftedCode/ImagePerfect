using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using ImagePerfect.Helpers;
using ImagePerfect.Models;
using ImagePerfect.Repository.IRepository;
using MsBox.Avalonia;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Enums;
using MsBox.Avalonia.Models;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace ImagePerfect.ViewModels
{
	public class PickNewFoldersViewModel : ViewModelBase
	{
        private readonly IUnitOfWork _unitOfWork;
        private readonly FolderCsvMethods _folderCsvMethods;
        private readonly FolderMethods _folderMethods;
        private readonly MainWindowViewModel _mainWindowViewModel;
        public PickNewFoldersViewModel(IUnitOfWork unitOfWork, MainWindowViewModel mainWindowViewModel) 
		{
            _unitOfWork = unitOfWork;
            _folderMethods = new FolderMethods(_unitOfWork);
            _folderCsvMethods = new FolderCsvMethods(_unitOfWork);
            _mainWindowViewModel = mainWindowViewModel;
            _SelectNewFoldersInteraction = new Interaction<string, List<string>?>();
			SelectNewFoldersCommand = ReactiveCommand.CreateFromTask(SelectNewFolders);
		}

		private List<string>? _NewFolders;

		private readonly Interaction<string, List<string>?> _SelectNewFoldersInteraction;

        public Interaction<string, List<string>?> SelectNewFoldersInteraction { get { return _SelectNewFoldersInteraction; } }

		public ReactiveCommand<Unit, Unit> SelectNewFoldersCommand { get; }

		private async Task SelectNewFolders()
		{
            Folder? rootFolder = await _folderMethods.GetRootFolder();
            if (rootFolder == null)
            {
                await MessageBoxManager.GetMessageBoxCustom(
                    new MessageBoxCustomParams
                    {
                        ButtonDefinitions = new List<ButtonDefinition>
                        {
                            new ButtonDefinition { Name = "Ok", },
                        },
                        ContentTitle = "Add Folders",
                        ContentMessage = $"You need to add a root library folder first before new folders can be added to it.",
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        SizeToContent = SizeToContent.WidthAndHeight,  // <-- lets it grow with content
                        MinWidth = 500  // optional, so it doesn’t wrap too soon
                    }
                ).ShowWindowDialogAsync(Globals.MainWindow);
                return;
            }

            _NewFolders = await _SelectNewFoldersInteraction.Handle(rootFolder.FolderPath);
			//list will be empty if Cancel is pressed exit method
			if (_NewFolders.Count == 0) 
			{
				return;
			}
            //add check to make sure user is picking folders within the root libary directory
            string pathCheck = PathHelper.FormatPathFromFolderPicker(_NewFolders[0]);
            if (!pathCheck.Contains(rootFolder.FolderPath))
            {
                await MessageBoxManager.GetMessageBoxCustom(
                    new MessageBoxCustomParams
                    {
                        ButtonDefinitions = new List<ButtonDefinition>
                        {
                            new ButtonDefinition { Name = "Ok", },
                        },
                        ContentTitle = "Add Folders",
                        ContentMessage = $"You can only add folders that are within your root library folder.",
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        SizeToContent = SizeToContent.WidthAndHeight,  // <-- lets it grow with content
                        MinWidth = 500  // optional, so it doesn’t wrap too soon
                    }
                ).ShowWindowDialogAsync(Globals.MainWindow);
                return;
            }
            //check for parent folder -- must add parent folder 1st to prevent double import
            string parentDirectory = PathHelper.RemoveOneFolderFromPath(PathHelper.FormatPathFromFolderPicker(_NewFolders[0]));
            List<Folder> parentFolderDirTree = await _folderMethods.GetDirectoryTree(parentDirectory);
            if (parentFolderDirTree.Count == 0)
            {
                await MessageBoxManager.GetMessageBoxCustom(
                    new MessageBoxCustomParams
                    {
                        ButtonDefinitions = new List<ButtonDefinition>
                        {
                            new ButtonDefinition { Name = "Ok", },
                        },
                        ContentTitle = "Add Folders",
                        ContentMessage = $"Add the parent folder first.",
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        SizeToContent = SizeToContent.WidthAndHeight,  // <-- lets it grow with content
                        MinWidth = 500  // optional, so it doesn’t wrap too soon
                    }
                ).ShowWindowDialogAsync(Globals.MainWindow);
                return;
            }
            _mainWindowViewModel.ShowLoading = true;
            //check for zip files -- must extract zips 1st before import
            List<string> zipLocations = new List<string>();
            foreach (string folder in _NewFolders)
            {
                string folderPath = PathHelper.FormatPathFromFolderPicker(folder);
                if (Directory.Exists(folderPath)) 
                {
                    IEnumerable<string> zipFiles = Directory.EnumerateFiles(folderPath, "*.zip", SearchOption.AllDirectories);
                    if (zipFiles.Any())
                    {
                        zipLocations.Add(folderPath);
                    }
                }
            }
            if (zipLocations.Any()) 
            {
                string message = "One or more of the selected folders contain .zip files:\n\n" +
                     string.Join("\n", zipLocations) +
                     "\n\nPlease extract the zip files before adding these folders to the library.";

                await MessageBoxManager.GetMessageBoxCustom(
                    new MessageBoxCustomParams
                    {
                        ButtonDefinitions = new List<ButtonDefinition>
                        {
                            new ButtonDefinition { Name = "Ok", },
                        },
                        ContentTitle = "Add Folders",
                        CanResize = true,
                        ContentMessage = message, 
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        SizeToContent = SizeToContent.WidthAndHeight,
                        MinWidth = 500,
                        MinHeight = 600,
                       
                    }
                ).ShowWindowDialogAsync(Globals.MainWindow);

                _mainWindowViewModel.ShowLoading = false;
                return;
            }
            //check if any folders are already in db
            List<Folder> allFoldersInDb = await _folderMethods.GetAllFolders();
            List<string> foldersNotToAdd = new List<string>();
            for (int i = 0; i < _NewFolders.Count; i++) 
            {
                foreach (Folder folder in allFoldersInDb)
                {
                    if (folder.FolderPath == PathHelper.FormatPathFromFolderPicker(_NewFolders[i]))
                    {
                        foldersNotToAdd.Add(_NewFolders[i]);
                    }
                }
            }
            foreach (string path in foldersNotToAdd)
            {
                _NewFolders.Remove(path);
            }
            if (_NewFolders.Count == 0) 
            {
                await MessageBoxManager.GetMessageBoxCustom(
                    new MessageBoxCustomParams
                    {
                        ButtonDefinitions = new List<ButtonDefinition>
                        {
                            new ButtonDefinition { Name = "Ok", },
                        },
                        ContentTitle = "Add Folders",
                        ContentMessage = $"All the folders selected are already in the library.",
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        SizeToContent = SizeToContent.WidthAndHeight,  // <-- lets it grow with content
                        MinWidth = 500  // optional, so it doesn’t wrap too soon
                    }
                ).ShowWindowDialogAsync(Globals.MainWindow);
                _mainWindowViewModel.ShowLoading = false;
                return;
            }
            //build csv
            bool csvIsSet = await FolderCsvMethods.AddNewFoldersCsv(_NewFolders, false);
            //write csv to database
            if (csvIsSet) 
            {
                await _folderCsvMethods.AddFolderCsv();
                //reload the page
                await _mainWindowViewModel.RefreshFolders();
            }
            _mainWindowViewModel.ShowLoading = false;
        }
    }
}