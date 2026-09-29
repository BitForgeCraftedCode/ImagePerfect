using System;
using System.Diagnostics;
using System.IO;
using ImagePerfect.Helpers;
using System.Threading.Tasks;

namespace ImagePerfect.ViewModels
{
	public class ExternalProgramViewModel : ViewModelBase
	{
        private readonly MainWindowViewModel _mainWindowViewModel;
        public ExternalProgramViewModel(MainWindowViewModel mainWindowViewModel) 
        {
            _mainWindowViewModel = mainWindowViewModel;
        }
        public async Task OpenImageInExternalViewer(ImageViewModel imageVm)
        {
            string? externalImageViewerExePath = _mainWindowViewModel.SettingsVm.ExternalImageViewerExePath;
            string imagePathForProcessStart = PathHelper.FormatFilePathForProcessStart(imageVm.ImagePath);
            if (!File.Exists(imageVm.ImagePath))
            {
                await MessageBoxHelper.ShowAsync(
                    "Open Image",
                    $"This image file no longer exists."
                );
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
                    await MessageBoxHelper.ShowAsync(
                        "Error",
                        $"Failed to launch external viewer:\n{ex.Message}"
                    );
                }
                
            }
            else
            {
                await MessageBoxHelper.ShowAsync(
                    "No External Viewer Configured",
                    $"To open images, you need to choose an external image viewer.\n\n" +
                    $"What to do:\n" +
                    $"1. Install an image viewer if you don't already have one.\n" +
                    $"      -Windows suggestions: Nomacs, XnView, IrfanView\n" +
                    $"      -Linux suggestions: Eye of GNOME (/usr/bin/eog), gThumb, Gwenview\n" +
                    $"2. In ImagePerfect, go to Settings -> Pick External Image Viewer and select the viewer's executable file.\n" +
                    $"      -Example: C:\\Program Files\\nomacs\\bin\\nomacs.exe\n"
                );
                return;
            }
        }
        public async Task OpenCurrentDirectoryWithExplorer()
        {
            string externalFileExplorerExePath = PathHelper.GetExternalFileExplorerExePath();
            string folderPathForProcessStart = PathHelper.FormatFilePathForProcessStart(_mainWindowViewModel.ExplorerVm.CurrentDirectory);
            if (!File.Exists(externalFileExplorerExePath)) 
            {
                await MessageBoxHelper.ShowAsync(
                    "Open Directory Error",
                    $"Your operating system's default file manager could not be found.\n\n" +
                    $"Windows: expected at C:\\Windows\\explorer.exe\n" +
                    $"Linux: expected xdg-open at /usr/bin/xdg-open\n\n" +
                    $"If this tool is installed and you still see this message, please submit a bug report."
                );
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
                    await MessageBoxHelper.ShowAsync(
                        "Open Directory Error",
                        $"Failed to open directory in default file manager:\n{ex.Message}.\n\n" +
                        $"Please submit this error message as a bug report."
                    );
                }
            }
        }
    }
}