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
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace ImagePerfect.ViewModels
{
	public class CreateNewFolderViewModel : ViewModelBase
	{
        private string _newFolderName = string.Empty;
        private bool _isNewFolderEnabled;

        private readonly MySqlDataSource _dataSource;
        private readonly IConfiguration _configuration;
        private readonly MainWindowViewModel _mainWindowViewModel;
        public CreateNewFolderViewModel(MySqlDataSource dataSource, IConfiguration config, MainWindowViewModel mainWindowViewModel) 
        {
            _dataSource = dataSource;
            _configuration = config;
            _mainWindowViewModel = mainWindowViewModel;
        }

        public string NewFolderName
        {
            get => _newFolderName;
            set
            {
                this.RaiseAndSetIfChanged(ref _newFolderName, value);
                if (value == "" || _mainWindowViewModel.ExplorerVm.CurrentDirectory == _mainWindowViewModel.InitializeVm.RootFolderLocation || _mainWindowViewModel.ExplorerVm.currentFilter != ExplorerViewModel.Filters.None)
                {
                    IsNewFolderEnabled = false;
                }
                else
                {
                    IsNewFolderEnabled = true;
                }
            }
        }

        public bool IsNewFolderEnabled
        {
            get => _isNewFolderEnabled;
            set => this.RaiseAndSetIfChanged(ref _isNewFolderEnabled, value);
        }

        public async Task CreateNewFolder()
		{
            //first check if directory exists
            string newFolderPath = PathHelper.GetNewFolderPath(_mainWindowViewModel.ExplorerVm.CurrentDirectory, NewFolderName);
            if (Directory.Exists(newFolderPath))
            {
                await MessageBoxManager.GetMessageBoxCustom(
                    new MessageBoxCustomParams
                    {
                        ButtonDefinitions = new List<ButtonDefinition>
                        {
                            new ButtonDefinition { Name = "Ok", },
                        },
                        ContentTitle = "New Folder",
                        ContentMessage = $"A folder with this name already exists.",
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        SizeToContent = SizeToContent.WidthAndHeight,  // <-- lets it grow with content
                        MinWidth = 500  // optional, so it doesn’t wrap too soon
                    }
                ).ShowWindowDialogAsync(Globals.MainWindow);
                return;
            }
            await using UnitOfWork uow = await UnitOfWork.CreateAsync(_dataSource, _configuration);
            FolderMethods folderMethods = new FolderMethods(uow);
            //add dir to database -- also need to update parent folders HasChildren bool value
            Folder newFolder = new Folder
            {
                FolderName = NewFolderName,
                FolderPath = newFolderPath,
                HasChildren = false,
                CoverImagePath = "",
                FolderDescription = "",
                FolderRating = 0,
                HasFiles = false,
                IsRoot = false,
                FolderContentMetaDataScanned = false,
                AreImagesImported = false,
            };
            bool success = await folderMethods.CreateNewFolder(newFolder);

            //create on disk
            if (success)
            {
                try
                {
                    Directory.CreateDirectory(newFolderPath);
                    //refresh UI
                    _mainWindowViewModel.ExplorerVm.currentFilter = ExplorerViewModel.Filters.None;
                    await _mainWindowViewModel.ExplorerVm.RefreshFolders("", uow);
                }
                catch (Exception e)
                {
                    await MessageBoxManager.GetMessageBoxCustom(
                        new MessageBoxCustomParams
                        {
                            ButtonDefinitions = new List<ButtonDefinition>
                            {
                                new ButtonDefinition { Name = "Ok", },
                            },
                            ContentTitle = "New Folder",
                            ContentMessage = $"Error {e}.",
                            WindowStartupLocation = WindowStartupLocation.CenterOwner,
                            SizeToContent = SizeToContent.WidthAndHeight,  // <-- lets it grow with content
                            MinWidth = 500  // optional, so it doesn’t wrap too soon
                        }
                    ).ShowWindowDialogAsync(Globals.MainWindow);
                    return;
                }
            }
        }
	}
}