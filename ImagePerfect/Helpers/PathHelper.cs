using ImagePerfect.Models;
using ImagePerfect.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImagePerfect.Helpers
{
    //this whole class will need platform specific code to handle file paths. Linux vs Windows paths will be different
    public static class PathHelper
    {
        public static string FormatPathForLikeOperator(string path)
        {
            #if WINDOWS
            return path.Replace(@"\", @"\\\\") + @"%";
            #else
            return path + @"%";
            #endif
        }

        //regular expression string used in sql with REGEXP_LIKE to get all folders in directory (NOT Their sub folders)
        //Only gets the folders in the path -- the folder itself or any sub directories within each folder are not returned
        public static string GetRegExpStringAllFoldersInDirectory(string path)
        {
            #if WINDOWS
            return path.Replace(@"\", @"\\\\") + @"\\\\[^\\\\]+\\\\?$";
            #else
            return path + @"/[^/]+/?$";
            #endif
        }
        //regexp string to get folder and all subfolders
        //gets the folder itself as well as all folders and subfolders within. 
        //the entire directory tree of the path
        public static string GetRegExpStringDirectoryTree(string path)
        {
            #if WINDOWS
            return path.Replace(@"\",@"\\\\");
            #else
            return path;
            #endif
        }
        public static string AddNewFolderNameToPathForDirectoryMoveFolder(string newFolderPath, string newFolderName)
        {
            return Path.Combine(newFolderPath, newFolderName);
        }
        /*
            say for example you want to move C:\Users\arogala\Documents\CSharp\SamplePictures\space 
            to C:\Users\arogala\Documents\CSharp\SamplePictures\Dad
            All this function does is removes everything in the string before space and adds that onto moveToFolderPath

            C:\Users\arogala\Documents\CSharp\SamplePictures\Dad + \ + space

            it does this for each folder with in space
        
            folder list must contain sub directores if any
         */
        public static List<Folder> ModifyFolderPathsForFolderMove(List<Folder> folders, string currentFolderName, string moveToFolderPath)
        {
            for(int i = 0; i < folders.Count; i++)
            {
                string newPath = string.Empty;
                string newCoverImagePath = string.Empty;
                newPath = Path.Combine(moveToFolderPath, ReturnPartialPath(folders[i].FolderPath, currentFolderName));
                folders[i].FolderPath = newPath;
                if (folders[i].CoverImagePath != "")
                {
                    newCoverImagePath = Path.Combine(moveToFolderPath, ReturnPartialPath(folders[i].CoverImagePath, currentFolderName));
                    folders[i].CoverImagePath = newCoverImagePath;
                }
                
            }
            return folders;
        }
        //does basically the same thing as the folder method
        public static List<Image> ModifyImagePathsForFolderMove(List<Image> images, string currentFolderName, string moveToFolderPath) 
        {
            for (int i = 0; i < images.Count; i++) 
            {
                string newPath = string.Empty;
                string newImageFolderPath = string.Empty;
                newPath = Path.Combine(moveToFolderPath, ReturnPartialPath(images[i].ImagePath, currentFolderName));
                newImageFolderPath = Path.Combine(moveToFolderPath, ReturnPartialPath(images[i].ImageFolderPath, currentFolderName));
                images[i].ImagePath = newPath;
                images[i].ImageFolderPath = newImageFolderPath;
            }
            return images;
        }

        public static string GetCoverImagePathForCopyCoverImageToContainingFolder(FolderViewModel folderVm)
        {
            string parent = RemoveOneFolderFromPath(folderVm.FolderPath);
            string fileName = GetFileNameFromImagePath(folderVm.CoverImagePath);
            return Path.Combine(parent, fileName);
        }

        public static List<ImageViewModel> ModifyImagePathsForMoveImagesToNewFolder(List<ImageViewModel> imagesToMove, Folder imagesNewFolder) 
        {
            List<ImageViewModel> imagesToMoveModifiedPaths = new List<ImageViewModel>();

            //keep a hashset of used file names to avoid duplicates within this move
            //hashset used to basically guard against renaming one file to a name that already exits.
            //if dog.jpg is in dest and dog.jpg is in source then source becomes dog_1.jpg but if source already has dog_1.jpg this is a problem.
            //safety net so we don't accidentally assign the same new name twice within this batch.
            HashSet<string> usedFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (ImageViewModel imgToMove in imagesToMove)
            {
                // deep copy
                ImageViewModel newImg = new ImageViewModel(imgToMove);

                string dir = imagesNewFolder.FolderPath;
                string fileName = Path.GetFileNameWithoutExtension(newImg.FileName);
                string ext = Path.GetExtension(newImg.FileName);
                string candidateFileName = fileName + ext;
                string candidatePath = Path.Combine(dir, candidateFileName);

                int counter = 1;
                // check both: existing files on disk + already renamed in this batch
                while (File.Exists(candidatePath) || usedFileNames.Contains(candidateFileName))
                {
                    candidateFileName = $"{fileName}_({counter}){ext}";
                    candidatePath = Path.Combine(dir, candidateFileName);
                    counter++;
                }

                // update model with conflict-free path
                newImg.FileName = candidateFileName;
                newImg.ImagePath = candidatePath;
                newImg.ImageFolderPath = dir;
                newImg.FolderId = imagesNewFolder.FolderId;

                // remember this file name
                usedFileNames.Add(candidateFileName);

                imagesToMoveModifiedPaths.Add(newImg);
            }
            return imagesToMoveModifiedPaths;
        }
        //returns path to end starting at the provied folderName
        //path must contain folderName
        private static string ReturnPartialPath(string path, string folderName)
        {
            string[] parent = path.Split(Path.DirectorySeparatorChar)
                         .TakeWhile(p => !p.Equals(folderName, StringComparison.OrdinalIgnoreCase))
                         .ToArray();

            string prefix = string.Join(Path.DirectorySeparatorChar, parent);
            return path.Substring(prefix.Length).TrimStart(Path.DirectorySeparatorChar);
        }

        public static string GetFileNameFromImagePath(string path)
        {
            return Path.GetFileName(path);
        }

        public static string GetFolderNameFromFolderPath(string path)
        {
            return Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar));
        }
        public static string RemoveOneFolderFromPath(string path)
        {
            return Path.GetDirectoryName(path) ?? string.Empty;
        }

        public static string GetHistroyDisplayNameFromPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;

            string lastFolder = Path.GetFileName(path) ?? string.Empty;

            string parent = Path.GetDirectoryName(path);      
            if (string.IsNullOrEmpty(parent)) 
                return lastFolder; // fallback: only one folder available

            string grandParent = Path.GetFileName(parent) ?? string.Empty;
            if (string.IsNullOrEmpty(grandParent))
                return lastFolder; // fallback if parent is root or empty

            return Path.Combine(grandParent, lastFolder);    // grandparent/parent
        }
        public static string RemoveTwoFoldersFromPath(string path)
        {
            string parent = Path.GetDirectoryName(path);
            if(parent == null) return string.Empty;
            return Path.GetDirectoryName(parent) ?? string.Empty;   
        }
        //#if, #else, #endif are conditional compilation directives
        public static string FormatPathForDbStorage(string path)
        {
            #if WINDOWS
            return path.Replace(@"\", @"\\");
            #else
            return path;
            #endif
        }

        public static string FormatPathFromFolderPicker(string path)
        {
            if (path.StartsWith("file:///"))
                path = path.Substring("file:///".Length);
            if (path.EndsWith('/'))
                path = path.TrimEnd('/');
            #if WINDOWS
                path = path.Replace('/', '\\');
            #else
                if (!path.StartsWith("/"))
                    path = "/" + path;
            #endif
            return path;
        }

        public static string FormatPathFromFilePicker(string path)
        {
            if (path.StartsWith("file:///"))
                path = path.Substring("file:///".Length);
            #if WINDOWS
                path = path.Replace('/', '\\');
            #else
                if (!path.StartsWith("/"))
                    path = "/" + path;
            #endif
            return path;
        }

        public static string GetExternalFileExplorerExePath()
        {
            #if WINDOWS
            return @"C:\Windows\explorer.exe";
            #else
            return @"/usr/bin/xdg-open";
            #endif
        }

        public static string FormatFilePathForProcessStart(string imagePath)
        {
            //https://stackoverflow.com/questions/1857325/c-sharp-easiest-way-to-parse-filename-with-spaces-eg-c-test-file-with-space
            //escape the " to wrap the path in " like this "C:\pictures\pic with spaces.jpg"
            //this allows the app to open files that have spaces in their names.
            return "\"" + imagePath + "\"";
        }

        public static string GetTrashFolderPath(string rootFolderPath)
        {
            return Path.Combine(rootFolderPath, "ImagePerfectTRASH");
        }
        public static string GetImageFileTrashPath(ImageViewModel imageVm, string trashFolderPath)
        {
            //add a guid to guarantee no image in trash has the same name
            Guid g = Guid.NewGuid();
            return Path.Combine(trashFolderPath, $"{g}_{imageVm.FileName}");
        }

        public static string GetFolderTrashPath(FolderViewModel folderVm, string trashFolderPath)
        {
            //add a guid to guarantee no folder in trash has the same name
            Guid g = Guid.NewGuid();
            return Path.Combine(trashFolderPath, $"{g}_{folderVm.FolderName}");
        }

        public static string GetZipFolderTrashPath(string zipFolderName, string trashFolderPath)
        {
            //add a guid to guarantee no zip file in trash has the same name
            Guid g = Guid.NewGuid();
            return Path.Combine(trashFolderPath, $"{g}_{zipFolderName}");
        }

        public static string GetNewFolderPath(string currentDirectory, string newFolderName)
        {
            return Path.Combine(currentDirectory, newFolderName);
        }
    }
}
