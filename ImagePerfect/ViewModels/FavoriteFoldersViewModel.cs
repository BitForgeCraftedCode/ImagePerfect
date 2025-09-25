using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ImagePerfect.Models;
using ImagePerfect.Repository.IRepository;
using MsBox.Avalonia.Enums;
using MsBox.Avalonia;
using ReactiveUI;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Models;
using Avalonia.Controls;

namespace ImagePerfect.ViewModels
{
	public class FavoriteFoldersViewModel : ViewModelBase
	{
        private readonly IUnitOfWork _unitOfWork;
        private readonly FolderMethods _folderMethods;

        public FavoriteFoldersViewModel(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
            _folderMethods = new FolderMethods(_unitOfWork);
        }

        public async Task SaveFolderAsFavorite(FolderViewModel folderVm)
        {
            await _folderMethods.SaveFolderToFavorites(folderVm.FolderId);
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
                await _folderMethods.RemoveAllFavoriteFolders();
            }
            else
            {
                return;
            }
        }
    }
}