using ImagePerfect.Helpers;
using ImagePerfect.Models;
using ImagePerfect.Repository;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ImagePerfect.ViewModels
{
    public class CheckForMissingFoldersViewModel : ViewModelBase
    {
        private readonly MySqlDataSource _dataSource;
        private readonly IConfiguration _configuration;
        private readonly MainWindowViewModel _mainWindowViewModel;

        public CheckForMissingFoldersViewModel(MySqlDataSource dataSource, IConfiguration config, MainWindowViewModel mainWindowViewModel)
        {
            _dataSource = dataSource;
            _configuration = config;
            _mainWindowViewModel = mainWindowViewModel;
        }

        public async Task CheckDirectoryForMissingFolders()
        {
            string currentDirectory = _mainWindowViewModel.ExplorerVm.CurrentDirectory;
            await using UnitOfWork uow = await UnitOfWork.CreateAsync(_dataSource, _configuration);
            FolderMethods folderMethods = new FolderMethods(uow);
            Folder? rootFolder = await folderMethods.GetRootFolder();
            if (rootFolder == null)
                return;
            if(PathHelper.RemoveOneFolderFromPath(rootFolder.FolderPath) == currentDirectory)
            {
                await MessageBoxHelper.ShowAsync(
                    "Check for Missing Folders",
                    $"You cannot check for missing folders from the root directory.");
                return;
            }

            (List<Folder> folders, List<FolderTag> tags) result = await folderMethods.GetFoldersInDirectory(
                currentDirectory,
                _mainWindowViewModel.ExplorerVm.LoadFoldersAscending);
            List<Folder> databaseCurrentDirectoryFolders = result.folders;

            HashSet<string> dbPaths = databaseCurrentDirectoryFolders
                .Select(folder => folder.FolderPath)
                .ToHashSet(StringComparer.Ordinal);

            HashSet<string> diskPaths = Directory.GetDirectories(currentDirectory)
                .ToHashSet(StringComparer.Ordinal);

            HashSet<string> exactMatches = dbPaths
                .Intersect(diskPaths, StringComparer.Ordinal)
                .ToHashSet(StringComparer.Ordinal);

            List<string> notImported = diskPaths
                .Except(exactMatches, StringComparer.Ordinal)
                .ToList();

            List<string> missingFromDisk = dbPaths
                .Except(exactMatches, StringComparer.Ordinal)
                .ToList();

            List<List<string>> caseConflictGroups = CaseConflictsHelper.FindCaseConflictGroups(dbPaths,diskPaths);

            //flatten the nested lists into HashSet
            HashSet<string> caseConflictPaths = caseConflictGroups
                .SelectMany(group => group)
                .ToHashSet(StringComparer.Ordinal);

            //use that HashSet to remove case-conflict paths from the other two result lists so they aren't reported twice
            notImported.RemoveAll(path => caseConflictPaths.Contains(path));
            missingFromDisk.RemoveAll(path => caseConflictPaths.Contains(path));

            List<string> differences = new List<string>();
            if (caseConflictGroups.Count > 0)
            {
                differences.Add("Case conflicts:");
                foreach (List<string> conflictGroup in caseConflictGroups)
                {
                    //Select() transforms each item
                    //AddRange(List<string>) adds each string in the list to the end of the differences list
                    differences.AddRange(conflictGroup.Select(path => $"  {path}"));
                }
            }
            if (notImported.Count > 0)
            {
                differences.Add("Folders on disk but not in the database (not imported):");
                differences.AddRange(notImported.Select(path => $"  {path}"));
            }
            if (missingFromDisk.Count > 0)
            {
                differences.Add("Folders in the database but missing from disk:");
                differences.AddRange(missingFromDisk.Select(path => $"  {path}"));
            }

            if (differences.Count == 0)
            {
                await MessageBoxHelper.ShowAsync(
                    "Check for Missing Folders",
                    $"All {exactMatches.Count} direct child folders match between the database and disk.");
                return;
            }

            string logPath = Path.Combine(Directory.GetCurrentDirectory(), "Missing_Folders.txt");
            List<string> logLines = new List<string>
            {
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Folder check for: {currentDirectory}",
                $"Exact matches: {exactMatches.Count}"
            };
            logLines.AddRange(differences);
            logLines.Add(string.Empty);
            await File.AppendAllLinesAsync(logPath, logLines);

            string summary = $"Exact matches: {exactMatches.Count}\n" +
                $"Case conflict groups: {caseConflictGroups.Count}\n" +
                $"On disk but not in database: {notImported.Count}\n" +
                $"In database but missing from disk: {missingFromDisk.Count}\n\n" +
                $"Details were appended to:\n{logPath}";
            await MessageBoxHelper.ShowAsync("Check for Missing Folders", summary);
        }
    }
}
