[← Back to Docs Index](README.md)

<a id="create-database-commands"></a>

```sql
-- Create database
CREATE DATABASE imageperfect;
USE imageperfect;

-- Folders table
CREATE TABLE `folders` (
  `FolderId` bigint unsigned NOT NULL AUTO_INCREMENT,
  `FolderName` varchar(200) NOT NULL,
  `FolderPath` varchar(2000) NOT NULL,
  `HasChildren` tinyint(1) DEFAULT NULL,
  `CoverImagePath` varchar(2000) DEFAULT NULL,
  `FolderDescription` varchar(3000) DEFAULT NULL,
  `FolderRating` tinyint unsigned DEFAULT NULL,
  `HasFiles` tinyint(1) DEFAULT NULL,
  `IsRoot` tinyint(1) DEFAULT NULL,
  `FolderContentMetaDataScanned` tinyint(1) DEFAULT NULL,
  `AreImagesImported` tinyint(1) DEFAULT NULL,
  PRIMARY KEY (`FolderId`),
  FULLTEXT KEY `fulltext` (`FolderName`,`FolderPath`,`FolderDescription`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- Images table
CREATE TABLE `images` (
  `ImageId` bigint unsigned NOT NULL AUTO_INCREMENT,
  `ImagePath` varchar(2000) NOT NULL,
  `FileName` varchar(500) NOT NULL,
  `ImageRating` tinyint unsigned DEFAULT NULL,
  `ImageFolderPath` varchar(2000) NOT NULL,
  `ImageMetaDataScanned` tinyint(1) DEFAULT NULL,
  `FolderId` bigint unsigned DEFAULT NULL,
  `DateTaken` date DEFAULT NULL,
  `DateTakenYear` smallint GENERATED ALWAYS AS (year(`DateTaken`)) STORED,
  `DateTakenMonth` tinyint GENERATED ALWAYS AS (month(`DateTaken`)) STORED,
  `DateTakenDay` tinyint GENERATED ALWAYS AS (dayofmonth(`DateTaken`)) STORED,
  PRIMARY KEY (`ImageId`),
  KEY `FolderId` (`FolderId`),
  KEY `idx_date_parts` (`DateTakenYear`,`DateTakenMonth`,`DateTakenDay`),
  FULLTEXT KEY `fulltext` (`ImagePath`),
  CONSTRAINT `images_ibfk_1` FOREIGN KEY (`FolderId`) REFERENCES `folders` (`FolderId`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- Image dates table
CREATE TABLE `image_dates` (
  `DateTaken` date NOT NULL,
  `Year` smallint NOT NULL,
  `Month` tinyint NOT NULL,
  `Day` tinyint NOT NULL,
  `YearMonth` char(7) GENERATED ALWAYS AS (concat(`Year`,_utf8mb4'-',lpad(`Month`,2,_utf8mb4'0'))) STORED,
  PRIMARY KEY (`DateTaken`),
  KEY `idx_year` (`Year`),
  KEY `idx_month` (`Month`),
  KEY `idx_year_month` (`YearMonth`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- Tags table
CREATE TABLE `tags` (
  `TagId` bigint unsigned NOT NULL AUTO_INCREMENT,
  `TagName` varchar(100) NOT NULL,
  PRIMARY KEY (`TagId`),
  UNIQUE KEY `tags_uq` (`TagName`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- Folder-Tags join table
CREATE TABLE `folder_tags_join` (
  `FolderId` bigint unsigned NOT NULL,
  `TagId` bigint unsigned NOT NULL,
  PRIMARY KEY (`FolderId`,`TagId`),
  KEY `folder_tags_join_idfk_2` (`TagId`),
  CONSTRAINT `folder_tags_join_idfk_1` FOREIGN KEY (`FolderId`) REFERENCES `folders` (`FolderId`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `folder_tags_join_idfk_2` FOREIGN KEY (`TagId`) REFERENCES `tags` (`TagId`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- Image-Tags join table
CREATE TABLE `image_tags_join` (
  `ImageId` bigint unsigned NOT NULL,
  `TagId` bigint unsigned NOT NULL,
  PRIMARY KEY (`ImageId`,`TagId`),
  KEY `image_tags_join_ibfk_2` (`TagId`),
  CONSTRAINT `image_tags_join_ibfk_1` FOREIGN KEY (`ImageId`) REFERENCES `images` (`ImageId`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `image_tags_join_ibfk_2` FOREIGN KEY (`TagId`) REFERENCES `tags` (`TagId`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- Settings table
CREATE TABLE `settings` (
  `SettingsId` enum('1') NOT NULL,
  `MaxImageWidth` int unsigned NOT NULL,
  `FolderPageSize` int unsigned NOT NULL,
  `ImagePageSize` int unsigned NOT NULL,
  `ExternalImageViewerExePath` varchar(2000) DEFAULT NULL,
  `FileExplorerExePath` varchar(2000) DEFAULT NULL,
  PRIMARY KEY (`SettingsId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

INSERT INTO settings (MaxImageWidth, FolderPageSize, ImagePageSize) VALUES (500, 20, 60); 

-- Folder Saved Favorites
CREATE TABLE `folder_saved_favorites` (
  `SavedId` bigint unsigned NOT NULL AUTO_INCREMENT,
  `FolderId` bigint unsigned DEFAULT NULL,
  PRIMARY KEY (`SavedId`),
  UNIQUE KEY `folderid_uq` (`FolderId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

-- Saved Directory Table
CREATE TABLE `saved_directory` (
  `SavedDirectoryId` enum('1') NOT NULL,
  `SavedDirectory` varchar(2000) NOT NULL,
  `SavedFolderPage` int unsigned NOT NULL,
  `SavedTotalFolderPages` int unsigned NOT NULL,
  `SavedImagePage` int unsigned NOT NULL,
  `SavedTotalImagePages` int unsigned NOT NULL,
  `XVector` double NOT NULL,
  `YVector` double NOT NULL,
  PRIMARY KEY (`SavedDirectoryId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

INSERT INTO saved_directory (SavedDirectory, SavedFolderPage, SavedTotalFolderPages, SavedImagePage, SavedTotalImagePages, XVector, YVector) VALUES ("",1,1,1,1,0,0);

-- Enable local file import
SET PERSIST local_infile = 1;

```

---

> 📌 **Important**: Make sure to run `SET PERSIST local_infile = 1;` or file importing won't work.

---