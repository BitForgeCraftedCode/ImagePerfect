using Avalonia.Controls;
using DynamicData;
using ImagePerfect.Helpers;
using ImagePerfect.Models;
using ImagePerfect.Repository;
using ImagePerfect.Repository.IRepository;
using Microsoft.Extensions.Configuration;
using MsBox.Avalonia;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Models;
using MySqlConnector;
using ReactiveUI;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace ImagePerfect.ViewModels
{
	public class PickFoldersToExtractZipsViewModel : ViewModelBase
    {
        private readonly MySqlDataSource _dataSource;
        private readonly IConfiguration _configuration;
        private readonly MainWindowViewModel _mainWindowViewModel;
        public PickFoldersToExtractZipsViewModel(MySqlDataSource dataSource, IConfiguration config, MainWindowViewModel mainWindowViewModel) 
        {
            _dataSource = dataSource;
            _configuration = config;
            _mainWindowViewModel = mainWindowViewModel;
            _SelectZipFoldersInteraction = new Interaction<string, List<string>?>();
            SelectZipFoldersCommand = ReactiveCommand.CreateFromTask(SelectZipFolders);
        }
		private List<string>? _ZipFolders;

        private readonly Interaction<string, List<string>?> _SelectZipFoldersInteraction;

        public Interaction<string, List<string>?> SelectZipFoldersInteraction { get { return _SelectZipFoldersInteraction; } }

        public ReactiveCommand<Unit, Unit> SelectZipFoldersCommand { get; }

        private async Task SelectZipFolders()
        {
            await using UnitOfWork uow = await UnitOfWork.CreateAsync(_dataSource, _configuration);
            FolderMethods folderMethods = new FolderMethods(uow);
            Folder? rootFolder = await folderMethods.GetRootFolder();
            if (rootFolder == null)
            {
                await MessageBoxManager.GetMessageBoxCustom(
                    new MessageBoxCustomParams
                    {
                        ButtonDefinitions = new List<ButtonDefinition>
                        {
                            new ButtonDefinition { Name = "Ok", },
                        },
                        ContentTitle = "Extract Zips In Folders",
                        ContentMessage = $"You need to add a root library folder first.",
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        SizeToContent = SizeToContent.WidthAndHeight,  // <-- lets it grow with content
                        MinWidth = 500  // optional, so it doesn’t wrap too soon
                    }
                ).ShowWindowDialogAsync(Globals.MainWindow);
                return;
            }
            _ZipFolders = await _SelectZipFoldersInteraction.Handle(rootFolder.FolderPath);
            //list will be empty if Cancel is pressed exit method
            if (_ZipFolders.Count == 0) 
            { 
                return;
            }
            _mainWindowViewModel.ShowLoading = true;
            ConcurrentBag<string> extractionErrors = new ConcurrentBag<string>();
            ConcurrentBag<string> successfullyExtracted = new ConcurrentBag<string>();
            List<string> moveErrors = new List<string>();
            //will move zips to ImagePrefectTRASH
            string globalTrashFolderPath = PathHelper.GetTrashFolderPath(rootFolder.FolderPath);
            //create ImagePerfectTRASH if it doesnt exist
            if (!Directory.Exists(globalTrashFolderPath))
            {
                Directory.CreateDirectory(globalTrashFolderPath);
            }
            // Phase 1: Extract zips in parallel
            try
            {
                List<string> allZipFiles = _ZipFolders.SelectMany(folder => 
                    Directory.EnumerateFiles(PathHelper.FormatPathFromFolderPicker(folder), "*.zip", SearchOption.AllDirectories)).ToList();

                if (!allZipFiles.Any())
                {
                    await MessageBoxManager.GetMessageBoxCustom(
                        new MessageBoxCustomParams
                        {
                            ButtonDefinitions = new List<ButtonDefinition>
                            {
                            new ButtonDefinition { Name = "Ok", },
                            },
                            ContentTitle = "Extract Zips In Folders",
                            ContentMessage = $"No zip files were found in the selected folders.",
                            WindowStartupLocation = WindowStartupLocation.CenterOwner,
                            SizeToContent = SizeToContent.WidthAndHeight,
                            MinWidth = 500
                        }
                    ).ShowWindowDialogAsync(Globals.MainWindow);

                    return;
                }
                
                // Extract zips in parallel
                await Parallel.ForEachAsync(allZipFiles, new ParallelOptions { MaxDegreeOfParallelism = 4 },
                async (zipFile, ct) =>
                {
                    try
                    {
                        string parentDir = Path.GetDirectoryName(zipFile)!;
                        string baseName = Path.GetFileNameWithoutExtension(zipFile);
                        string targetDir = Path.Combine(parentDir, baseName);

                        // Ensure unique folder name if target exists
                        int counter = 1;
                        while (Directory.Exists(targetDir))
                        {
                            targetDir = Path.Combine(parentDir, $"{baseName}_{counter}");
                            counter++;
                        }

                        Directory.CreateDirectory(targetDir);

                        // Extract
                        await Task.Run(() =>
                        {
                            ZipFile.ExtractToDirectory(zipFile, targetDir);
                        }, ct);

                        //add to list so we can move those zips to trash later
                        successfullyExtracted.Add(zipFile);
                    }
                    catch(Exception ex) 
                    {
                        string failedMsg = $"{zipFile} failed to extract. Reason: {ex.Message}";
                        extractionErrors.Add(failedMsg);
                    }
                });

                //Phase 2: Move to trash only if extraction worked
                
                //group strings based on a part of the string -- group based on Parent dir so all zip paths will be grouped together by their folders
                IEnumerable<IGrouping<string, string>> zipsGroupedByParentFolder = successfullyExtracted.GroupBy(zip => Path.GetDirectoryName(zip)!);
                foreach (IGrouping<string, string> groupParentFolder in zipsGroupedByParentFolder)
                {
                    try
                    {
                        //create a TRASH folder in each groupParentFolder
                        string parentDir = groupParentFolder.Key;
                        string trashDir = Path.Combine(parentDir, Path.GetFileName(parentDir) + "TRASH");
                        Directory.CreateDirectory(trashDir);

                        //move each zip into the TRASH folder
                        foreach (string zipFile in groupParentFolder)
                        {
                            try
                            {
                                string destPath = Path.Combine(trashDir, Path.GetFileName(zipFile));
                                File.Move(zipFile, destPath);
                            }
                            catch (Exception ex)
                            {
                                moveErrors.Add($"{zipFile} could not be moved. Reason: {ex.Message}");
                            }

                        }

                        //move the TRASH folder to ImagePerfectTrash
                        try
                        {
                            string zipFolderIPTrashPath = PathHelper.GetZipFolderTrashPath(Path.GetFileName(trashDir), globalTrashFolderPath);
                            Directory.Move(trashDir, zipFolderIPTrashPath);
                        }
                        catch (Exception ex)
                        {
                            moveErrors.Add($"{trashDir} could not be moved to global trash. Reason: {ex.Message}");
                        }

                    }
                    catch (Exception ex)
                    {
                        moveErrors.Add($"Unexpected error while preparing trash for {groupParentFolder.Key}. Reason: {ex.Message}");
                    }

                }

            }
            finally
            {
                if (extractionErrors.Any() || moveErrors.Any())
                {
                    string errorMsg = "";
                    if (extractionErrors.Any())
                    {
                        errorMsg += "Some zips failed to extract:\n\n" +
                                    string.Join("\n\n", extractionErrors) + "\n\n";
                    }
                    if (moveErrors.Any())
                    {
                        errorMsg += "Some zips could not be moved to ImagePerfectTRASH:\n\n" +
                                    string.Join("\n\n", moveErrors);
                    }
                    await MessageBoxManager.GetMessageBoxCustom(
                        new MessageBoxCustomParams
                        {
                            ButtonDefinitions = new List<ButtonDefinition>
                            {
                                new ButtonDefinition { Name = "Ok", },
                            },
                            ContentTitle = "Extract Zips In Folders",
                            CanResize = true,
                            ContentMessage = errorMsg,
                            WindowStartupLocation = WindowStartupLocation.CenterOwner,
                            SizeToContent = SizeToContent.WidthAndHeight,
                            MinWidth = 500,
                            MinHeight = 600,

                        }
                    ).ShowWindowDialogAsync(Globals.MainWindow);
                }
                else
                {
                    await MessageBoxManager.GetMessageBoxCustom(
                        new MessageBoxCustomParams
                        {
                            ButtonDefinitions = new List<ButtonDefinition>
                            {
                                new ButtonDefinition { Name = "Ok", },
                            },
                            ContentTitle = "Extract Zips In Folders",
                            CanResize = true,
                            ContentMessage = $"All zips were extracted successfully, and zip files moved to ImagePerfectTRASH.",
                            WindowStartupLocation = WindowStartupLocation.CenterOwner,
                            SizeToContent = SizeToContent.WidthAndHeight,
                            MinWidth = 500,
                            MinHeight = 400,

                        }
                    ).ShowWindowDialogAsync(Globals.MainWindow);
                }
                _mainWindowViewModel.ShowLoading = false;
            }
        }
    }
}