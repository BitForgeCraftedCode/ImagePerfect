using ImagePerfect.Helpers;
using ImagePerfect.Models;
using ImagePerfect.Repository;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using System.Threading.Tasks;

namespace ImagePerfect.ViewModels
{
	public class FavoriteFoldersViewModel : ViewModelBase
	{
        private readonly MySqlDataSource _dataSource;
        private readonly IConfiguration _configuration;

        public FavoriteFoldersViewModel(MySqlDataSource dataSource, IConfiguration config)
        {
            _dataSource = dataSource;
            _configuration = config;
        }

        public async Task SaveFolderAsFavorite(FolderViewModel folderVm)
        {
            await using UnitOfWork uow = await UnitOfWork.CreateAsync(_dataSource, _configuration);
            FolderMethods folderMethods = new FolderMethods(uow);
            await folderMethods.SaveFolderToFavorites(folderVm.FolderId);
        }

        public async Task RemoveAllFavoriteFolders()
        {
            bool boxResult = await MessageBoxHelper.ShowYesNoAsync(
                "Remove Favorite Folders",
                $"Are you sure you want to remove your favorite folders from the data base? The folders on the file system will remain."
            );
            if (boxResult)
            {
                await using UnitOfWork uow = await UnitOfWork.CreateAsync(_dataSource, _configuration);
                FolderMethods folderMethods = new FolderMethods(uow);
                await folderMethods.RemoveAllFavoriteFolders();
            }
            else
            {
                return;
            }
        }
    }
}