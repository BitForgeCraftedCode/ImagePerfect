using Avalonia.Controls;
using ImagePerfect.Helpers;
using ImagePerfect.Models;
using ImagePerfect.ObjectMappers;
using ImagePerfect.Repository;
using Microsoft.Extensions.Configuration;
using MsBox.Avalonia;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Models;
using MySqlConnector;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Image = ImagePerfect.Models.Image;

namespace ImagePerfect.ViewModels
{
	public class ModifyImageDataViewModel : ViewModelBase
	{
        private readonly MySqlDataSource _dataSource;
        private readonly IConfiguration _configuration;
        private readonly MainWindowViewModel _mainWindowViewModel;
        private string _textForEditTagOnAllImages = string.Empty;
        public ModifyImageDataViewModel(MySqlDataSource dataSource, IConfiguration config, MainWindowViewModel mainWindowViewModel) 
		{
            _dataSource = dataSource;
            _configuration = config;
            _mainWindowViewModel = mainWindowViewModel;
        }

        public string TextForEditTagOnAllImages
        {
            get => _textForEditTagOnAllImages;
            set => this.RaiseAndSetIfChanged(ref _textForEditTagOnAllImages, value);
        }
        //update image sql and metadata only. 
        public async Task UpdateImage(ImageViewModel imageVm, string fieldUpdated)
        {
            await using UnitOfWork uow = await UnitOfWork.CreateAsync(_dataSource, _configuration);
            ImageMethods imageMethods = new ImageMethods(uow);

            Image image = ImageMapper.GetImageFromVm(imageVm);
            bool success = await imageMethods.UpdateImage(image);
            if (!success)
            {
                await MessageBoxManager.GetMessageBoxCustom(
                    new MessageBoxCustomParams
                    {
                        ButtonDefinitions = new List<ButtonDefinition>
                        {
                            new ButtonDefinition { Name = "Ok", },
                        },
                        ContentTitle = $"Add {fieldUpdated}",
                        ContentMessage = $"Image {fieldUpdated} update error. Try again.",
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        SizeToContent = SizeToContent.WidthAndHeight,  // <-- lets it grow with content
                        MinWidth = 500  // optional, so it doesn’t wrap too soon
                    }
                ).ShowWindowDialogAsync(Globals.MainWindow);
                return;
            }
            //write rating to image metadata
            if (fieldUpdated == "Rating")
            {
                await ImageMetaDataHelper.AddRatingToImage(image);
            }
        }

        //remove the tag from the image_tag_join table 
        //Also need to remove imageMetaData
        public async Task EditImageTag(ImageViewModel imageVm)
        {
            await using UnitOfWork uow = await UnitOfWork.CreateAsync(_dataSource, _configuration);
            ImageMethods imageMethods = new ImageMethods(uow);

            if (imageVm.ImageTags == null || imageVm.ImageTags == "")
            {
                if (imageVm.Tags.Count == 1)
                {
                    await imageMethods.DeleteImageTag(imageVm.Tags[0]);
                    //remove tag from image metadata
                    await ImageMetaDataHelper.WriteTagToImage(imageVm);
                }
                else if (imageVm.Tags.Count == 0)
                {
                    return;
                }
            }
            List<string> imageTags = imageVm.ImageTags.Split(",").ToList();
            ImageTag tagToRemove = null;
            foreach (ImageTag tag in imageVm.Tags)
            {
                if (!imageTags.Contains(tag.TagName))
                {
                    tagToRemove = tag;
                }
            }
            if (tagToRemove != null)
            {
                await imageMethods.DeleteImageTag(tagToRemove);
                //remove tag from image metadata
                await ImageMetaDataHelper.WriteTagToImage(imageVm);
            }
        }

        //update ImageTags in db, and update image metadata
        public async Task AddImageTag(ImageViewModel imageVm)
        {
            //click submit with empty input just return
            if (imageVm.NewTag == "" || imageVm.NewTag == null)
            {
                return;
            }
            //add NewTag to ImageTags -- KEEP!! THIS IS NEEDED TO WRITE METADATA
            if (string.IsNullOrEmpty(imageVm.ImageTags))
            {
                imageVm.ImageTags = imageVm.NewTag;
            }
            else
            {
                imageVm.ImageTags = imageVm.ImageTags + "," + imageVm.NewTag;
            }
            await using UnitOfWork uow = await UnitOfWork.CreateAsync(_dataSource, _configuration);
            ImageMethods imageMethods = new ImageMethods(uow);

            Image image = ImageMapper.GetImageFromVm(imageVm);
            //update image table and tags table in db -- success will be false if you try to input a duplicate tag
            bool success = await imageMethods.UpdateImageTags(image, imageVm.NewTag);
            if (success)
            {
                //write new tag to image metadata
                await ImageMetaDataHelper.WriteTagToImage(imageVm);
                //Update TagsList to show in UI AutoCompleteBox clear NewTag in box as well
                await _mainWindowViewModel.GetTagsList(uow);
                imageVm.NewTag = "";
            }
            else
            {
                //remove the NewTag from the Tags list in the UI (New tag was duplicate and not added in this case)
                int tagsMaxIndex = imageVm.ImageTags.Length - 1;
                int newTagTotalCharsToRemove = imageVm.NewTag.Length; //total chars to remove
                int removeStartAtIndex = tagsMaxIndex - newTagTotalCharsToRemove;
                imageVm.ImageTags = imageVm.ImageTags.Remove(removeStartAtIndex);
                //clear NewTag in box if try to input duplicate tag
                imageVm.NewTag = "";
            }
        }

        public async Task EditTagOnAllImages(Tag selectedTag)
        {
            string newTag = TextForEditTagOnAllImages?.Trim() ?? string.Empty;
            //no tag selected or no edited text for new tag just return
            if (selectedTag == null || string.IsNullOrEmpty(newTag) || string.Equals(selectedTag.TagName, newTag, StringComparison.Ordinal))
                return;

            var boxYesNo = MessageBoxManager.GetMessageBoxCustom(
                new MessageBoxCustomParams
                {
                    ButtonDefinitions = new List<ButtonDefinition>
                        {
                            new ButtonDefinition { Name = "Yes", },
                            new ButtonDefinition { Name = "No", },
                        },
                    ContentTitle = "Edit Tag",
                    ContentMessage = $"CAUTION you are about to edit tag {selectedTag.TagName} to {newTag} this could take a long time are you sure?",
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    SizeToContent = SizeToContent.WidthAndHeight,  // <-- lets it grow with content
                    MinWidth = 500  // optional, so it doesn’t wrap too soon
                }
            );
            var boxResult = await boxYesNo.ShowWindowDialogAsync(Globals.MainWindow);
            if (boxResult != "Yes")
                return;

            await using UnitOfWork uow = await UnitOfWork.CreateAsync(_dataSource, _configuration);
            ImageMethods imageMethods = new ImageMethods(uow);

            _mainWindowViewModel.ShowLoading = true;
            try
            {
                //select all images from db with tag get as List<Image>
                (List<Image> images, List<ImageTag> tags) imageTagResult = await imageMethods.GetAllImagesWithTags(new List<string> { selectedTag.TagName }, false, _mainWindowViewModel.ExplorerVm.CurrentDirectory);
                List<Image> taggedImages = imageTagResult.images;
                //no taggedImages returned just exit
                if (taggedImages == null || taggedImages.Count == 0)
                    return;


                //pass those images to method that edits the tag on physical image metadata
                bool success = await ImageMetaDataHelper.EditTagOnAllImages(taggedImages, selectedTag, newTag);
                //if thats a success edit from data base
                if (success)
                {
                    bool dbSuccess = await imageMethods.EditTagOnAllImages(selectedTag, newTag);
                    if (dbSuccess)
                    {
                        TextForEditTagOnAllImages = string.Empty;
                        //Update TagsList to show in UI
                        await _mainWindowViewModel.GetTagsList(uow);
                    }
                }
            }
            finally
            {
                _mainWindowViewModel.ShowLoading = false;
            }
        }
        public async Task RemoveTagOnAllImages(Tag selectedTag)
        {
            //nothing selected just return
            if (selectedTag == null)
                return;
            var boxYesNo = MessageBoxManager.GetMessageBoxCustom(
                new MessageBoxCustomParams
                {
                    ButtonDefinitions = new List<ButtonDefinition>
                        {
                            new ButtonDefinition { Name = "Yes", },
                            new ButtonDefinition { Name = "No", },
                        },
                    ContentTitle = "Remove Tag",
                    ContentMessage = $"CAUTION you are about to remove a tag this could take a long time are you sure?",
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    SizeToContent = SizeToContent.WidthAndHeight,  // <-- lets it grow with content
                    MinWidth = 500  // optional, so it doesn’t wrap too soon
                }
            );
            var boxResult = await boxYesNo.ShowWindowDialogAsync(Globals.MainWindow);
            if (boxResult != "Yes")
                return;

            await using UnitOfWork uow = await UnitOfWork.CreateAsync(_dataSource, _configuration);
            ImageMethods imageMethods = new ImageMethods(uow);

            _mainWindowViewModel.ShowLoading = true;
            try
            {
                //select all images from db with tag get as List<Image>
                (List<Image> images, List<ImageTag> tags) imageTagResult = await imageMethods.GetAllImagesWithTags(new List<string> { selectedTag.TagName }, false, _mainWindowViewModel.ExplorerVm.CurrentDirectory);
                List<Image> taggedImages = imageTagResult.images;
                //no taggedImages returned just exit
                if (taggedImages == null || taggedImages.Count == 0)
                    return;
                        

                //pass those images to method that removes the tag from physical image metadata
                bool success = await ImageMetaDataHelper.RemoveTagFromAllImages(taggedImages, selectedTag);
                //if thats a success remove from data base
                if (success)
                {
                    await imageMethods.RemoveTagOnAllImages(selectedTag);
                    //Update TagsList to show in UI
                    await _mainWindowViewModel.GetTagsList(uow);
                }
            }
            finally 
            {
                _mainWindowViewModel.ShowLoading = false;
            }
        }
        public async Task AddMultipleImageTags(ListBox selectedTagsListBox)
        {
            if (selectedTagsListBox.DataContext != null && selectedTagsListBox.SelectedItems != null)
            {
                ImageViewModel imageVm = (ImageViewModel)selectedTagsListBox.DataContext;
                List<Tag> tagsToAdd = new List<Tag>();
                //nothing selected just return
                if (selectedTagsListBox.SelectedItems.Count == 0)
                {
                    return;
                }
                //if no current tags just add all to list
                if (imageVm.ImageTags == "" || imageVm.ImageTags == null)
                {
                    foreach (Tag selectedTag in selectedTagsListBox.SelectedItems)
                    {
                        tagsToAdd.Add(selectedTag);
                    }
                }
                //else only add non duplicates
                else
                {
                    foreach (Tag selectedTag in selectedTagsListBox.SelectedItems)
                    {
                        if (!imageVm.ImageTags.Contains(selectedTag.TagName))
                        {
                            tagsToAdd.Add(selectedTag);
                        }
                    }
                }
                //add new tags to ImageTags -- KEEP!! THIS IS NEEDED TO WRITE METADATA
                foreach (Tag selectedTag in tagsToAdd)
                {
                    if (string.IsNullOrEmpty(imageVm.ImageTags))
                    {
                        imageVm.ImageTags = selectedTag.TagName;
                    }
                    else
                    {
                        imageVm.ImageTags = imageVm.ImageTags + "," + selectedTag.TagName;
                    }
                }

                await using UnitOfWork uow = await UnitOfWork.CreateAsync(_dataSource, _configuration);
                ImageMethods imageMethods = new ImageMethods(uow);

                //build sql for bulk insert
                string sql = SqlStringBuilder.BuildSqlForAddMultipleImageTags(tagsToAdd, imageVm);
                //update sql db
                bool success = await imageMethods.AddMultipleImageTags(sql);
                //write new tags to image file
                if (success)
                {
                    //write new tags to image metadata
                    await ImageMetaDataHelper.WriteTagToImage(imageVm);
                }
                else
                {
                    List<string> imageTags = imageVm.ImageTags.Split(",").ToList();
                    //if fail remove the tags from the Tags list in the UI
                    foreach (Tag tag in tagsToAdd)
                    {
                        imageTags.Remove(tag.TagName);
                    }
                    for (int i = 0; i < imageTags.Count; i++)
                    {
                        if (i == 0)
                        {
                            imageVm.ImageTags = imageTags[i];
                        }
                        else
                        {
                            imageVm.ImageTags = imageVm.ImageTags + "," + imageTags[i];

                        }
                    }
                }
            }
        }

        private static async Task ShowRenameImageMessage(string message)
        {
            await MessageBoxManager.GetMessageBoxCustom(
                new MessageBoxCustomParams
                {
                    ButtonDefinitions = new List<ButtonDefinition>
                    {
                        new ButtonDefinition { Name = "Ok", },
                    },
                    ContentTitle = "Rename Image",
                    ContentMessage = message,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    SizeToContent = SizeToContent.WidthAndHeight,
                    MinWidth = 500
                }
            ).ShowWindowDialogAsync(Globals.MainWindow);
        }

        public async Task RenameImage(ImageViewModel imageVm)
        {
            if (imageVm == null)
            {
                return;
            }

            await using UnitOfWork uow = await UnitOfWork.CreateAsync(_dataSource, _configuration);
            ImageMethods imageMethods = new ImageMethods(uow);
            Image currentImage = await imageMethods.GetImageById(imageVm.ImageId);

            string oldPath = currentImage.ImagePath;
            string imageFolderPath = currentImage.ImageFolderPath;
            string oldFileName = currentImage.FileName;
            string oldExtension = Path.GetExtension(oldFileName);
            string newImageName = imageVm.FileName?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(newImageName))
            {
                imageVm.FileName = oldFileName;
                await ShowRenameImageMessage("Image name cannot be blank.");
                return;
            }

            if (newImageName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                imageVm.FileName = oldFileName;
                await ShowRenameImageMessage("Image name contains invalid file name characters.");
                return;
            }

            string newExtension = Path.GetExtension(newImageName);
            if (!string.Equals(oldExtension, newExtension, StringComparison.OrdinalIgnoreCase))
            {
                imageVm.FileName = oldFileName;
                await ShowRenameImageMessage($"Image extension cannot be changed. Keep the original '{oldExtension}' extension.");
                return;
            }

            if (!File.Exists(oldPath))
            {
                imageVm.FileName = oldFileName;
                await ShowRenameImageMessage("The image could not be found on disk.");
                return;
            }
            //new name must be different
            if (string.Equals(oldFileName, newImageName, StringComparison.Ordinal))
            {
                imageVm.FileName = oldFileName;
                return;
            }
            //Build new path and verify it does not already exist
            string newPath = Path.Combine(imageFolderPath, newImageName);
            if (File.Exists(newPath))
            {
                imageVm.FileName = oldFileName;
                await ShowRenameImageMessage("An image with that name already exists in this location.");
                return;
            }
            //Try to rename/move physical image
            _mainWindowViewModel.ShowLoading = true;
            bool physicalImageMoved = false;
            try
            {
                File.Move(oldPath, newPath);
                physicalImageMoved = true;
                //once physical image is renamed. rename the data base paths
                bool success = await imageMethods.RenameImage(imageVm.ImageId, oldPath, newPath, newImageName);
                if (!success)
                {
                    //if data base rename fails move back physical image
                    if (File.Exists(newPath) && !File.Exists(oldPath))
                    {
                        File.Move(newPath, oldPath);
                    }

                    imageVm.FileName = oldFileName;
                    await ShowRenameImageMessage("Image rename database update failed. The image was restored to its original name.");
                    return;
                }
                //I think no refresh is better even though you will not be able to open the image with external viewer until refresh. 
                //if you want to rename a bunch of images refresh after each one is not good UI. Its a trade off and this is a choice. 
                //await _mainWindowViewModel.ExplorerVm.RefreshImages(imageFolderPath, 0, uow);
            }
            catch (Exception e)
            {
                //fallback
                if (physicalImageMoved && File.Exists(newPath) && !File.Exists(oldPath))
                {
                    File.Move(newPath, oldPath);
                }

                imageVm.FileName = oldFileName;
                await ShowRenameImageMessage($"Sorry something went wrong.\n{e.Message}");
            }
            finally
            {
                _mainWindowViewModel.ShowLoading = false;
            }
        }

    }
}