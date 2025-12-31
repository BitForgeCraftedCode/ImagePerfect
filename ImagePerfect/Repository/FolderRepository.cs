using Dapper;
using ImagePerfect.Helpers;
using ImagePerfect.Models;
using ImagePerfect.Repository.IRepository;
using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Threading.Tasks;

namespace ImagePerfect.Repository
{
    public class FolderRepository : Repository<Folder>, IFolderRepository
    {
        private readonly MySqlConnection _connection;

        public FolderRepository(MySqlConnection db) : base(db) 
        {
            _connection = db;
        }
        //any Folder model specific database methods here
        public async Task<Folder?> GetRootFolder()
        {
            MySqlTransaction txn = await _connection.BeginTransactionAsync();
            string sql1 = @"SELECT * FROM folders WHERE IsRoot = 1";
            Folder? rootFolder = await _connection.QuerySingleOrDefaultAsync<Folder>(sql1, transaction: txn);
            string sql2 = @"SELECT tags.TagId, tags.TagName, folders.FolderId FROM folders
	                            JOIN folder_tags_join ON folder_tags_join.FolderId = folders.FolderId
	                            JOIN tags ON folder_tags_join.TagId = tags.TagId WHERE folders.IsRoot = 1;";
            if (rootFolder != null) 
            { 
                List<FolderTag> tags = (List<FolderTag>)await _connection.QueryAsync<FolderTag>(sql2, transaction: txn);
                rootFolder.Tags = tags;
            }
            await txn.CommitAsync();
            return rootFolder;
        }
        public async Task<bool> AddFolderCsv(string filePath)
        {
            int rowsEffected = 0;
            var bulkLoader = new MySqlBulkLoader(_connection)
            {
                FileName = filePath,
                TableName = "folders",
                CharacterSet = "UTF8",
                NumberOfLinesToSkip = 1,
                FieldTerminator = ",",
                FieldQuotationCharacter = '"',
                FieldQuotationOptional = true,
                Local = true,
            };
            rowsEffected = await bulkLoader.LoadAsync();
            return rowsEffected > 0 ? true : false;
        }

        //Only gets the folders in the path -- the folder itself or any sub directories within each folder are not returned
        public async Task<(List<Folder> folders, List<FolderTag> tags)> GetFoldersInDirectory(string directoryPath, bool ascending)
        {
            MySqlTransaction txn = await _connection.BeginTransactionAsync();
            string regExpString = PathHelper.GetRegExpStringAllFoldersInDirectory(directoryPath);
            string order = ascending ? "ASC" : "DESC";
            string sql1 = $@"SELECT * FROM folders WHERE REGEXP_LIKE(FolderPath, @Pattern) ORDER BY FolderPath {order}, FolderName {order};";
            List<Folder> folders = (List<Folder>)await _connection.QueryAsync<Folder>(sql1, new { Pattern = regExpString }, transaction: txn);
            string sql2 = $@"SELECT tags.TagId, tags.TagName, folders.FolderId FROM folders 
                            JOIN folder_tags_join ON folder_tags_join.FolderId = folders.FolderId 
                            JOIN tags ON folder_tags_join.TagId = tags.TagId WHERE REGEXP_LIKE(folders.FolderPath, @Pattern) ORDER BY folders.FolderPath {order}, folders.FolderName {order};";
            List<FolderTag> tags = (List<FolderTag>)await _connection.QueryAsync<FolderTag>(sql2, new { Pattern = regExpString }, transaction: txn);
            await txn.CommitAsync();
            return (folders,tags);
        }

        public async Task<(List<Folder> folders, List<FolderTag> tags)> GetFoldersInDirectoryByStartingLetter(string directoryPath, bool ascending, string letter)
        {
            MySqlTransaction txn = await _connection.BeginTransactionAsync();
            string regExpString = PathHelper.GetRegExpStringAllFoldersInDirectory(directoryPath);
            string order = ascending ? "ASC" : "DESC";
            string sql1 = $@"SELECT * FROM folders WHERE REGEXP_LIKE(FolderPath, @Pattern) AND FolderName LIKE @Letter ORDER BY FolderPath {order}, FolderName {order};";
            List<Folder> folders = (List<Folder>)await _connection.QueryAsync<Folder>(sql1, new { Pattern = regExpString, Letter = letter + "%" }, transaction: txn);
            string sql2 = $@"SELECT tags.TagId, tags.TagName, folders.FolderId FROM folders 
                            JOIN folder_tags_join ON folder_tags_join.FolderId = folders.FolderId 
                            JOIN tags ON folder_tags_join.TagId = tags.TagId WHERE REGEXP_LIKE(folders.FolderPath, @Pattern) AND FolderName LIKE @Letter ORDER BY folders.FolderPath {order}, folders.FolderName {order};";
            List<FolderTag> tags = (List<FolderTag>)await _connection.QueryAsync<FolderTag>(sql2, new { Pattern = regExpString, Letter = letter + "%" }, transaction: txn);
            await txn.CommitAsync();
            return (folders, tags);
        }

        //get the folder at the path -- only one folder returned
        public async Task<Folder> GetFolderAtDirectory(string directoryPath)
        {
            string sql = "SELECT * FROM folders WHERE FolderPath = @Path";
            Folder folder = (Folder)await _connection.QuerySingleAsync<Folder>(sql, new { Path = directoryPath });
            return folder;
        }

        public async Task<(List<Folder> folders, List<FolderTag> tags)> GetAllFoldersAtRating(int rating, bool filterInCurrentDirectory, string currentDirectory)
        {
            MySqlTransaction txn = await _connection.BeginTransactionAsync();
            string path = PathHelper.FormatPathForLikeOperator(currentDirectory);
            string sql1 = string.Empty;
            string sql2 = string.Empty;
            if (filterInCurrentDirectory)
            {
                sql1 = @"SELECT * FROM folders WHERE FolderRating = @Rating AND FolderPath LIKE @Path ORDER BY FolderPath, FolderName";
                sql2 = @"SELECT tags.TagId, tags.TagName, folders.FolderId FROM folders 
                            JOIN folder_tags_join ON folder_tags_join.FolderId = folders.FolderId 
                            JOIN tags ON folder_tags_join.TagId = tags.TagId WHERE folders.FolderRating = @Rating AND FolderPath LIKE @Path ORDER BY folders.FolderPath, folders.FolderName;";
            }
            else
            {
                sql1 = @"SELECT * FROM folders WHERE FolderRating = @Rating ORDER BY FolderPath, FolderName";
                sql2 = @"SELECT tags.TagId, tags.TagName, folders.FolderId FROM folders 
                            JOIN folder_tags_join ON folder_tags_join.FolderId = folders.FolderId 
                            JOIN tags ON folder_tags_join.TagId = tags.TagId WHERE folders.FolderRating = @Rating ORDER BY folders.FolderPath, folders.FolderName;";
            }
            
            List<Folder> allFoldersAtRating = (List<Folder>)await _connection.QueryAsync<Folder>(sql1, new { Path = path, Rating = rating }, transaction: txn);
            List<FolderTag> tags = (List<FolderTag>)await _connection.QueryAsync<FolderTag>(sql2, new { Path = path, Rating = rating }, transaction: txn);
            await txn.CommitAsync();
            return (allFoldersAtRating,tags);
        }

        public async Task<(List<Folder> folders, List<FolderTag> tags)> GetAllFoldersWithRatingAndTag(int rating, string tagOne, string tagTwo, bool filterInCurrentDirectory, string currentDirectory)
        {
            MySqlTransaction txn = await _connection.BeginTransactionAsync();
            string path = PathHelper.FormatPathForLikeOperator(currentDirectory);
            string sql1 = string.Empty;
            string sql2 = string.Empty;
           
            string[] tagNames = string.IsNullOrEmpty(tagTwo) ? new[] { tagOne } : new[] { tagOne, tagTwo };
            int requiredCount = tagNames.Length;

            
            if (filterInCurrentDirectory)
            {
                sql1 = $@"SELECT folders.* FROM folders
                    JOIN folder_tags_join ON folder_tags_join.FolderId = folders.FolderId
                    JOIN tags ON folder_tags_join.TagId = tags.TagId WHERE folders.FolderRating = @rating AND tags.TagName IN @tagNames AND FolderPath LIKE @path 
                    GROUP BY folders.FolderId HAVING COUNT(DISTINCT tags.TagName) = @requiredCount ORDER BY folders.FolderPath, folders.FolderName;";
                sql2 = $@"SELECT tags.TagId, tags.TagName, folders.FolderId FROM folders 
                        JOIN folder_tags_join ON folder_tags_join.FolderId = folders.FolderId 
                        JOIN tags ON folder_tags_join.TagId = tags.TagId WHERE folders.FolderRating = @rating AND FolderPath LIKE @path ORDER BY folders.FolderPath, folders.FolderName;";
            }
            else
            {
                sql1 = $@"SELECT folders.* FROM folders
                    JOIN folder_tags_join ON folder_tags_join.FolderId = folders.FolderId
                    JOIN tags ON folder_tags_join.TagId = tags.TagId WHERE folders.FolderRating = @rating AND tags.TagName IN @tagNames 
                    GROUP BY folders.FolderId HAVING COUNT(DISTINCT tags.TagName) = @requiredCount ORDER BY folders.FolderPath, folders.FolderName;";
                sql2 = @"SELECT tags.TagId, tags.TagName, folders.FolderId FROM folders 
                        JOIN folder_tags_join ON folder_tags_join.FolderId = folders.FolderId 
                        JOIN tags ON folder_tags_join.TagId = tags.TagId WHERE folders.FolderRating = @rating ORDER BY folders.FolderPath, folders.FolderName;";
            }
            //Note: for sql2 i should just fetch tags for the folders found in the first query -- will have to apply that on all methods
            List<Folder> allFoldersWithRatingAndTag = (List<Folder>)await _connection.QueryAsync<Folder>(sql1, new { path, rating, tagNames, requiredCount }, transaction: txn);
            List<FolderTag> tags = (List<FolderTag>)await _connection.QueryAsync<FolderTag>(sql2, new { path, rating, tagNames, requiredCount }, transaction: txn);
            await txn.CommitAsync();
            return (allFoldersWithRatingAndTag, tags);
        }

        public async Task<(List<Folder> folders, List<FolderTag> tags)> GetAllFoldersWithNoImportedImages(bool filterInCurrentDirectory, string currentDirectory)
        {
            MySqlTransaction txn = await _connection.BeginTransactionAsync();
            string path = PathHelper.FormatPathForLikeOperator(currentDirectory);
            string sql1 = string.Empty;
            string sql2 = string.Empty;
            if (filterInCurrentDirectory) 
            {
                sql1 = @"SELECT * FROM folders WHERE AreImagesImported = false AND HasFiles = true AND FolderPath LIKE @path ORDER BY FolderPath, FolderName";
                sql2 = @"SELECT tags.TagId, tags.TagName, folders.FolderId FROM folders 
                            JOIN folder_tags_join ON folder_tags_join.FolderId = folders.FolderId 
                            JOIN tags ON folder_tags_join.TagId = tags.TagId WHERE folders.AreImagesImported = false AND FolderPath LIKE @path ORDER BY folders.FolderPath, folders.FolderName;";
            }
            else
            {
                sql1 = @"SELECT * FROM folders WHERE AreImagesImported = false AND HasFiles = true ORDER BY FolderPath, FolderName";
                sql2 = @"SELECT tags.TagId, tags.TagName, folders.FolderId FROM folders 
                            JOIN folder_tags_join ON folder_tags_join.FolderId = folders.FolderId 
                            JOIN tags ON folder_tags_join.TagId = tags.TagId WHERE folders.AreImagesImported = false ORDER BY folders.FolderPath, folders.FolderName;";
            }

            List<Folder> allFoldersWithNoImportedImages = (List<Folder>)await _connection.QueryAsync<Folder>(sql1, new { path }, transaction: txn);
            List<FolderTag> tags = (List<FolderTag>)await _connection.QueryAsync<FolderTag>(sql2, new { path }, transaction: txn);
            await txn.CommitAsync();
            return (allFoldersWithNoImportedImages, tags);
        }

        public async Task<(List<Folder> folders, List<FolderTag> tags)> GetAllFoldersWithMetadataNotScanned(bool filterInCurrentDirectory, string currentDirectory)
        {
            MySqlTransaction txn = await _connection.BeginTransactionAsync();
            string path = PathHelper.FormatPathForLikeOperator(currentDirectory);
            string sql1 = string.Empty;
            string sql2 = string.Empty;
            if (filterInCurrentDirectory)
            {
                sql1 = @"SELECT * FROM folders WHERE FolderContentMetaDataScanned = false AND HasFiles = true AND AreImagesImported = true AND FolderPath LIKE @path ORDER BY FolderPath, FolderName";
                sql2 = @"SELECT tags.TagId, tags.TagName, folders.FolderId FROM folders 
                            JOIN folder_tags_join ON folder_tags_join.FolderId = folders.FolderId 
                            JOIN tags ON folder_tags_join.TagId = tags.TagId WHERE folders.FolderContentMetaDataScanned = false AND FolderPath LIKE @path ORDER BY folders.FolderPath, folders.FolderName;";
            }
            else
            {
                sql1 = @"SELECT * FROM folders WHERE FolderContentMetaDataScanned = false AND HasFiles = true AND AreImagesImported = true ORDER BY FolderPath, FolderName";
                sql2 = @"SELECT tags.TagId, tags.TagName, folders.FolderId FROM folders 
                            JOIN folder_tags_join ON folder_tags_join.FolderId = folders.FolderId 
                            JOIN tags ON folder_tags_join.TagId = tags.TagId WHERE folders.FolderContentMetaDataScanned = false ORDER BY folders.FolderPath, folders.FolderName;";
            }

            List<Folder> allFoldersWithMetadataNotScanned = (List<Folder>)await _connection.QueryAsync<Folder>(sql1, new { path }, transaction: txn);
            List<FolderTag> tags = (List<FolderTag>)await _connection.QueryAsync<FolderTag>(sql2, new { path }, transaction: txn);
            await txn.CommitAsync();
            return (allFoldersWithMetadataNotScanned, tags);
        }

        public async Task<(List<Folder> folders, List<FolderTag> tags)> GetAllFoldersWithoutCovers(bool filterInCurrentDirectory, string currentDirectory)
        {
            MySqlTransaction txn = await _connection.BeginTransactionAsync();
            string path = PathHelper.FormatPathForLikeOperator(currentDirectory);
            string sql1 = string.Empty;
            string sql2 = string.Empty;
            if (filterInCurrentDirectory)
            {
                sql1 = @"SELECT * FROM folders WHERE AreImagesImported = true AND HasFiles = true AND CoverImagePath = '' AND FolderPath LIKE @path ORDER BY FolderPath, FolderName";
                sql2 = @"SELECT tags.TagId, tags.TagName, folders.FolderId FROM folders 
                            JOIN folder_tags_join ON folder_tags_join.FolderId = folders.FolderId 
                            JOIN tags ON folder_tags_join.TagId = tags.TagId WHERE folders.AreImagesImported = true AND folders.CoverImagePath = '' AND FolderPath LIKE @path ORDER BY folders.FolderPath, folders.FolderName;";
            }
            else
            {
                sql1 = @"SELECT * FROM folders WHERE AreImagesImported = true AND HasFiles = true AND CoverImagePath = '' ORDER BY FolderPath, FolderName";
                sql2 = @"SELECT tags.TagId, tags.TagName, folders.FolderId FROM folders 
                            JOIN folder_tags_join ON folder_tags_join.FolderId = folders.FolderId 
                            JOIN tags ON folder_tags_join.TagId = tags.TagId WHERE folders.AreImagesImported = true AND folders.CoverImagePath = '' ORDER BY folders.FolderPath, folders.FolderName;";
            }

            List<Folder> allFoldersWithoutCovers = (List<Folder>)await _connection.QueryAsync<Folder>(sql1, new { path }, transaction: txn);
            List<FolderTag> tags = (List<FolderTag>)await _connection.QueryAsync<FolderTag>(sql2, new { path }, transaction: txn);
            await txn.CommitAsync();
            return (allFoldersWithoutCovers, tags);
        }
        public async Task<(List<Folder> folders, List<FolderTag> tags)> GetAllFoldersWithTag(string tag, bool filterInCurrentDirectory, string currentDirectory)
        {
            MySqlTransaction txn = await _connection.BeginTransactionAsync();
            string path = PathHelper.FormatPathForLikeOperator(currentDirectory);
            string sql1 = string.Empty;
            string sql2 = string.Empty;
            if (filterInCurrentDirectory) 
            {
                sql1 = @"SELECT * FROM folders
                            JOIN folder_tags_join ON folder_tags_join.FolderId = folders.FolderId
                            JOIN tags ON folder_tags_join.TagId = tags.TagId WHERE tags.TagName = @tag AND FolderPath LIKE @path ORDER BY folders.FolderPath, folders.FolderName;";
                sql2 = @"SELECT tags.TagId, tags.TagName, folders.FolderId FROM folders
                            JOIN folder_tags_join ON folder_tags_join.FolderId = folders.FolderId
                            JOIN tags ON folder_tags_join.TagId = tags.TagId WHERE FolderPath LIKE @path ORDER BY folders.FolderPath, folders.FolderName;";
            }
            else
            {
                sql1 = @"SELECT * FROM folders
                            JOIN folder_tags_join ON folder_tags_join.FolderId = folders.FolderId
                            JOIN tags ON folder_tags_join.TagId = tags.TagId WHERE tags.TagName = @tag ORDER BY folders.FolderPath, folders.FolderName;";
                sql2 = @"SELECT tags.TagId, tags.TagName, folders.FolderId FROM folders
                            JOIN folder_tags_join ON folder_tags_join.FolderId = folders.FolderId
                            JOIN tags ON folder_tags_join.TagId = tags.TagId ORDER BY folders.FolderPath, folders.FolderName;";
            }
            
            List<Folder> allFoldersWithTag = (List<Folder>)await _connection.QueryAsync<Folder>(sql1, new { path, tag }, transaction: txn);
            List<FolderTag> tags = (List<FolderTag>)await _connection.QueryAsync<FolderTag>(sql2, new { path, tag }, transaction: txn);
            await txn.CommitAsync();
            return (allFoldersWithTag, tags);
        }

        public async Task<(List<Folder> folders, List<FolderTag> tags)> GetAllFoldersWithDescriptionText(string text, bool filterInCurrentDirectory, string currentDirectory)
        {
            MySqlTransaction txn = await _connection.BeginTransactionAsync();
            string regExpString = PathHelper.GetRegExpStringAllFoldersInDirectory(currentDirectory);
            string sql1 = string.Empty;
            string sql2 = string.Empty;
            if (filterInCurrentDirectory)
            {
                sql1 = @"SELECT * FROM folders WHERE REGEXP_LIKE(FolderPath, @Pattern) AND (FolderName LIKE CONCAT('%', @Text, '%') OR FolderDescription LIKE CONCAT('%', @Text, '%')) ORDER BY FolderPath, FolderName;";
                sql2 = @"SELECT tags.TagId, tags.TagName, folders.FolderId FROM folders 
                JOIN folder_tags_join ON folder_tags_join.FolderId = folders.FolderId 
                JOIN tags ON folder_tags_join.TagId = tags.TagId WHERE REGEXP_LIKE(folders.FolderPath, @Pattern) ORDER BY folders.FolderPath, folders.FolderName;";
            }
            else
            {
                sql1 = @"SELECT * FROM folders WHERE (FolderName LIKE CONCAT('%', @Text, '%') OR FolderDescription LIKE CONCAT('%', @Text, '%')) ORDER BY FolderPath, FolderName";
                sql2 = @"SELECT tags.TagId, tags.TagName, folders.FolderId FROM folders
                        JOIN folder_tags_join ON folder_tags_join.FolderId = folders.FolderId
                        JOIN tags ON folder_tags_join.TagId = tags.TagId ORDER BY folders.FolderPath, folders.FolderName;";
            }

            List<Folder> allFoldersWithDescriptionText = (List<Folder>)await _connection.QueryAsync<Folder>(sql1, new { Pattern = regExpString, Text = text }, transaction: txn);
            List<FolderTag> tags = (List<FolderTag>)await _connection.QueryAsync<FolderTag>(sql2, new { Pattern = regExpString }, transaction: txn);
            await txn.CommitAsync();
            return (allFoldersWithDescriptionText, tags);
        }

        public async Task<(List<Folder> folders, List<FolderTag> tags)> GetAllFavoriteFolders()
        {
            MySqlTransaction txn = await _connection.BeginTransactionAsync();
            string sql1 = @"SELECT * FROM folders WHERE FolderId IN (SELECT FolderId FROM folder_saved_favorites) ORDER BY FolderPath, FolderName";
            string sql2 = @"SELECT tags.TagId, tags.TagName, folders.FolderId FROM folders
                                JOIN folder_tags_join ON folder_tags_join.FolderId = folders.FolderId
                                JOIN tags ON folder_tags_join.TagId = tags.TagId WHERE folders.FolderId IN (SELECT folder_saved_favorites.FolderId FROM folder_saved_favorites) ORDER BY FolderPath, FolderName";
            List<Folder> allFavoriteFolders = (List<Folder>)await _connection.QueryAsync<Folder>(sql1, transaction: txn);
            List<FolderTag> tags = (List<FolderTag>)await _connection.QueryAsync<FolderTag>(sql2, transaction: txn);
            await txn.CommitAsync();
            return (allFavoriteFolders, tags);
        }

        //gets the folder itself as well as all folders and subfolders within. 
        //the entire directory tree of the path
        public async Task<List<Folder>> GetDirectoryTree(string directoryPath)
        {
            string regExpString = PathHelper.GetRegExpStringDirectoryTree(directoryPath);
            string sql = @"SELECT * FROM folders WHERE REGEXP_LIKE(FolderPath, @Pattern) ORDER BY FolderName;";
            List<Folder> folders = (List<Folder>)await _connection.QueryAsync<Folder>(sql, new { Pattern = regExpString });
            return folders;
        }

        public async Task<bool> AddCoverImage(string coverImagePath, int folderId)
        {
            int rowsEffected = 0;
            string sql = @"UPDATE folders SET CoverImagePath = @coverImagePath WHERE FolderId = @folderId";
            rowsEffected = await _connection.ExecuteAsync(sql, new { coverImagePath, folderId });
            return rowsEffected > 0 ? true : false;
        }

        public async Task<bool> MoveFolder(string folderMoveSql, string imageMoveSql)
        {
            int rowsEffectedA = 0;
            int rowsEffectedB = 0;  
            MySqlTransaction txn = await _connection.BeginTransactionAsync();
            rowsEffectedA = await _connection.ExecuteAsync(folderMoveSql, transaction: txn);
            rowsEffectedB = await _connection.ExecuteAsync(imageMoveSql, transaction: txn);
            await txn.CommitAsync();
            return rowsEffectedA > 0 && rowsEffectedB >= 0 ? true : false;
        }

        public async Task<bool> UpdateFolderTags(Folder folder, string newTag)
        {
            int rowsEffectedA = 0;
            int rowsEffectedB = 0;
            MySqlTransaction txn = await _connection.BeginTransactionAsync();
            //insert newTag if its already there IGNORE
            string sql1 = @"INSERT IGNORE INTO tags (TagName) VALUES (@newTag)";
            rowsEffectedA = await _connection.ExecuteAsync(sql1, new { newTag = newTag }, transaction: txn);
            //get newTag id
            string sql2 = @"SELECT TagId FROM tags WHERE TagName = @newTag";
            int newTagId = await _connection.QuerySingleOrDefaultAsync<int>(sql2, new { newTag }, transaction: txn);
            //insert into folder_tags_join if its already there IGNORE
            string sql3 = @"INSERT IGNORE INTO folder_tags_join (FolderId, TagId) VALUES (@folderId, @tagId)";
            rowsEffectedB = await _connection.ExecuteAsync(sql3, new { folderId = folder.FolderId, tagId = newTagId }, transaction: txn);

            await txn.CommitAsync();
            return rowsEffectedA + rowsEffectedB >= 1 ? true : false;
        }

        public async Task<bool> AddTagToAllFoldersInCurrentDirectory(List<string> folderInsertTagSqlBatches)
        {
            int rowsEffected = 0;
            foreach (string sql in folderInsertTagSqlBatches)
            {
                int rows = await _connection.ExecuteAsync(sql);
                rowsEffected += rows;
            }
            return rowsEffected > 0 ? true : false;
        }
        public async Task<bool> DeleteFolderTag(FolderTag tag)
        {
            int rowsEffected = 0;
            string sql = @"DELETE FROM folder_tags_join WHERE FolderId = @folderId AND TagId = @tagId";
            rowsEffected = await _connection.ExecuteAsync(sql, new { folderId = tag.FolderId, tagId = tag.TagId });
            return rowsEffected > 0 ? true : false;
        }

        public async Task<bool> DeleteLibrary()
        {
            int rowsEffected = 0;
            MySqlTransaction txn = await _connection.BeginTransactionAsync();
            string sql1 = @"DELETE FROM folders WHERE FolderId >= 1";
            string sql2 = @"ALTER TABLE folders AUTO_INCREMENT = 1";
            string sql3 = @"ALTER TABLE images AUTO_INCREMENT = 1";
            string sql4 = @"DELETE FROM tags WHERE TagId >= 1";
            string sql5 = @"ALTER TABLE tags AUTO_INCREMENT = 1";
            string sql6 = @"DELETE FROM folder_saved_favorites WHERE SavedId >=1";
            string sql7 = @"UPDATE saved_directory SET SavedDirectory = '', SavedFolderPage=1, SavedTotalFolderPages=1, SavedImagePage=1, SavedTotalImagePages=1, XVector=0, YVector=0 WHERE SavedDirectoryId=1";
            string sql8 = @"DELETE FROM image_dates";
            string sql9 = @"UPDATE settings SET ExternalImageViewerExePath = null, FileExplorerExePath = null WHERE SettingsId = 1";

            rowsEffected = await _connection.ExecuteAsync(sql1, transaction: txn);
            await _connection.ExecuteAsync(sql2, transaction: txn);
            await _connection.ExecuteAsync(sql3, transaction: txn);
            await _connection.ExecuteAsync(sql4, transaction: txn);
            await _connection.ExecuteAsync(sql5, transaction: txn);
            await _connection.ExecuteAsync(sql6, transaction: txn);
            await _connection.ExecuteAsync(sql7, transaction: txn);
            await _connection.ExecuteAsync(sql8, transaction: txn);
            await _connection.ExecuteAsync(sql9, transaction: txn);

            await txn.CommitAsync();
            return rowsEffected > 0 ? true : false;
        }

        public async Task<bool> RemoveTagOnAllFolders(Tag selectedTag)
        {
            int rowsEffectedA = 0;
            int rowsEffectedB = 0;
            MySqlTransaction txn = await _connection.BeginTransactionAsync();

            //remove from folder_tags_join
            string removeFolderTagsJoin = @"DELETE FROM folder_tags_join WHERE TagId = @tagId";
            rowsEffectedA = await _connection.ExecuteAsync(removeFolderTagsJoin, new { tagId = selectedTag.TagId }, transaction: txn);

            //check for tag in image_tags_join
            string tagInImageTagsJoin = @"SELECT COUNT(*) FROM image_tags_join WHERE TagId = @tagId";
            int numTagInImageTagsJoin = await _connection.QuerySingleAsync<int>(tagInImageTagsJoin, new { tagId = selectedTag.TagId }, transaction: txn);

            //if tag not present in image_tags_join
            //  --remove from tags table
            if (numTagInImageTagsJoin == 0)
            {
                string removeFromTagsTable = @"DELETE FROM tags WHERE TagId = @tagId";
                rowsEffectedB = await _connection.ExecuteAsync(removeFromTagsTable, new { tagId = selectedTag.TagId }, transaction: txn);
            }
            await txn.CommitAsync();
            return rowsEffectedA + rowsEffectedB >= 1 ? true : false;
        }

        public async Task SaveFolderToFavorites(int folderId)
        {
            string sql = @"INSERT IGNORE INTO folder_saved_favorites (FolderId) VALUES (@folderId)";
            await _connection.ExecuteAsync(sql, new { folderId });
        }

        public async Task DeleteAllFavoriteFolders()
        {
            string sql = @"DELETE FROM folder_saved_favorites";
            await _connection.ExecuteAsync(sql);
        }
    }
}
