using ImagePerfect.Models;
using ImagePerfect.Repository;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
            await using UnitOfWork uow = await UnitOfWork.CreateAsync(_dataSource, _configuration);
            FolderMethods folderMethods = new FolderMethods(uow);
            (List<Folder> folders, List<FolderTag> tags) result = await folderMethods.GetFoldersInDirectory(_mainWindowViewModel.ExplorerVm.CurrentDirectory, _mainWindowViewModel.ExplorerVm.LoadFoldersAscending);
            List<Folder> databaseCurrentDirectoryFolders = result.folders;

            HashSet<string> dbPaths = databaseCurrentDirectoryFolders.Select(f => f.FolderPath).ToHashSet(StringComparer.Ordinal);
            HashSet<string> diskPaths = Directory.GetDirectories(_mainWindowViewModel.ExplorerVm.CurrentDirectory).ToHashSet(StringComparer.Ordinal);
            /*
             * compair both lists
             * we want to get:
             * Folders found on disk but absent from db
             * db folders with no corresponding directory on disk
             * show case conflicts as well
             * So we must adhere to the Know limitaion below
             * Known limitation: On Ubuntu/Linux, where the filesystem is case-sensitive, importing folders named e.g., Photos/BeachTrip and Photos/beachtrip causes both folders to show each other's contents.
             * - When adding a library or new folders; any folders with the same name but different case will not be imported. 
             * A log file "case_conflict_folders.txt" will list the date and folders not imported. From there users must rename the folders and then import them.
             * 
             * so we need
             * 
             * Exact match: The folder exists both on disk and in the database.
                Case conflict: A folder differs only by case from another folder, either in the database or on disk.
                Not imported: A folder exists on disk but has no matching database entry or case conflict.
                Missing from disk: A database folder has no corresponding directory on disk and isn't explained by a case conflict.
             */

            //log differences to Missing_Folders.txt in app root directory. Also display message to user. no need to log all the exact matches we can just say 200 matched folders
        }
    }
}
