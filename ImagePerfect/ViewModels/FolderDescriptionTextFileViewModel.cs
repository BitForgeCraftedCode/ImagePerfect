using Avalonia.Controls;
using ImagePerfect.Helpers;
using ImagePerfect.Models;
using ImagePerfect.ObjectMappers;
using ImagePerfect.Repository;
using Microsoft.Extensions.Configuration;
using MySqlConnector;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImagePerfect.ViewModels
{
	public class FolderDescriptionTextFileViewModel : ViewModelBase
	{
        private readonly MySqlDataSource _dataSource;
        private readonly IConfiguration _configuration;
        private readonly MainWindowViewModel _mainWindowViewModel;

        public FolderDescriptionTextFileViewModel(MySqlDataSource dataSource, IConfiguration config, MainWindowViewModel mainWindowViewModel)
        {
            _dataSource = dataSource;
            _configuration = config;
            _mainWindowViewModel = mainWindowViewModel;
        }

        public async Task CopyFolderDescriptionToContainingFolder(FolderViewModel folderVm)
        {
            if (PathHelper.RemoveOneFolderFromPath(folderVm.FolderPath) == _mainWindowViewModel.InitializeVm.RootFolderLocation)
            {
                await MessageBoxHelper.ShowAsync(
                    "Copy Description",
                    $"Cannot copy description from root folder."
                );
                return;
            }
            if (String.IsNullOrEmpty(folderVm.FolderDescription))
            {
                await MessageBoxHelper.ShowAsync(
                    "Copy Description",
                    $"The folder must have a description to copy."
                );
                return;
            }
            await using UnitOfWork uow = await UnitOfWork.CreateAsync(_dataSource, _configuration);
            FolderMethods folderMethods = new FolderMethods(uow);
            Folder containingFolder = await folderMethods.GetFolderAtDirectory(PathHelper.RemoveOneFolderFromPath(folderVm.FolderPath));
            if (!string.IsNullOrEmpty(containingFolder.FolderDescription))
            {
                bool boxResult = await MessageBoxHelper.ShowYesNoAsync(
                    "Copy Description",
                    $"Containing folder already has a description. Do you want to overwrite it?"
                );
                if (!boxResult)
                {
                    return;
                }
            }
            containingFolder.FolderDescription = folderVm.FolderDescription;
            //update db
            await folderMethods.UpdateFolder(containingFolder);

        }
        public async Task GetFolderDescriptionFromTextFileOnCurrentPage(ItemsControl foldersItemsControl)
        {
            await using UnitOfWork uow = await UnitOfWork.CreateAsync(_dataSource, _configuration);
            FolderMethods folderMethods = new FolderMethods(uow);

            List<FolderViewModel> allFolders = foldersItemsControl.Items.OfType<FolderViewModel>().ToList();
            List<string> errors = new List<string>();
            foreach (FolderViewModel folder in allFolders) 
            {
                if (folder.HasFiles == true && folder.AreImagesImported == true)
                {
                    //get txt file if there
                    //should be 0 - 2 text files but maybe more
                    //the app will create a backup of FolderDescription and call the txt file folderDescription.txt
                    //want to account for user's initial lib having an initial text file in there as well
                    //that initial file could be named anything

                    string[] txtFiles = Directory.GetFiles(folder.FolderPath, "*.txt");
                    //skip folder if no text files
                    if(txtFiles.Length == 0)
                        continue;

                    //grab the correct text file if they exists
                    //folderDescription.txt takes precedence
                    //fallback use any other file. 
                    string filePathToUse = txtFiles.First();
                    foreach (string filePath in txtFiles) 
                    { 
                        string fileName = Path.GetFileName(filePath).Trim();
                        if(string.Equals(fileName, "folderDescription.txt", StringComparison.OrdinalIgnoreCase))
                        {
                            filePathToUse = filePath;
                            break;
                        }
                    }

                    if (string.IsNullOrEmpty(filePathToUse) || !File.Exists(filePathToUse))
                        continue;

                    //parse text file line by line
                    try
                    {
                        string fileContent = await File.ReadAllTextAsync(filePathToUse);
                        // Normalize line endings (Windows-friendly)
                        fileContent = fileContent.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", Environment.NewLine);
                        // Optional: insert a line break after sentences for readability
                        // (only if there are no existing newlines between sentences)
                        //fileContent = Regex.Replace(fileContent,@"(?<=[.!?])(?<!\b(e\.g|i\.e|U\.S|Mr|Mrs|Dr)\.)(\s+)(?=[A-Z])",Environment.NewLine);

                        // Trim excess spaces/newlines
                        fileContent = fileContent.Trim();

                        // Enforce DB column limit
                        if (fileContent.Length > 3000)
                            fileContent = fileContent.Substring(0, 3000);

                        folder.FolderDescription = fileContent;
                        //update db
                        await folderMethods.UpdateFolder(FolderMapper.GetFolderFromVm(folder));
                    }
                    catch (Exception ex) 
                    {
                        string failedMsg = $"Failed to read file {filePathToUse}. Reason: {ex.Message}";
                        errors.Add(failedMsg);
                    }
                }
            }

            //add folder description to parent folder as well
            if (!errors.Any() && _mainWindowViewModel.CopyFolderTextToParentFolder == true)
            {
                Folder containingFolder = await folderMethods.GetFolderAtDirectory(PathHelper.RemoveOneFolderFromPath(allFolders[0].FolderPath));
                if (string.IsNullOrEmpty(containingFolder.FolderDescription))
                {
                    containingFolder.FolderDescription = allFolders[0].FolderDescription;
                    await folderMethods.UpdateFolder(containingFolder);
                }
            }

            if (errors.Any())
            {
                string errorMsg = string.Empty;
                errorMsg += "Some text files failed to be read: \n\n" + string.Join("\n\n", errors);

                await MessageBoxHelper.ShowAsync(
                    "Add text file to folder description",
                    errorMsg,
                    true,
                    600
                );
            }
        }

        public async Task BackUpFolderDescriptionToTextFileOnCurrentPage(ItemsControl foldersItemsControl)
        {
            List<FolderViewModel> allFolders = foldersItemsControl.Items.OfType<FolderViewModel>().ToList();
            List<string> errors = new List<string>();
            foreach (FolderViewModel folder in allFolders) 
            {
                if (folder.HasFiles == true && folder.AreImagesImported == true && string.IsNullOrEmpty(folder.FolderDescription) == false)
                {

                    string pathToFile = Path.Combine(folder.FolderPath, "folderDescription.txt");
                    
                    try
                    {
                        string contentToWrite = folder.FolderDescription.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", Environment.NewLine);
                        await File.WriteAllTextAsync(pathToFile, contentToWrite, Encoding.UTF8);
                    }
                    catch (Exception ex) 
                    {
                        string failedMsg = $"Failed to write description for folder {folder.FolderName}. Reason: {ex.Message}";
                        errors.Add(failedMsg);
                    }
                   

                }
            }
            if (errors.Any())
            {
                string errorMsg = string.Empty;
                errorMsg += "Some text files failed to be written: \n\n" + string.Join("\n\n", errors);

                await MessageBoxHelper.ShowAsync(
                    "Back up folder description",
                    errorMsg,
                    true,
                    600
                );
            }
        }
    }
}