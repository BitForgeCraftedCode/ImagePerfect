using ImagePerfect.Helpers;
using ImagePerfect.Models;
using ImagePerfect.Repository;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using ReactiveUI;
using ReactiveUI.Primitives;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace ImagePerfect.ViewModels
{
	public class PickNewFoldersViewModel : ViewModelBase
	{
        private readonly MySqlDataSource _dataSource;
        private readonly IConfiguration _configuration;
        private readonly MainWindowViewModel _mainWindowViewModel;
        public PickNewFoldersViewModel(MySqlDataSource dataSource, IConfiguration config, MainWindowViewModel mainWindowViewModel) 
		{
            _dataSource = dataSource;
            _configuration = config;
            _mainWindowViewModel = mainWindowViewModel;
            _SelectNewFoldersInteraction = new Interaction<string, List<string>?>();
			SelectNewFoldersCommand = ReactiveCommand.CreateFromTask(SelectNewFolders);
		}

		private List<string>? _NewFolders;

		private readonly Interaction<string, List<string>?> _SelectNewFoldersInteraction;

        public Interaction<string, List<string>?> SelectNewFoldersInteraction { get { return _SelectNewFoldersInteraction; } }

		public ReactiveCommand<RxVoid, RxVoid> SelectNewFoldersCommand { get; }

		private async Task SelectNewFolders()
		{
            await using UnitOfWork uow = await UnitOfWork.CreateAsync(_dataSource, _configuration);
            FolderMethods folderMethods = new FolderMethods(uow);
            FolderCsvMethods folderCsvMethods = new FolderCsvMethods(uow);

            Folder? rootFolder = await folderMethods.GetRootFolder();
            if (rootFolder == null)
            {
                await MessageBoxHelper.ShowAsync(
                    "Add Folders",
                    $"You need to add a root library folder first before new folders can be added to it."
                );
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
                await MessageBoxHelper.ShowAsync(
                    "Add Folders",
                    $"You can only add folders that are within your root library folder."
                );
                return;
            }
            //check for parent folder -- must add parent folder 1st to prevent double import
            string parentDirectory = PathHelper.RemoveOneFolderFromPath(PathHelper.FormatPathFromFolderPicker(_NewFolders[0]));
            List<Folder> parentFolderDirTree = await folderMethods.GetDirectoryTree(parentDirectory);
            if (parentFolderDirTree.Count == 0)
            {
                await MessageBoxHelper.ShowAsync(
                    "Add Folders",
                    $"Add the parent folder first."
                );
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

                await MessageBoxHelper.ShowAsync(
                    "Add Folders",
                    message,
                    true,
                    600
                );
                _mainWindowViewModel.ShowLoading = false;
                return;
            }
            //check if any folders are already in db
            (List<Folder> folders, List<FolderTag> tags) result = await folderMethods.GetFoldersInDirectory(
                parentDirectory,
                _mainWindowViewModel.ExplorerVm.LoadFoldersAscending);
            List<Folder> databaseCurrentDirectoryFolders = result.folders;
            List<string> foldersNotToAdd = new List<string>();
            for (int i = 0; i < _NewFolders.Count; i++) 
            {
                foreach (Folder folder in databaseCurrentDirectoryFolders)
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
                await MessageBoxHelper.ShowAsync(
                    "Add Folders",
                    $"All the folders selected are already in the library."
                );
                _mainWindowViewModel.ShowLoading = false;
                return;
            }
            //check for case conflicts
            List<string> dbPaths = databaseCurrentDirectoryFolders.Select(folder => folder.FolderPath).ToList();
            List<string> formattedNewFolderPaths = _NewFolders.Select(x => PathHelper.FormatPathFromFolderPicker(x)).ToList();
            List<List<string>> caseConflictGroups = CaseConflictsHelper.FindCaseConflictGroups(dbPaths, formattedNewFolderPaths);
            
            if (caseConflictGroups.Count > 0)
            {
                List<string> differences = new List<string>();
                differences.Add("Case conflicts:");
                foreach (List<string> conflictGroup in caseConflictGroups)
                {
                    //Select() transforms each item
                    //AddRange(List<string>) adds each string in the list to the end of the differences list
                    differences.AddRange(conflictGroup.Select(path => $"  {path}"));
                }
                //flatten the nested lists into HashSet
                HashSet<string> caseConflictPaths = caseConflictGroups
                    .SelectMany(group => group)
                    .ToHashSet(StringComparer.Ordinal);

                //remove conflicting picker paths, comparing their formatted form to the conflict paths
                _NewFolders.RemoveAll(path => caseConflictPaths.Contains(PathHelper.FormatPathFromFolderPicker(path)));
                string logPath = Path.Combine(Directory.GetCurrentDirectory(), "case_conflict_folders.txt");
                List<string> logLines = new List<string>
                {
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Folder check for: {parentDirectory}",
                };
                logLines.AddRange(differences);
                logLines.Add(string.Empty);
                await File.AppendAllLinesAsync(logPath, logLines);
                await MessageBoxHelper.ShowAsync("Add new folders", "Case conflicts were found check case_conflict_folders.txt for a list of folders not added.");
                
                if (_NewFolders.Count == 0)
                {
                    _mainWindowViewModel.ShowLoading = false;
                    return;
                }
            }
            //build csv
            bool csvIsSet = await FolderCsvMethods.AddNewFoldersCsv(_NewFolders, false);
            //write csv to database
            if (csvIsSet) 
            {
                await folderCsvMethods.AddFolderCsv();
                //reload the page
                await _mainWindowViewModel.ExplorerVm.RefreshFolders("", uow);
            }
            _mainWindowViewModel.ShowLoading = false;
        }
    }
}