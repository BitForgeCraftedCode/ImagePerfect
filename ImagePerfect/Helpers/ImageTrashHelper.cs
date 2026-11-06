using ImagePerfect.Models;
using ImagePerfect.ViewModels;

namespace ImagePerfect.Helpers
{
    public static class ImageTrashHelper
    {
        public static ImageTrash GetImageTrashModel(ImageViewModel imageVm)
        {
            ImageTrash trashedImage = new ImageTrash
            {
                ImageId = imageVm.ImageId,
                ImagePath = imageVm.ImagePath,
                FileName = imageVm.FileName,
                ImageRating = imageVm.ImageRating,
                ImageFolderPath = imageVm.ImageFolderPath,
                ImageMetaDataScanned = imageVm.ImageMetaDataScanned,
                FolderId = imageVm.FolderId,
                DateTaken = imageVm.DateTaken
            };
            return trashedImage;
        }
    }
}
