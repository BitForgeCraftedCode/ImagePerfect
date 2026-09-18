using CliWrap;
using ImagePerfect.Models;
using ImagePerfect.ViewModels;
using ReactiveUI.Primitives;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.Metadata.Profiles.Iptc;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ImagePerfectImage = ImagePerfect.Models.Image;
using ImageSharp = SixLabors.ImageSharp;
//https://aaronbos.dev/posts/iptc-metadata-csharp-imagesharp
namespace ImagePerfect.Helpers
{
    public static class ImageMetaDataHelper
    {
        public static async Task<List<ImagePerfectImage>> ScanImagesForMetaData(List<ImagePerfectImage> images)
        {
            await Parallel.ForEachAsync(images, 
                new ParallelOptions { MaxDegreeOfParallelism = 4 }, 
                async(img, ct) => {
                    try
                    {
                        ImageSharp.ImageInfo imageInfo = await ImageSharp.Image.IdentifyAsync(img.ImagePath);
                        UpdateMetadata(imageInfo, img);
                    }
                    catch
                    {
                        /* want to continue scaning the rest if exception -- like a corrupted image file
                         * 
                         * return vs. continue: In the context of a lambda expression within Parallel.ForEach, 
                         * return serves the same purpose as continue in a traditional foreach loop – it exits the current iteration of the lambda and moves to the next item.
                         * 
                         * The delegate (async (img, ct) => { ... }) is the entire body of what runs per item.
                         * Once you return, you’re done with that iteration, and Parallel.ForEachAsync moves on to the next image automatically.
                         */
                        return;
                    }
                    
                });
            return images;
        }

        public static async Task WriteTagToImage(ImageViewModel imageVm)
        {
            await WriteKeywordToImage(imageVm);
        }

        public static async Task AddRatingToImage(ImagePerfectImage image)
        {
            string originalPath = image.ImagePath;
            string backupPath = Path.ChangeExtension(originalPath, ".bak" + Path.GetExtension(originalPath));

            string exifToolPath = OperatingSystem.IsWindows()
                ? Path.Combine(AppContext.BaseDirectory, "ExternalTools", "ExifTool", "win-x64", "exiftool.exe")
                : Path.Combine(AppContext.BaseDirectory, "ExternalTools", "ExifTool", "linux-x64", "exiftool");

            try
            {
                // Step 1: Create a backup
                File.Copy(originalPath, backupPath, overwrite: true);

                // Step 2: Save new rating to image
                //suppressing ExifTool's informational output with -q causes the CliWrap invocation to behave correctly
                await Cli.Wrap(exifToolPath)
                    .WithArguments(args => args
                        .Add("-q") // suppress informational output not errors. dont print "1 image files updated"
                        .Add("-EXIF:Rating=" + image.ImageRating.ToString())
                        .Add("-overwrite_original")
                        .Add(originalPath))
                    .ExecuteAsync();
    
                // Step 3: If successful, delete backup
                if (File.Exists(backupPath))
                    File.Delete(backupPath);
            }
            catch
            {
                // Step 4: Restore backup if save failed
                if (File.Exists(backupPath))
                {
                    File.Copy(backupPath, originalPath, overwrite: true);
                    File.Delete(backupPath);
                }

                throw;
            }
        }

        // MaxDegreeOfParallelism = 1 set to 1 till i can confirm SharpExifTool can safely be called in parallel - set back to 4
        public static async Task<bool> EditTagOnAllImages(List<ImagePerfectImage> images, Tag selectedTag, string newTag)
        {
            List<ImagePerfectImage> sortedImages = images
               .OrderBy(img => Path.GetDirectoryName(img.ImagePath))
               .ThenBy(img => Path.GetFileName(img.ImagePath))
               .ToList();

            int anyFail = 0;
            await Parallel.ForEachAsync(sortedImages, new ParallelOptions { MaxDegreeOfParallelism = 1 }, async (img, ct) => {
                try
                {
                    ImageSharp.ImageInfo imageSharpInfo = await ImageSharp.Image.IdentifyAsync(img.ImagePath, ct);
                    bool success = await EditTag(imageSharpInfo, img, selectedTag, newTag);
                    if (!success)
                        Interlocked.Exchange(ref anyFail, 1);
                }
                catch
                {
                    Interlocked.Exchange(ref anyFail, 1);
                }
            });

            return anyFail == 0;
        }

        // MaxDegreeOfParallelism = 1 set to 1 till i can confirm SharpExifTool can safely be called in parallel - set back to 4
        public static async Task<bool> RemoveTagFromAllImages(List<ImagePerfectImage> images, Tag selectedTag)
        {
            /*
            assuming HHD with lots of data. Images with the selected tag will likely be from many different folders
            sorting by the directory and filename will group them per folder and cut down oh physical HHD head movement

            Without sorting:
            Parallel threads request files in random order, causing the HDD head to constantly jump between distant tracks. Seek times pile up.

            With sorting:
            Because files from the same directory tend to be stored physically close to each other on disk, sorting like this means:
                
                The read/write head moves sequentially within a folder's cluster.

                Only when that folder's files are done does the head jump to another folder.

                That reduces "seek time," which is what kills HDD speed.

            Consider for future commit and doing this in SQL then i can remove the C# sort
            sql: ORDER BY images.ImageFolderPath, images.FileName

            the sql ORDER BY has been added keeping below code for now

            */
            List<ImagePerfectImage> sortedImages = images
               .OrderBy(img => Path.GetDirectoryName(img.ImagePath))
               .ThenBy(img => Path.GetFileName(img.ImagePath))
               .ToList();

            int anyFail = 0;
            await Parallel.ForEachAsync(sortedImages, new ParallelOptions { MaxDegreeOfParallelism = 1 }, async (img, ct) => {
                try
                {
                    bool success = await RemoveTag(img, selectedTag);
                    if (!success)
                        Interlocked.Exchange(ref anyFail, 1);
                }
                catch
                {
                    Interlocked.Exchange(ref anyFail, 1);
                }
            });
            return anyFail == 0;
        }
        
        //adds the image metadata to the ImagePerfect Image object
        private static void UpdateMetadata(ImageSharp.ImageInfo imageInfo, ImagePerfectImage image)
        {
            //thread safe bc/ each thread will make new List
            List<ImageTag> newTags = new List<ImageTag>();
            if (imageInfo.Metadata.IptcProfile?.Values?.Any() == true)
            {
                foreach (var prop in imageInfo.Metadata.IptcProfile.Values)
                {
                    if (prop.Tag == IptcTag.Keywords)
                    {
                        ImageTag imageTag = new() 
                        { 
                            TagName = prop.Value,
                            ImageId = image.ImageId,
                        };
                        newTags.Add(imageTag);
                    }
                }
            }
            image.Tags = newTags;
            //shotwell rating is in exifprofile
            if (imageInfo.Metadata.ExifProfile?.Values?.Any() == true)
            {
                if 
                (
                    imageInfo.Metadata.ExifProfile.TryGetValue(ExifTag.DateTimeOriginal, out IExifValue<string>? dateValue) &&
                    DateTime.TryParseExact(dateValue?.Value, "yyyy:MM:dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedDate)
                )
                {
                    image.DateTaken = parsedDate;
                }
                else
                {
                    image.DateTaken = null;
                }
                foreach (var prop in imageInfo.Metadata.ExifProfile.Values)
                {
                    if (prop.Tag == ExifTag.Rating)
                    {
                        image.ImageRating = Convert.ToInt32(prop.GetValue());
                    }
                }   
            }
        }

        //clear all the current keywords, add the imagePerfect ones, save -- keeps physical file in sync with the UI
        private static async Task WriteKeywordToImage(ImageViewModel imagePerfectImage)
        {
            string originalPath = imagePerfectImage.ImagePath;
            string backupPath = Path.ChangeExtension(originalPath, ".bak" + Path.GetExtension(originalPath));

            try
            {
                // Step 1: Create a backup
                File.Copy(originalPath, backupPath, overwrite: true);

                using (var exiftool = new SharpExifTool.ExifTool())
                {
                    // Step 2: Clear all existing keywords first (assigning empty deletes the tag/all values)
                    await exiftool.WriteTagsAsync(
                        filename: originalPath,
                        properties: new Dictionary<string, string>
                        {
                            ["IPTC:Keywords"] = ""
                        },
                        overwriteOriginal: true);

                    // Step 3: Re-add each tag from the comma separated list, if any
                    if (!string.IsNullOrEmpty(imagePerfectImage.ImageTags))
                    {
                        string[] tags = imagePerfectImage.ImageTags.Split(',');
                        foreach (string tag in tags)
                        {
                            string trimmedTag = tag.Trim();
                            if (string.IsNullOrEmpty(trimmedTag))
                                continue;

                            // "+" appends to the list-type tag instead of overwriting it
                            await exiftool.WriteTagsAsync(
                                filename: originalPath,
                                properties: new Dictionary<string, string>
                                {
                                    ["IPTC:Keywords+"] = trimmedTag
                                },
                                overwriteOriginal: true);
                        }
                    }
                }

                // Step 4: If successful, delete backup
                if (File.Exists(backupPath))
                    File.Delete(backupPath);
            }
            catch
            {
                // Step 5: Restore backup if save failed
                if (File.Exists(backupPath))
                {
                    File.Copy(backupPath, originalPath, overwrite: true);
                    File.Delete(backupPath);
                }
                throw;
            }
        }

        private static async Task<bool> EditTag(ImageSharp.ImageInfo imageInfo, ImagePerfectImage imagePerfectImage, Tag selectedTag, string newTag)
        {
            if (imageInfo.Metadata.IptcProfile == null)
                imageInfo.Metadata.IptcProfile = new IptcProfile();

            string originalPath = imagePerfectImage.ImagePath;
            string backupPath = Path.ChangeExtension(originalPath, ".bak" + Path.GetExtension(originalPath));
            
            try
            {
                // Step 1: Create a backup
                File.Copy(originalPath, backupPath, overwrite: true);
                //get tags from physical image and replaces oldTag with newTag
                string oldTag = selectedTag.TagName.Trim();
                List<string> keywords = imageInfo.Metadata.IptcProfile.Values //Get all IPTC metadata values
                    .Where(v => v.Tag == IptcTag.Keywords) //Keep only values whose tag is Keywords
                    .Select(v => v.Value.Trim()) //Extract the actual string value
                    .Where(v => !string.IsNullOrWhiteSpace(v)) //Ignore empty values
                    .Select(v => string.Equals(v, oldTag, StringComparison.Ordinal) ? newTag : v) //If the keyword is the tag you're replacing, change it to newTag
                    .Distinct(StringComparer.Ordinal) //Remove duplicates
                    .ToList();
                // If newTag isn't already in the list, add it.
                // This is a fail-safe in case the DB and physical image metadata are out of sync.
                if (!keywords.Any(k => string.Equals(k, newTag, StringComparison.Ordinal)))
                    keywords.Add(newTag);

                using (var exiftool = new SharpExifTool.ExifTool())
                {
                    //Step 2: remove all from physical image
                    await exiftool.WriteTagsAsync(
                       filename: originalPath,
                       properties: new Dictionary<string, string>
                       {
                           ["IPTC:Keywords"] = ""
                       },
                       overwriteOriginal: true);

                    // Step 3: re-add to physical image
                    foreach (string keyword in keywords) 
                    {
                        // "+" appends to the list-type tag instead of overwriting it
                        await exiftool.WriteTagsAsync(
                            filename: originalPath,
                            properties: new Dictionary<string, string>
                            {
                                ["IPTC:Keywords+"] = keyword
                            },
                            overwriteOriginal: true);
                    }
                }

                // Step 3: If successful, delete backup
                if (File.Exists(backupPath))
                    File.Delete(backupPath);
                return true;
            }
            catch
            {
                // Step 4: Restore backup if save failed
                if (File.Exists(backupPath))
                {
                    File.Copy(backupPath, originalPath, overwrite: true);
                    File.Delete(backupPath);
                }
                return false;
            }
        }
        
        private static async Task<bool> RemoveTag(ImagePerfectImage imagePerfectImage, Tag selectedTag)
        {
            string originalPath = imagePerfectImage.ImagePath;
            string backupPath = Path.ChangeExtension(originalPath, ".bak" + Path.GetExtension(originalPath));

            try
            {
                // Step 1: Create a backup
                File.Copy(originalPath, backupPath, overwrite: true);

                // Step 2: Remove just this one value from the list-type tag, leaving the rest intact
                using (var exiftool = new SharpExifTool.ExifTool())
                {
                    await exiftool.WriteTagsAsync(
                        filename: originalPath,
                        properties: new Dictionary<string, string>
                        {
                            ["IPTC:Keywords-"] = selectedTag.TagName.Trim()
                        },
                        overwriteOriginal: true);
                }

                // Step 3: If successful, delete backup
                if (File.Exists(backupPath))
                    File.Delete(backupPath);
                return true;
            }
            catch
            {
                // Step 4: Restore backup if save failed
                if (File.Exists(backupPath))
                {
                    File.Copy(backupPath, originalPath, overwrite: true);
                    File.Delete(backupPath);
                }
                return false;
            }
        }  
    }
}
