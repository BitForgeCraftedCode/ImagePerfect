using Avalonia.Controls;
using ImagePerfect.Helpers;
using ImagePerfect.Models;
using ImagePerfect.ObjectMappers;
using ImagePerfect.Repository.IRepository;
using MsBox.Avalonia;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Enums;
using MsBox.Avalonia.Models;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace ImagePerfect.ViewModels
{
	public class PickRootFolderViewModel : ViewModelBase
	{
        private readonly IUnitOfWork _unitOfWork;
        private readonly FolderCsvMethods _folderCsvMethods;
        private readonly FolderMethods _folderMethods;
        private readonly MainWindowViewModel _mainWindowViewModel;
           
        public PickRootFolderViewModel(IUnitOfWork unitOfWork, MainWindowViewModel mainWindowViewModel) 
		{
            _unitOfWork = unitOfWork;
            _folderMethods = new FolderMethods(_unitOfWork);
            _folderCsvMethods = new FolderCsvMethods(_unitOfWork);
            _SelectFolderInteraction = new Interaction<string, List<string>?>();
            SelectLibraryFolderCommand = ReactiveCommand.CreateFromTask(SelectLibraryFolder);
            _mainWindowViewModel = mainWindowViewModel;
        }

        private List<string>? _RootFolderPath;
        
        private readonly Interaction<string, List<string>?> _SelectFolderInteraction;

        public Interaction<string, List<string>?> SelectFolderInteraction { get { return _SelectFolderInteraction; } }

        public ReactiveCommand<Unit, Unit> SelectLibraryFolderCommand { get; }

        private async Task SelectLibraryFolder()
        {
            Folder? rootFolder = await _folderMethods.GetRootFolder();
            if (rootFolder != null)
            {
                await MessageBoxManager.GetMessageBoxCustom(
                    new MessageBoxCustomParams
                    {
                        ButtonDefinitions = new List<ButtonDefinition>
                        {
                            new ButtonDefinition { Name = "Ok", },
                        },
                        ContentTitle = "Add Library",
                        ContentMessage = $"You already have a root library folder. You have to delete your library to add different one.",
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        SizeToContent = SizeToContent.WidthAndHeight,  // <-- lets it grow with content
                        MinWidth = 500  // optional, so it doesn’t wrap too soon
                    }
                ).ShowWindowDialogAsync(Globals.MainWindow);
                return;
            }

            _RootFolderPath = await _SelectFolderInteraction.Handle("Select Root Library Folder");
            //list will be empty if Cancel is pressed exit method
            if (_RootFolderPath.Count == 0) 
            {
                return;
            }
            
            _mainWindowViewModel.ShowLoading = true;
            //build csv
            bool csvIsSet = await FolderCsvMethods.AddNewFoldersCsv(_RootFolderPath, true);
            //write csv to database
            if (csvIsSet) 
            {
                await _folderCsvMethods.AddFolderCsv();

                rootFolder = await _folderMethods.GetRootFolder();
                if (rootFolder != null)
                {
                    FolderViewModel rootFolderVm = await FolderMapper.GetFolderVm(rootFolder);
                    _mainWindowViewModel.LibraryFolders.Add(rootFolderVm);

                    _mainWindowViewModel.InitializeVm.RootFolderLocation = PathHelper.RemoveOneFolderFromPath(rootFolder.FolderPath);
                    _mainWindowViewModel.ExplorerVm.CurrentDirectory = _mainWindowViewModel.InitializeVm.RootFolderLocation;
                    //initially set SavedDirectory to CurrentDirectory so method wont fail if btn clicked before saving a directory
                    _mainWindowViewModel.SavedDirectory = _mainWindowViewModel.ExplorerVm.CurrentDirectory;
                }
            }
            _mainWindowViewModel.ShowLoading = false;
        }
    }
}