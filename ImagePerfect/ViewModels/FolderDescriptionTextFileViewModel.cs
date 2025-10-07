using Avalonia.Controls;
using ImagePerfect.Helpers;
using ImagePerfect.Models;
using ImagePerfect.ObjectMappers;
using ImagePerfect.Repository.IRepository;
using MsBox.Avalonia;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Models;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ImagePerfect.ViewModels
{
	public class FolderDescriptionTextFileViewModel : ViewModelBase
	{
        private readonly IUnitOfWork _unitOfWork;
        private readonly FolderMethods _folderMethods;
        private readonly MainWindowViewModel _mainWindowViewModel;

        public FolderDescriptionTextFileViewModel(IUnitOfWork unitOfWork, MainWindowViewModel mainWindowViewModel)
        {
            _unitOfWork = unitOfWork;
            _mainWindowViewModel = mainWindowViewModel;
            _folderMethods = new FolderMethods(_unitOfWork);
        }

        public async Task CopyFolderDescriptionToContainingFolder(FolderViewModel folderVm)
        {
            if (PathHelper.RemoveOneFolderFromPath(folderVm.FolderPath) == _mainWindowViewModel.InitializeVm.RootFolderLocation)
            {
                await MessageBoxManager.GetMessageBoxCustom(
                    new MessageBoxCustomParams
                    {
                        ButtonDefinitions = new List<ButtonDefinition>
                        {
                            new ButtonDefinition { Name = "Ok", },
                        },
                        ContentTitle = "Copy Description",
                        ContentMessage = $"Cannot copy description from root folder.",
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        SizeToContent = SizeToContent.WidthAndHeight,  // <-- lets it grow with content
                        MinWidth = 500  // optional, so it doesn’t wrap too soon
                    }
                ).ShowWindowDialogAsync(Globals.MainWindow);
                return;
            }
            if (String.IsNullOrEmpty(folderVm.FolderDescription))
            {
                await MessageBoxManager.GetMessageBoxCustom(
                   new MessageBoxCustomParams
                   {
                       ButtonDefinitions = new List<ButtonDefinition>
                       {
                            new ButtonDefinition { Name = "Ok", },
                       },
                       ContentTitle = "Copy Description",
                       ContentMessage = $"The folder must have a description to copy.",
                       WindowStartupLocation = WindowStartupLocation.CenterOwner,
                       SizeToContent = SizeToContent.WidthAndHeight,  // <-- lets it grow with content
                       MinWidth = 500  // optional, so it doesn’t wrap too soon
                   }
               ).ShowWindowDialogAsync(Globals.MainWindow);
                return;
            }
            Folder containingFolder = await _folderMethods.GetFolderAtDirectory(PathHelper.RemoveOneFolderFromPath(folderVm.FolderPath));
            if (!string.IsNullOrEmpty(containingFolder.FolderDescription))
            {
                var boxYesNo = MessageBoxManager.GetMessageBoxCustom(
                    new MessageBoxCustomParams
                    {
                        ButtonDefinitions = new List<ButtonDefinition>
                            {
                                new ButtonDefinition { Name = "Yes", },
                                new ButtonDefinition { Name = "No", },
                            },
                        ContentTitle = "Copy Description",
                        ContentMessage = $"Containing folder already has a description. Do you want to overwrite it?",
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        SizeToContent = SizeToContent.WidthAndHeight,  // <-- lets it grow with content
                        MinWidth = 500  // optional, so it doesn’t wrap too soon
                    }
                );
                var boxResult = await boxYesNo.ShowWindowDialogAsync(Globals.MainWindow);
                if (boxResult == "No")
                {
                    return;
                }
            }
            containingFolder.FolderDescription = folderVm.FolderDescription;
            //update db
            await _folderMethods.UpdateFolder(containingFolder);

        }
        public async Task GetFolderDescriptionFromTextFileOnCurrentPage(ItemsControl foldersItemsControl)
        {
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
                        await _folderMethods.UpdateFolder(FolderMapper.GetFolderFromVm(folder));
                    }
                    catch (Exception ex) 
                    {
                        string failedMsg = $"Failed to read file {filePathToUse}. Reason: {ex.Message}";
                        errors.Add(failedMsg);
                    }
                }
            }

            if (errors.Any())
            {
                string errorMsg = string.Empty;
                errorMsg += "Some text files failed to be read: \n\n" + string.Join("\n\n", errors);

                await MessageBoxManager.GetMessageBoxCustom(
                    new MessageBoxCustomParams
                    {
                        ButtonDefinitions = new List<ButtonDefinition>
                        {
                            new ButtonDefinition { Name = "Ok", },
                        },
                        ContentTitle = "Add text file to folder description",
                        CanResize = true,
                        ContentMessage = errorMsg,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        SizeToContent = SizeToContent.WidthAndHeight,
                        MinWidth = 500,
                        MinHeight = 600,

                    }
                ).ShowWindowDialogAsync(Globals.MainWindow);
            }
        }
    }
}