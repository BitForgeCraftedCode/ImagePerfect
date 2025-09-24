using System;
using System.Collections.Generic;
using MsBox.Avalonia.Enums;
using MsBox.Avalonia;
using System.Diagnostics;
using System.IO;
using ReactiveUI;
using ImagePerfect.Helpers;

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
            string imagePathForProcessStart = PathHelper.FormatImageFilePathForProcessStart(imageVm.ImagePath);
            if (!File.Exists(imageVm.ImagePath))
            {
                await MessageBoxManager.GetMessageBoxStandard(
                    "Open Image",
                    "This image file no longer exists.",
                    ButtonEnum.Ok).ShowAsync();
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
                    await MessageBoxManager.GetMessageBoxStandard(
                        "Error",
                        $"Failed to launch external viewer:\n{ex.Message}",
                        ButtonEnum.Ok).ShowAsync();
                }
                
            }
            else
            {
                await MessageBoxManager.GetMessageBoxStandard(
                    "No External Viewer Configured",
                    $"To open images, you need to choose an external image viewer.\n\n" +
                    $"What to do:\n" +
                    $"1. Install an image viewer if you don't already have one.\n" +
                    $"      -Windows suggestions: Nomacs, XnView, IrfanView\n" +
                    $"      -Linux suggestions: Eye of GNOME (/usr/bin/eog), gThumb, Gwenview\n"+
                    $"2. In ImagePerfect, go to Settings -> Pick External Image Viewer and select the viewer's executable file.\n" +
                    $"      -Example: C:\\Program Files\\nomacs\\bin\\nomacs.exe\n",
                    ButtonEnum.Ok).ShowAsync();

                return;
            }
        }
        public async void OpenCurrentDirectoryWithExplorer()
        {
            string externalFileExplorerExePath = PathHelper.GetExternalFileExplorerExePath();
            string folderPathForProcessStart = PathHelper.FormatImageFilePathForProcessStart(_mainWindowViewModel.CurrentDirectory); //not an image path but all this did was wrap it in quotes
            if (File.Exists(externalFileExplorerExePath) && Directory.Exists(_mainWindowViewModel.CurrentDirectory))
            {
                Process.Start(externalFileExplorerExePath, folderPathForProcessStart);
            }
            else
            {
                var box = MessageBoxManager.GetMessageBoxStandard("Open Folder", "Sorry something went wrong.", ButtonEnum.Ok);
                await box.ShowAsync();
                return;
            }
        }
    }
}