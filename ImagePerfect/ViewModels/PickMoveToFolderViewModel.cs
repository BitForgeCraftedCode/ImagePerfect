using ImagePerfect.Helpers;
using ImagePerfect.Models;
using ImagePerfect.Repository;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using ReactiveUI;
using ReactiveUI.Primitives;
using Serilog;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Image = ImagePerfect.Models.Image;

namespace ImagePerfect.ViewModels
{
	public class PickMoveToFolderViewModel : ViewModelBase
	{
        private readonly MySqlDataSource _dataSource;
        private readonly IConfiguration _configuration;
        private readonly MainWindowViewModel _mainWindowViewModel;
        public PickMoveToFolderViewModel(MySqlDataSource dataSource, IConfiguration config, MainWindowViewModel mainWindowViewModel) 
		{
            _dataSource = dataSource;
            _configuration = config;
            _mainWindowViewModel = mainWindowViewModel;

            _SelectMoveToFolderInteration = new Interaction<string, List<string>?>();
			SelectMoveToFolderCommand = ReactiveCommand.Create(async (IList? selectedFolders) => await SelectMoveToFolder(selectedFolders));
		}

		private List<string>? _MoveToFolderPath;

		private Interaction<string, List<string>?> _SelectMoveToFolderInteration;

		public Interaction<string, List<string>?> SelectMoveToFolderInteration { get { return _SelectMoveToFolderInteration; } }

		public ReactiveCommand<IList?, Task> SelectMoveToFolderCommand { get; }

		private async Task SelectMoveToFolder(IList? selectedFolders)
		{
            if (selectedFolders is null || selectedFolders.Count == 0)
            {
                await MessageBoxHelper.ShowAsync(
                    "Move Folders",
                    $"You need to select folders to move."
                );
                return;
            }
            List<FolderViewModel> foldersToMove = selectedFolders.OfType<FolderViewModel>().ToList();
            if (foldersToMove.Count == 0)
            {
                await MessageBoxHelper.ShowAsync("Move Folders", "The selection does not contain any folders to move.");
                return;
            }
            if (foldersToMove.Count > 20)
            {
                await MessageBoxHelper.ShowAsync("Move Folders", "You cannot move more that 20 folders at once.");
                return;
            }

            _MoveToFolderPath = await _SelectMoveToFolderInteration.Handle(_mainWindowViewModel.ExplorerVm.CurrentDirectory);
            //list will be empty if Cancel is pressed exit method
            if (_MoveToFolderPath == null || _MoveToFolderPath.Count == 0)
            {
                return;
            }
            //add check to make sure user is picking folders within the root libary directory
            string newFolderPath = PathHelper.FormatPathFromFolderPicker(_MoveToFolderPath[0]);

            await _mainWindowViewModel.MoveFoldersVm.MoveFolders(foldersToMove, newFolderPath);
        }
    }
}
