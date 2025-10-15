using System;
using System.Collections.Generic;
using MsBox.Avalonia.Enums;
using MsBox.Avalonia;
using System.Diagnostics;
using System.IO;
using ReactiveUI;
using ImagePerfect.Helpers;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Models;
using Avalonia.Controls;

namespace ImagePerfect.ViewModels
{
	public class ExternalProgramViewModel : ViewModelBase
	{
        private readonly MainWindowViewModel _mainWindowViewModel;
        public ExternalProgramViewModel(MainWindowViewModel mainWindowViewModel) 
        {
            _mainWindowViewModel = mainWindowViewModel;
        }
        public async void OpenImageInExternalViewer(ImageViewModel imageVm)
        {
            string? externalImageViewerExePath = _mainWindowViewModel.SettingsVm.ExternalImageViewerExePath;
            string imagePathForProcessStart = PathHelper.FormatFilePathForProcessStart(imageVm.ImagePath);
            if (!File.Exists(imageVm.ImagePath))
            {
                await MessageBoxManager.GetMessageBoxCustom(
                    new MessageBoxCustomParams
                    {
                        ButtonDefinitions = new List<ButtonDefinition>
                        {
                            new ButtonDefinition { Name = "Ok", },
                        },
                        ContentTitle = "Open Image",
                        ContentMessage = $"This image file no longer exists.",
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        SizeToContent = SizeToContent.WidthAndHeight,  // <-- lets it grow with content
                        MinWidth = 500  // optional, so it doesn’t wrap too soon
                    }
                ).ShowWindowDialogAsync(Globals.MainWindow);
                return;
            }
            if (File.Exists(externalImageViewerExePath))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = externalImageViewerExePath,
                        Arguments = imagePathForProcessStart,
                        UseShellExecute = false
                    });
                }
                catch (Exception ex) 
                {
                    await MessageBoxManager.GetMessageBoxCustom(
                        new MessageBoxCustomParams
                        {
                            ButtonDefinitions = new List<ButtonDefinition>
                            {
                                new ButtonDefinition { Name = "Ok", },
                            },
                            ContentTitle = "Error",
                            ContentMessage = $"Failed to launch external viewer:\n{ex.Message}",
                            WindowStartupLocation = WindowStartupLocation.CenterOwner,
                            SizeToContent = SizeToContent.WidthAndHeight,  // <-- lets it grow with content
                            MinWidth = 500  // optional, so it doesn’t wrap too soon
                        }
                    ).ShowWindowDialogAsync(Globals.MainWindow);
                }
                
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
                        ContentTitle = "No External Viewer Configured",
                        ContentMessage = $"To open images, you need to choose an external image viewer.\n\n" +
                        $"What to do:\n" +
                        $"1. Install an image viewer if you don't already have one.\n" +
                        $"      -Windows suggestions: Nomacs, XnView, IrfanView\n" +
                        $"      -Linux suggestions: Eye of GNOME (/usr/bin/eog), gThumb, Gwenview\n" +
                        $"2. In ImagePerfect, go to Settings -> Pick External Image Viewer and select the viewer's executable file.\n" +
                        $"      -Example: C:\\Program Files\\nomacs\\bin\\nomacs.exe\n",
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        SizeToContent = SizeToContent.WidthAndHeight,  // <-- lets it grow with content
                        MinWidth = 500  // optional, so it doesn’t wrap too soon
                    }
                ).ShowWindowDialogAsync(Globals.MainWindow);
                return;
            }
        }
        public async void OpenCurrentDirectoryWithExplorer()
        {
            string externalFileExplorerExePath = PathHelper.GetExternalFileExplorerExePath();
            string folderPathForProcessStart = PathHelper.FormatFilePathForProcessStart(_mainWindowViewModel.ExplorerVm.CurrentDirectory);
            if (!File.Exists(externalFileExplorerExePath)) 
            {
                await MessageBoxManager.GetMessageBoxCustom(
                    new MessageBoxCustomParams
                    {
                        ButtonDefinitions = new List<ButtonDefinition>
                        {
                            new ButtonDefinition { Name = "Ok", },
                        },
                        ContentTitle = "Open Directory Error",
                        ContentMessage = $"Your operating system's default file manager could not be found.\n\n" +
                        $"Windows: expected at C:\\Windows\\explorer.exe\n" +
                        $"Linux: expected xdg-open at /usr/bin/xdg-open\n\n" +
                        $"If this tool is installed and you still see this message, please submit a bug report.",
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        SizeToContent = SizeToContent.WidthAndHeight,  // <-- lets it grow with content
                        MinWidth = 500  // optional, so it doesn’t wrap too soon
                    }
                ).ShowWindowDialogAsync(Globals.MainWindow);
                return;
            }
            if (Directory.Exists(_mainWindowViewModel.ExplorerVm.CurrentDirectory))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = externalFileExplorerExePath,
                        Arguments = folderPathForProcessStart,
                        UseShellExecute = false
                    });
                }
                catch (Exception ex) 
                {
                    await MessageBoxManager.GetMessageBoxCustom(
                        new MessageBoxCustomParams
                        {
                            ButtonDefinitions = new List<ButtonDefinition>
                            {
                                new ButtonDefinition { Name = "Ok", },
                            },
                            ContentTitle = "Open Directory Error",
                            ContentMessage = $"Failed to open directory in default file manager:\n{ex.Message}.\n\n" +
                            $"Please submit this error message as a bug report.",
                            WindowStartupLocation = WindowStartupLocation.CenterOwner,
                            SizeToContent = SizeToContent.WidthAndHeight,  // <-- lets it grow with content
                            MinWidth = 500  // optional, so it doesn’t wrap too soon
                        }
                    ).ShowWindowDialogAsync(Globals.MainWindow);
                }
            }
        }
    }
}