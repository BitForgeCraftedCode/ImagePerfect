using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using ImagePerfect.Helpers;
using ImagePerfect.Models;
using ImagePerfect.Repository.IRepository;
using MsBox.Avalonia.Enums;
using MsBox.Avalonia;
using ReactiveUI;
using System.Linq;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Models;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using ImagePerfect.Repository;

namespace ImagePerfect.ViewModels
{
	public class ImportImagesViewModel : ViewModelBase
	{
        private readonly MySqlDataSource _dataSource;
        private readonly IConfiguration _configuration;
        private readonly MainWindowViewModel _mainWindowViewModel;
        public ImportImagesViewModel(MySqlDataSource dataSource, IConfiguration config, MainWindowViewModel mainWindowViewModel) 
		{
            _dataSource = dataSource;
            _configuration = config;
            _mainWindowViewModel = mainWindowViewModel;
        }

        public async Task ImportImages(FolderViewModel imageFolder, bool bulkScan)
        {
            await using UnitOfWork uow = await UnitOfWork.CreateAsync(_dataSource, _configuration);
            ImageCsvMethods imageCsvMethods = new ImageCsvMethods(uow);

            string newPath = string.Empty;
            string imageFolderPath = imageFolder.FolderPath;
            int imageFolderId = imageFolder.FolderId;
            if(bulkScan == false)
            {
                _mainWindowViewModel.ShowLoading = true;
                ImageCsvMethods.CopyMasterCsv(imageFolder);
            }
               
            //build csv
            bool csvIsSet = await ImageCsvMethods.BuildImageCsv(imageFolderPath, imageFolderId);
            //write csv to database and load folders and images at the location again
            //load again so the import button will go away
            if (csvIsSet)
            {
                await imageCsvMethods.AddImageCsv(imageFolderId);
                if(bulkScan == false)
                {
                    //remove one folder from path
                    newPath = PathHelper.RemoveOneFolderFromPath(imageFolderPath);
                    //refresh UI
                    if (_mainWindowViewModel.ExplorerVm.currentFilter == ExplorerViewModel.Filters.AllFoldersWithMetadataNotScanned || _mainWindowViewModel.ExplorerVm.currentFilter == ExplorerViewModel.Filters.AllFoldersWithNoImportedImages)
                    {
                        //have to call hard refresh for these two cases as they will not be returned from the query to update props
                        await _mainWindowViewModel.ExplorerVm.RefreshFolders("", uow);
                    }
                    else
                    {
                        await _mainWindowViewModel.ExplorerVm.RefreshFolderProps(newPath, imageFolder, uow);
                    }
                }
            }
            if(bulkScan == false)
            {
                _mainWindowViewModel.ShowLoading = false;
                ImageCsvMethods.DeleteCsvCopy(imageFolderId);
            }
        }

        public async Task ImportAllFoldersOnCurrentPage(ItemsControl foldersItemsControl)
        {
            
            _mainWindowViewModel.ShowLoading = true;
            List<FolderViewModel> allFolders = foldersItemsControl.Items.OfType<FolderViewModel>().ToList();
            foreach (FolderViewModel folder in allFolders)
            {
                if (folder.HasFiles == true && folder.AreImagesImported == false)
                {
                    //make the csv file
                    ImageCsvMethods.CopyMasterCsv(folder);
                }
            }
            await Parallel.ForEachAsync(allFolders, new ParallelOptions { MaxDegreeOfParallelism = 4 }, async (folder, cancellationToken) =>
            {
                if (folder.HasFiles == true && folder.AreImagesImported == false) 
                {
                    try
                    {
                        // Each folder uses its own CSV
                        await ImportImages(folder, bulkScan: true);
                    }
                    finally
                    {
                        // Delete folder-specific CSV after import
                        ImageCsvMethods.DeleteCsvCopy(folder.FolderId);
                    }
                }
            });
               
            _mainWindowViewModel.ExplorerVm.ResetPagination();
            await _mainWindowViewModel.ExplorerVm.RefreshFolders();
            _mainWindowViewModel.ShowLoading = false;
            
        }
    }
}