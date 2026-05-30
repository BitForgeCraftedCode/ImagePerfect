CREATE DATABASE  IF NOT EXISTS `imageperfect` /*!40100 DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci */ /*!80016 DEFAULT ENCRYPTION='N' */;
USE `imageperfect`;
-- MySQL dump 10.13  Distrib 8.0.42, for Win64 (x86_64)
--
-- Host: localhost    Database: imageperfect
-- ------------------------------------------------------
-- Server version	8.0.42

/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET @OLD_CHARACTER_SET_RESULTS=@@CHARACTER_SET_RESULTS */;
/*!40101 SET @OLD_COLLATION_CONNECTION=@@COLLATION_CONNECTION */;
/*!50503 SET NAMES utf8 */;
/*!40103 SET @OLD_TIME_ZONE=@@TIME_ZONE */;
/*!40103 SET TIME_ZONE='+00:00' */;
/*!40014 SET @OLD_UNIQUE_CHECKS=@@UNIQUE_CHECKS, UNIQUE_CHECKS=0 */;
/*!40014 SET @OLD_FOREIGN_KEY_CHECKS=@@FOREIGN_KEY_CHECKS, FOREIGN_KEY_CHECKS=0 */;
/*!40101 SET @OLD_SQL_MODE=@@SQL_MODE, SQL_MODE='NO_AUTO_VALUE_ON_ZERO' */;
/*!40111 SET @OLD_SQL_NOTES=@@SQL_NOTES, SQL_NOTES=0 */;

--
-- Table structure for table `folder_saved_favorites`
--

DROP TABLE IF EXISTS `folder_saved_favorites`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `folder_saved_favorites` (
  `SavedId` bigint unsigned NOT NULL AUTO_INCREMENT,
  `FolderId` bigint unsigned DEFAULT NULL,
  PRIMARY KEY (`SavedId`),
  UNIQUE KEY `folderid_uq` (`FolderId`)
) ENGINE=InnoDB AUTO_INCREMENT=85 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `folder_tags_join`
--

DROP TABLE IF EXISTS `folder_tags_join`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `folder_tags_join` (
  `FolderId` bigint unsigned NOT NULL,
  `TagId` bigint unsigned NOT NULL,
  PRIMARY KEY (`FolderId`,`TagId`),
  KEY `folder_tags_join_idfk_2` (`TagId`),
  CONSTRAINT `folder_tags_join_idfk_1` FOREIGN KEY (`FolderId`) REFERENCES `folders` (`FolderId`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `folder_tags_join_idfk_2` FOREIGN KEY (`TagId`) REFERENCES `tags` (`TagId`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `folders`
--

DROP TABLE IF EXISTS `folders`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
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
) ENGINE=InnoDB AUTO_INCREMENT=65 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `image_dates`
--

DROP TABLE IF EXISTS `image_dates`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
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
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `image_tags_join`
--

DROP TABLE IF EXISTS `image_tags_join`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `image_tags_join` (
  `ImageId` bigint unsigned NOT NULL,
  `TagId` bigint unsigned NOT NULL,
  PRIMARY KEY (`ImageId`,`TagId`),
  KEY `image_tags_join_ibfk_2` (`TagId`),
  CONSTRAINT `image_tags_join_ibfk_1` FOREIGN KEY (`ImageId`) REFERENCES `images` (`ImageId`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `image_tags_join_ibfk_2` FOREIGN KEY (`TagId`) REFERENCES `tags` (`TagId`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `images`
--

DROP TABLE IF EXISTS `images`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
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
) ENGINE=InnoDB AUTO_INCREMENT=5490 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `saved_directory`
--

DROP TABLE IF EXISTS `saved_directory`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
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
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `settings`
--

DROP TABLE IF EXISTS `settings`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `settings` (
  `SettingsId` enum('1') NOT NULL,
  `MaxImageWidth` int unsigned NOT NULL,
  `FolderPageSize` int unsigned NOT NULL,
  `ImagePageSize` int unsigned NOT NULL,
  `ExternalImageViewerExePath` varchar(2000) DEFAULT NULL,
  `FileExplorerExePath` varchar(2000) DEFAULT NULL,
  `HistoryPointsSize` int unsigned NOT NULL,
  PRIMARY KEY (`SettingsId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `tags`
--

DROP TABLE IF EXISTS `tags`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `tags` (
  `TagId` bigint unsigned NOT NULL AUTO_INCREMENT,
  `TagName` varchar(100) NOT NULL,
  PRIMARY KEY (`TagId`),
  UNIQUE KEY `tags_uq` (`TagName`)
) ENGINE=InnoDB AUTO_INCREMENT=109 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;
/*!40103 SET TIME_ZONE=@OLD_TIME_ZONE */;

/*!40101 SET SQL_MODE=@OLD_SQL_MODE */;
/*!40014 SET FOREIGN_KEY_CHECKS=@OLD_FOREIGN_KEY_CHECKS */;
/*!40014 SET UNIQUE_CHECKS=@OLD_UNIQUE_CHECKS */;
/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40101 SET CHARACTER_SET_RESULTS=@OLD_CHARACTER_SET_RESULTS */;
/*!40101 SET COLLATION_CONNECTION=@OLD_COLLATION_CONNECTION */;
/*!40111 SET SQL_NOTES=@OLD_SQL_NOTES */;

-- Dump completed on 2026-05-29 13:42:16
