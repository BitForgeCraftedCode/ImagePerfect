using ImagePerfect.Models;
using ImagePerfect.ViewModels;
using System.Collections.Generic;
using System.Text;


namespace ImagePerfect.Helpers
{
    public static class SqlStringBuilder
    {
        public static string BuildSqlForMoveImagesToTrash(List<ImageViewModel> images)
        {
            StringBuilder sb = new StringBuilder("DELETE FROM images WHERE ImageId IN (");
            for (int i = 0; i < images.Count; i++) 
            { 
                if(i <  images.Count - 1)
                {
                    sb.Append($"{images[i].ImageId},");
                }
                else
                {
                    sb.Append($"{images[i].ImageId}");
                }
            }
            sb.Append(");");
            return sb.ToString();
        }

        public static (string Sql, Dapper.DynamicParameters Parameters) BuildSqlForMoveImagesToNewFolder(List<ImageViewModel> imagesToMove)
        {
            Dapper.DynamicParameters parameters = new Dapper.DynamicParameters();
            if (imagesToMove.Count == 0)
                return (string.Empty, parameters);

            StringBuilder sb = new StringBuilder("UPDATE images SET ImagePath = CASE ");
            for (int i = 0; i < imagesToMove.Count; i++)
            {
                ImageViewModel image = imagesToMove[i];
                string imagePathParameter = $"imagePath{i}";
                parameters.Add(imagePathParameter, image.ImagePath);
                sb.Append($"WHEN ImageId = {image.ImageId} THEN @{imagePathParameter} ");
            }
            sb.Append("ELSE ImagePath END, ImageFolderPath = CASE ");
            for (int i = 0; i < imagesToMove.Count; i++)
            {
                ImageViewModel image = imagesToMove[i];
                string imageFolderPathParameter = $"imageFolderPath{i}";
                parameters.Add(imageFolderPathParameter, image.ImageFolderPath);
                sb.Append($"WHEN ImageId = {image.ImageId} THEN @{imageFolderPathParameter} ");
            }
            sb.Append("ELSE ImageFolderPath END, FolderId = CASE ");
            foreach (ImageViewModel image in imagesToMove)
            {
                sb.Append($"WHEN ImageId = {image.ImageId} THEN {image.FolderId} ");
            }
            sb.Append($"ELSE FolderId END WHERE ImageId IN (");
            for (int i = 0; i < imagesToMove.Count; i++)
            {
                if (i < imagesToMove.Count - 1)
                {
                    sb.Append($"{imagesToMove[i].ImageId},");
                }
                else
                {
                    sb.Append($"{imagesToMove[i].ImageId}");
                }
            }
            sb.Append(");");
            return (sb.ToString(), parameters);
        }
        public static (string Sql, Dapper.DynamicParameters Parameters) BuildFolderSqlForFolderMove(List<Folder> folders)
        {
            Dapper.DynamicParameters parameters = new Dapper.DynamicParameters();
            if (folders.Count == 0)
                return (string.Empty, parameters);

            //need two sql string one that updates both folder path and coverimage path
            //and one just for folder path where there are no cover images
            int coverImageCount = 0;
            foreach (Folder folder in folders)
            {
                if (folder.CoverImagePath != "")
                {
                    coverImageCount = coverImageCount + 1;
                }
            }

            StringBuilder sb = new StringBuilder("UPDATE folders SET FolderPath = CASE ");
            for (int i = 0; i < folders.Count; i++)
            {
                Folder folder = folders[i];
                string folderPathParameter = $"folderPath{i}";
                parameters.Add(folderPathParameter, folder.FolderPath);
                sb.Append($"WHEN FolderId = {folder.FolderId} THEN @{folderPathParameter} ");
            }
            if (coverImageCount > 0)
            {
                sb.Append("ELSE FolderPath End, CoverImagePath = CASE ");
                for (int i = 0; i < folders.Count; i++)
                {
                    Folder folder = folders[i];
                    if (folder.CoverImagePath != "")
                    {
                        string coverImagePathParameter = $"coverImagePath{i}";
                        parameters.Add(coverImagePathParameter, folder.CoverImagePath);
                        sb.Append($"WHEN FolderId = {folder.FolderId} THEN @{coverImagePathParameter} ");
                    }
                }
                sb.Append("ELSE CoverImagePath END WHERE FolderId IN (");
                for (int i = 0; i < folders.Count; i++)
                {
                    if (i < folders.Count - 1)
                    {
                        sb.Append($"{folders[i].FolderId},");
                    }
                    else
                    {
                        sb.Append($"{folders[i].FolderId}");
                    }
                }
                sb.Append(");");
            }
            else
            {
                sb.Append("ELSE FolderPath END WHERE FolderId IN (");
                for (int i = 0; i < folders.Count; i++)
                {
                    if (i < folders.Count - 1)
                    {
                        sb.Append($"{folders[i].FolderId},");
                    }
                    else
                    {
                        sb.Append($"{folders[i].FolderId}");
                    }
                }
                sb.Append(");");
            }

            return (sb.ToString(), parameters);
        }
        /*
         * A parameterized query prevents a folder named Vacation's from breaking the query
         * Id's are database stored and users do not edit them so there is no query breaking risk
         */
        public static (string Sql, Dapper.DynamicParameters Parameters) BuildImageSqlForFolderMove(List<Image> images)
        {
            Dapper.DynamicParameters parameters = new Dapper.DynamicParameters();
            if (images.Count == 0)
                return (string.Empty, parameters);

            StringBuilder sb = new StringBuilder("UPDATE images SET ImagePath = CASE ");
            for (int i = 0; i < images.Count; i++)
            {
                Image image = images[i];
                string imagePathParameter = $"imagePath{i}";
                parameters.Add(imagePathParameter, image.ImagePath);
                sb.Append($"WHEN ImageId = {image.ImageId} THEN @{imagePathParameter} ");
            }
            sb.Append("ELSE ImagePath END, ImageFolderPath = CASE ");
            for (int i = 0; i < images.Count; i++)
            {
                Image image = images[i];
                string imageFolderPathParameter = $"imageFolderPath{i}";
                parameters.Add(imageFolderPathParameter, image.ImageFolderPath);
                sb.Append($"WHEN ImageId = {image.ImageId} THEN @{imageFolderPathParameter} ");
            }
            sb.Append("ELSE ImageFolderPath END WHERE ImageId IN (");
            for (int i = 0; i < images.Count; i++)
            {
                if (i < images.Count - 1)
                {
                    sb.Append($"{images[i].ImageId},");
                }
                else
                {
                    sb.Append($"{images[i].ImageId}");
                }
            }
            sb.Append(");");
            return (sb.ToString(), parameters);
        }

        public static string BuildSqlForBulkInsertImageRating(List<Image> images)
        {
            StringBuilder sb = new StringBuilder("UPDATE images SET ImageRating = CASE ");
            foreach (Image image in images) 
            {
                sb.Append($"WHEN ImageId = {image.ImageId} THEN {image.ImageRating} ");
            }
            sb.Append("ELSE ImageRating END, ImageMetaDataScanned = CASE ");
            foreach (Image image in images)
            {
                sb.Append($"WHEN ImageId = {image.ImageId} THEN 1 ");
            }
            sb.Append("ELSE ImageMetaDataScanned END, DateTaken = CASE ");
            foreach(Image image in images)
            {
                if(image.DateTaken != null)
                {
                    sb.Append($"WHEN ImageId = {image.ImageId} THEN '{image.DateTaken?.ToString("yyyy-MM-dd HH:mm:ss")}' ");
                }
                else
                {
                    sb.Append($"WHEN ImageId = {image.ImageId} THEN NULL ");
                }
            }
            sb.Append("ELSE DateTaken END WHERE ImageId IN (");
            for (int i = 0; i < images.Count; i++) 
            {
                if (i < images.Count - 1)
                {
                    sb.Append($"{images[i].ImageId},");
                }
                else
                {
                    sb.Append($"{images[i].ImageId}");
                }
            }
            sb.Append(");");
            return sb.ToString();
        }

        public static string BuildSqlForBulkInsertImageTags(List<string> allTags)
        {
            StringBuilder sb = new StringBuilder("INSERT IGNORE INTO tags (TagName) VALUES ");
            for(int i = 0; i < allTags.Count; i++)
            {
                if(i < allTags.Count - 1)
                {
                    sb.Append($"('{allTags[i]}'),");
                }
                else
                {
                    sb.Append($"('{allTags[i]}');");
                }
            }
            return sb.ToString();
        }

        public static string BuildSqlForBulkInsertImageTagsJoin(List<(int imageId, int tagId)> imageTagJoins)
        {
            StringBuilder sb = new StringBuilder("INSERT INTO image_tags_join (ImageId, TagId) VALUES ");
            for(int i = 0; i < imageTagJoins.Count; i++)
            {
                if(i < imageTagJoins.Count - 1)
                {
                    sb.Append($"({imageTagJoins[i].imageId}, {imageTagJoins[i].tagId}),");
                }
                else
                {
                    sb.Append($"({imageTagJoins[i].imageId}, {imageTagJoins[i].tagId});");
                }

            }
            return sb.ToString();
        }
        public static string BuildSqlForAddMultipleImageTags(List<Tag> tagsToAdd, ImageViewModel imageVm)
        {
            StringBuilder sb = new StringBuilder("INSERT INTO image_tags_join (ImageId, TagId) VALUES ");
            for(int i = 0; i < tagsToAdd.Count; i++)
            {
                if(i < tagsToAdd.Count - 1)
                {
                    sb.Append($"({imageVm.ImageId}, {tagsToAdd[i].TagId}),");
                }
                else
                {
                    sb.Append($"({imageVm.ImageId}, {tagsToAdd[i].TagId});");
                }
            }
            
            return sb.ToString();
        }

        public static string BuildSqlForBulkInsertFolderTag(Tag tagToAdd, List<Folder> folders)
        {
            //IGNORE to ignore duplicate tags. If a folder already has this tag just IGNORE that one but do the rest
            StringBuilder sb = new StringBuilder("INSERT IGNORE INTO folder_tags_join (FolderId, TagId) VALUES ");
            for (int i = 0; i < folders.Count; i++) 
            { 
                if(i < folders.Count - 1)
                {
                    sb.Append($"({folders[i].FolderId}, {tagToAdd.TagId}),");
                }
                else
                {
                    sb.Append($"({folders[i].FolderId}, {tagToAdd.TagId});");
                }
            }
            return sb.ToString();
        }
    }
}
