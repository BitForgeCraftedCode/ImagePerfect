using Avalonia.Media.Imaging;
using ImagePerfect.Models;
using ImagePerfect.ViewModels;
using Serilog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ImagePerfect.Helpers
{
    public static class DeepCopy
    {
        //if i can convert ImagePerfect to use a WriteableBitmap this could be useful for deep copy
        private static Bitmap DeepCopyWriteableBitmap(WriteableBitmap source)
        {
            var size = source.PixelSize;
            var pixelFormat = source.Format;
            var newBitmap = new WriteableBitmap(size, source.Dpi, pixelFormat);

            using (var srcLock = source.Lock())
            using (var dstLock = newBitmap.Lock())
            {
                int height = size.Height;

                // Use RowBytes (bytes per row) from the locked framebuffer
                int srcRowBytes = srcLock.RowBytes;
                int dstRowBytes = dstLock.RowBytes;

                int bytesPerLine = Math.Min(srcRowBytes, dstRowBytes);

                var buffer = new byte[bytesPerLine];

                for (int y = 0; y < height; y++)
                {
                    IntPtr srcPtr = srcLock.Address + y * srcRowBytes;
                    IntPtr dstPtr = dstLock.Address + y * dstRowBytes;

                    Marshal.Copy(srcPtr, buffer, 0, bytesPerLine);
                    Marshal.Copy(buffer, 0, dstPtr, bytesPerLine);
                }
            }

            return newBitmap;
        }
        public static async Task<ImageViewModel> CopyImageVm(ImageViewModel imageVm)
        {
            ImageViewModel copy = new ImageViewModel
            {
                ImageId = imageVm.ImageId,
                ImagePath = imageVm.ImagePath,
                FileName = imageVm.FileName,
                ImageTags = imageVm.ImageTags,
                NewTag = imageVm.NewTag,
                ImageRating = imageVm.ImageRating,
                ImageFolderPath = imageVm.ImageFolderPath,
                ImageMetaDataScanned = imageVm.ImageMetaDataScanned,
                FolderId = imageVm.FolderId,
                DateTaken = imageVm.DateTaken,
                DateTakenYear = imageVm.DateTakenYear,
                DateTakenMonth = imageVm.DateTakenMonth,
                DateTakenDay = imageVm.DateTakenDay,
                IsSelected = imageVm.IsSelected,
                ShowAddMultipleImageTags = imageVm.ShowAddMultipleImageTags,
            };
            // async copy bitmap
            if (imageVm.ImageBitmap != null)
            {
                try
                {
                    await Task.Run(() =>
                    {
                        using var ms = new MemoryStream();
                        imageVm.ImageBitmap.Save(ms);
                        ms.Position = 0;
                        copy.ImageBitmap = new Bitmap(ms);
                    });
                }
                catch (Exception ex) 
                {
                    Log.Error(ex,
                        "Failed to deep-copy ImageBitmap for ImageId {ImageId}, Path {Path}",
                        imageVm.ImageId,
                        imageVm.ImagePath);
                    throw;
                }
            }
            copy.Tags = imageVm.Tags.Select(t => new ImageTag
            {
                TagId = t.TagId,
                TagName = t.TagName,
                ImageId = t.ImageId
            }).ToList();
            copy.Stars = new ObservableCollection<StarItem>(
                imageVm.Stars.Select(s => new StarItem(s.Number) { IsFilled = s.IsFilled })
            );
            return copy;
        }

        public static async Task<FolderViewModel> CopyFolderVm(FolderViewModel folderVm)
        {
            FolderViewModel copy = new FolderViewModel
            {
                FolderId = folderVm.FolderId,
                FolderName = folderVm.FolderName,
                FolderPath = folderVm.FolderPath,
                HasChildren = folderVm.HasChildren,
                CoverImagePath = folderVm.CoverImagePath,
                FolderDescription = folderVm.FolderDescription,
                FolderTags = folderVm.FolderTags,
                NewTag = folderVm.NewTag,
                FolderRating = folderVm.FolderRating,
                HasFiles = folderVm.HasFiles,
                IsRoot = folderVm.IsRoot,
                FolderContentMetaDataScanned = folderVm.FolderContentMetaDataScanned,
                AreImagesImported = folderVm.AreImagesImported,
                ShowImportImagesButton = folderVm.ShowImportImagesButton,
            };

            //async copy bitmap
            if (folderVm.CoverImageBitmap != null)
            {
                try
                {
                    await Task.Run(() =>
                    {
                        using var ms = new MemoryStream();
                        folderVm.CoverImageBitmap.Save(ms);
                        ms.Position = 0;
                        copy.CoverImageBitmap = new Bitmap(ms);
                    });
                }
                catch (Exception ex) 
                {
                    Log.Error(ex,
                        "Failed to deep-copy CoverImageBitmap for FolderId {FolderId}, Path {Path}",
                        folderVm.FolderId,
                        folderVm.FolderPath);
                    throw;
                }
            }
            copy.Tags = folderVm.Tags.Select(t => new FolderTag
            {
                TagId = t.TagId,
                TagName = t.TagName,
                FolderId = t.FolderId
            }).ToList();
            copy.Stars = new ObservableCollection<StarItem>(
                folderVm.Stars.Select(s => new StarItem(s.Number) { IsFilled = s.IsFilled })
            );
            return copy;
        }
    }
}
