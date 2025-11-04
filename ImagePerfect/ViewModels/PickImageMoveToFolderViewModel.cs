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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace ImagePerfect.ViewModels
{
	public class PickImageMoveToFolderViewModel : ViewModelBase
	{
        private readonly MySqlDataSource _dataSource;
        private readonly IConfiguration _configuration;
        private readonly MainWindowViewModel _mainWindowViewModel;
        public PickImageMoveToFolderViewModel(MySqlDataSource dataSource, IConfiguration config, MainWindowViewModel mainWindowViewModel)
        {
            _dataSource = dataSource;
            _configuration = config;
            _mainWindowViewModel = mainWindowViewModel;

            _SelectMoveImagesToFolderInteration = new Interaction<string, List<string>?>();
            SelectMoveImagesToFolderCommand = ReactiveCommand.Create(async (IList? selectedImages)=>await SelectMoveImagesToFolder(selectedImages));
        }

        private List<string>? _MoveImagesToFolderPath;
        
        private Interaction<string, List<string>?> _SelectMoveImagesToFolderInteration;

        public Interaction<string, List<string>?> SelectMoveImagesToFolderInteration { get { return _SelectMoveImagesToFolderInteration; } }

        public ReactiveCommand<IList?, Task> SelectMoveImagesToFolderCommand { get; }

        private async Task SelectMoveImagesToFolder(IList? selectedImages)
        {
            if (selectedImages is null || selectedImages.Count == 0)
            {
                await MessageBoxManager.GetMessageBoxCustom(
                    new MessageBoxCustomParams
                    {
                        ButtonDefinitions = new List<ButtonDefinition>
                        {
                            new ButtonDefinition { Name = "Ok", },
                        },
                        ContentTitle = "Move Image",
                        ContentMessage = $"You need to select images to move.",
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        SizeToContent = SizeToContent.WidthAndHeight,  // <-- lets it grow with content
                        MinWidth = 500  // optional, so it doesn’t wrap too soon
                    }
                ).ShowWindowDialogAsync(Globals.MainWindow);
                return;
            }
            await using UnitOfWork uow = await UnitOfWork.CreateAsync(_dataSource, _configuration);
            FolderMethods folderMethods = new FolderMethods(uow);
            Folder? rootFolder = await folderMethods.GetRootFolder();
            _MoveImagesToFolderPath = await _SelectMoveImagesToFolderInteration.Handle(_mainWindowViewModel.ExplorerVm.CurrentDirectory);
            //list will be empty if Cancel is pressed exit method
            if (_MoveImagesToFolderPath.Count == 0) 
            { 
                return;
            }
            //add check to make sure user is picking folders within the root libary directory
            string pathCheck = PathHelper.FormatPathFromFolderPicker(_MoveImagesToFolderPath[0]);
            if (!pathCheck.Contains(rootFolder.FolderPath))
            {
                await MessageBoxManager.GetMessageBoxCustom(
                    new MessageBoxCustomParams
                    {
                        ButtonDefinitions = new List<ButtonDefinition>
                        {
                            new ButtonDefinition { Name = "Ok", },
                        },
                        ContentTitle = "Move Images",
                        ContentMessage = $"You can only move images to folders that are within your root library.",
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        SizeToContent = SizeToContent.WidthAndHeight,  // <-- lets it grow with content
                        MinWidth = 500  // optional, so it doesn’t wrap too soon
                    }
                ).ShowWindowDialogAsync(Globals.MainWindow);
                return;
            }
            //set the move to directory
            _mainWindowViewModel.SelectedImagesNewDirectory = PathHelper.FormatPathFromFolderPicker(_MoveImagesToFolderPath[0]);
            await _mainWindowViewModel.MoveImages.MoveSelectedImagesToNewFolder(selectedImages);
        }
    }
}