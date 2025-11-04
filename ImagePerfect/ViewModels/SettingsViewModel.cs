using ImagePerfect.Models;
using ImagePerfect.Repository;
using ImagePerfect.Repository.IRepository;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ImagePerfect.ViewModels
{
	public class SettingsViewModel : ViewModelBase
	{
        //FolderPageSize and ImagePageSize are needed for pagination as well.
        //image width max of 600 min of 300
        private int _maxImageWidth = 600;
        private int _folderPageSize = 20;
        private int _imagePageSize = 20;

        private string? _externalImageViewerExePath;
        private string? _fileExplorerExePath;

        private readonly MySqlDataSource _dataSource;
        private readonly IConfiguration _configuration;
        private readonly MainWindowViewModel _mainWindowViewModel;
        public SettingsViewModel(MySqlDataSource dataSource, IConfiguration config, MainWindowViewModel mainWindowViewModel) 
		{
            _dataSource = dataSource;
            _configuration = config;
            _mainWindowViewModel = mainWindowViewModel;
        }

        public string? ExternalImageViewerExePath
        {
            get => _externalImageViewerExePath;
            set => _externalImageViewerExePath = value;
        }
        public string? FileExplorerExePath
        {
            get => _fileExplorerExePath;
            set => _fileExplorerExePath = value;
        }
        public int ImagePageSize
        {
            get => _imagePageSize;
            set => this.RaiseAndSetIfChanged(ref _imagePageSize, value);
        }
        public int FolderPageSize
        {
            get => _folderPageSize;
            set => this.RaiseAndSetIfChanged(ref _folderPageSize, value);
        }
        public int MaxImageWidth
        {
            get => _maxImageWidth;
            set => this.RaiseAndSetIfChanged(ref _maxImageWidth, value);
        }

        public async Task UpdateExternalImageViewerExePath(string externalImageViewerExePath)
        {
            ExternalImageViewerExePath = externalImageViewerExePath;
            await UpdateSettings();
        }
        private async Task UpdateSettings()
        {
            await using UnitOfWork uow = await UnitOfWork.CreateAsync(_dataSource, _configuration);
            SettingsMethods settingsMethods = new SettingsMethods(uow);
            //update database
            Settings settings = new()
            {
                SettingsId = 1,
                MaxImageWidth = MaxImageWidth,
                FolderPageSize = FolderPageSize,
                ImagePageSize = ImagePageSize,
                ExternalImageViewerExePath = ExternalImageViewerExePath,
                FileExplorerExePath = FileExplorerExePath,
            };
            await settingsMethods.UpdateSettings(settings);
        }

        public async Task GetSettings(UnitOfWork uow)
        {
            SettingsMethods settingsMethods = new SettingsMethods(uow);
            Settings settings = await settingsMethods.GetSettings();
            MaxImageWidth = settings.MaxImageWidth;
            FolderPageSize = settings.FolderPageSize;
            ImagePageSize = settings.ImagePageSize;
            ExternalImageViewerExePath = settings.ExternalImageViewerExePath;
            FileExplorerExePath = settings.FileExplorerExePath;
        }

        public async Task PickImagePageSize(string size)
        {
            switch (size)
            {
                case "20":
                    ImagePageSize = 20;
                    break;
                case "40":
                    ImagePageSize = 40;
                    break;
                case "60":
                    ImagePageSize = 60;
                    break;
                case "80":
                    ImagePageSize = 80;
                    break;
                case "100":
                    ImagePageSize = 100;
                    break;
                case "125":
                    ImagePageSize = 125;
                    break;
                case "150":
                    ImagePageSize = 150;
                    break;
                case "175":
                    ImagePageSize = 175;
                    break;
                case "200":
                    ImagePageSize = 200;
                    break;
                case "300":
                    ImagePageSize = 300;
                    break;
                case "400":
                    ImagePageSize = 400;
                    break;
            }
            await UpdateSettings();
            _mainWindowViewModel.ExplorerVm.ResetPagination();
            await _mainWindowViewModel.ExplorerVm.RefreshFolders();
            await _mainWindowViewModel.ExplorerVm.RefreshImages(_mainWindowViewModel.ExplorerVm.CurrentDirectory);
        }
        public async Task PickFolderPageSize(string size)
        {
            switch (size)
            {
                case "20":
                    FolderPageSize = 20;
                    break;
                case "40":
                    FolderPageSize = 40;
                    break;
                case "60":
                    FolderPageSize = 60;
                    break;
                case "80":
                    FolderPageSize = 80;
                    break;
                case "100":
                    FolderPageSize = 100;
                    break;
                case "125":
                    FolderPageSize = 125;
                    break;
                case "150":
                    FolderPageSize = 150;
                    break;
                case "175":
                    FolderPageSize = 175;
                    break;
                case "200":
                    FolderPageSize = 200;
                    break;
                case "300":
                    FolderPageSize = 300;
                    break;
                case "400":
                    FolderPageSize = 400;
                    break;
            }
            await UpdateSettings();
            _mainWindowViewModel.ExplorerVm.ResetPagination();
            await _mainWindowViewModel.ExplorerVm.RefreshFolders();
            await _mainWindowViewModel.ExplorerVm.RefreshImages(_mainWindowViewModel.ExplorerVm.CurrentDirectory);
        }
        public async Task SelectImageWidth(decimal size)
        {
            MaxImageWidth = (int)size;
            await UpdateSettings();
        }
        public async Task PickImageWidth(string size)
        {
            switch (size)
            {
                case "Small":
                    MaxImageWidth = 400;
                    break;
                case "Medium":
                    MaxImageWidth = 500;
                    break;
                case "Large":
                    MaxImageWidth = 550;
                    break;
                case "XLarge":
                    MaxImageWidth = 600;
                    break;
            }
            await UpdateSettings();
        }
	}
}