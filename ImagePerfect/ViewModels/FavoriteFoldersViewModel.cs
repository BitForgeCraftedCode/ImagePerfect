using Avalonia.Controls;
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
            var boxYesNo = MessageBoxManager.GetMessageBoxCustom(
                new MessageBoxCustomParams
                {
                    ButtonDefinitions = new List<ButtonDefinition>
                        {
                            new ButtonDefinition { Name = "Yes", },
                            new ButtonDefinition { Name = "No", },
                        },
                    ContentTitle = "Remove Favorite Folders",
                    ContentMessage = $"Are you sure you want to remove your favorite folders from the data base? The folders on the file system will remain.",
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    SizeToContent = SizeToContent.WidthAndHeight,  // <-- lets it grow with content
                    MinWidth = 500  // optional, so it doesn’t wrap too soon
                }
            );
            var boxResult = await boxYesNo.ShowWindowDialogAsync(Globals.MainWindow);
            if (boxResult == "Yes")
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