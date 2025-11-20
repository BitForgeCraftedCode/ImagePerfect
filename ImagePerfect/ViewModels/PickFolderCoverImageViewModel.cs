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
using System.Diagnostics;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace ImagePerfect.ViewModels
{
    public class PickFolderCoverImageViewModel : ViewModelBase
    {
        private readonly MySqlDataSource _dataSource;
        private readonly IConfiguration _configuration;
        private readonly MainWindowViewModel _mainWindowViewModel;
        public PickFolderCoverImageViewModel(MySqlDataSource dataSource, IConfiguration config, MainWindowViewModel mainWindowViewModel)
        {
            _dataSource = dataSource;
            _configuration = config;
            _mainWindowViewModel = mainWindowViewModel;
            _SelectCoverImageInteraction = new Interaction<string, List<string>?>();
            SelectCoverImageCommand = ReactiveCommand.CreateFromTask((FolderViewModel folderVm) => SelectCoverImage(folderVm));
        }

        private List<String>? _CoverImagePath;

        private readonly Interaction<string, List<string>?> _SelectCoverImageInteraction;

        public Interaction<string, List<string>?> SelectCoverImageInteraction { get { return _SelectCoverImageInteraction; } }

        public ReactiveCommand<FolderViewModel, Unit> SelectCoverImageCommand { get; }

        private async Task SelectCoverImage(FolderViewModel folderVm)
        {
            _CoverImagePath = await _SelectCoverImageInteraction.Handle(folderVm.FolderPath);
            //list will be empty if Cancel is pressed exit method
            if (_CoverImagePath.Count == 0) 
            { 
                return;
            }
            //add check to make sure user is picking cover image directly in the folder
            //the move operation is complex and best to ensure each cover image is an image from its own folder only
            string pathCheck = PathHelper.FormatPathFromFolderPicker(_CoverImagePath[0]);
            //pathCheck is now the selected image folder path.
            pathCheck = PathHelper.RemoveOneFolderFromPath(pathCheck);
            if (pathCheck != folderVm.FolderPath)
            {
                await MessageBoxManager.GetMessageBoxCustom(
                    new MessageBoxCustomParams
                    {
                        ButtonDefinitions = new List<ButtonDefinition>
                        {
                            new ButtonDefinition { Name = "Ok", },
                        },
                        ContentTitle = "Cover Image",
                        ContentMessage = $"You can only select a cover image that is within its own folder.",
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        SizeToContent = SizeToContent.WidthAndHeight,  // <-- lets it grow with content
                        MinWidth = 500  // optional, so it doesn’t wrap too soon
                    }
                ).ShowWindowDialogAsync(Globals.MainWindow);
                return;
            }
            await using UnitOfWork uow = await UnitOfWork.CreateAsync(_dataSource, _configuration);
            FolderMethods folderMethods = new FolderMethods(uow);
            bool success = await folderMethods.UpdateCoverImage(PathHelper.FormatPathFromFilePicker(_CoverImagePath[0]), folderVm.FolderId);
            //update lib folders to show the new cover !!
            if (success)
            {
                string foldersDirectoryPath = PathHelper.RemoveOneFolderFromPath(folderVm.FolderPath);
                await _mainWindowViewModel.ExplorerVm.RefreshFolderProps(foldersDirectoryPath, folderVm, uow);
            }
        }
    }
}