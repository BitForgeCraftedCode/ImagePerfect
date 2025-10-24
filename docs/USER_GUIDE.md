[← Back to Docs Index](README.md)

<a id="user-guide"></a>
## 🔍 User Guide

The best way to get started is to run the app and explore. But here’s a guided overview of the core features.

### 📂 Importing the Library Structure

**To start building your library:**

- **File ➡️ Pick Library Folder** 
- Select the root folder that contains all your image folders
- Image Perfect will scan the folder structure (but not import images yet)
	+ If you have a large library this will take some time to import your folder structure
	
> 💡 **Tip**: Image Perfect works best when your photo collection is already organized into meaningful folders. Avoid dumping thousands of images into a single folder.
	

### 🗑️ Deleting the Library

- **File ➡️ Delete Library**

This removes the MySQL database but **does not delete your image files** from the file system.
	
### ➕ Adding New Folders 

You can add additional folders to your library after initial import.

- **File ➡️ Add New Folders**
- Select one or more new folders from the file system 
- The app will skip any folder that has already been imported

> 📌 **Note Known issue**: folders imported containing only ZIP files cannot be opened. So ensure your new folders contain only images jpg, png, gif etc.
	
### 🖼️ Importing Images & Scanning Metadata

**Importing images** loads file paths into the MySQL database.  
**Scanning metadata** reads tags and ratings from image files (e.g., written by Shotwell) and stores them in the database.

> 📌 **Note**: Metadata is *not* scanned and images are *not* imported during the initial library import for speed.

#### 📂 Per-Folder Import/Scan

- Click **Import Images** button on a specific folder to load all image paths into the database
- Click **Scan Images for Metadata** button on a specific folder to extract tags/ratings from image files.

> 📌 **Note**: You have to **Import Images** first before the scan metadata button appears.

#### 📦 Bulk Import & Scan

- **File ➡️ Import and Scan** to open the bulk toolbar
- Use these buttons on the current page of folders:

| Button                                 | Description                        |
|----------------------------------------|------------------------------------|
| **Import All Folders On Current Page** | Quickly imports all images for folders on the page |
| **Scan All Folders On Current Page**   | Scans all images for metadata (Takes longer) |
| **Add Cover Image On Current Page**    | Picks a random image as a folder cover (must import images first) |

> ⏱️ **Performance Tip**:  
> Scanning metadata can be time-consuming on large pages. Use a folder pagination size of 40–60 for best balance.  
> A page size of 100 folders may take 10–30 minutes depending on number of images, image resolution, and computer specs.

#### 🔍 Filter-Based Bulk Actions

- **File ➡️ Filters ➡️ Get Folders With Images Not Imported**
- **File ➡️ Filters ➡️ Get Folders With Metadata Not Scanned**

Optionally check **"Filter in Current Directory"**, then run the corresponding import/scan button from the **Import and Scan** toolbar.

### 🧭 Navigation 

Image Perfect was designed to mirror the file system so navigation will mostly be intuitive. However, there are a lot of buttons within the app and the pagination feature adds some complexity so getting used to navigating may take a bit. Navigation is done with a combination of on folder and on image buttons as well as two, always visible, toolbars.

#### Hot Keys

- Ctrl + L (Loads the saved directory)
- Ctrl + B (Goes back one directory)
- Ctrl + R (Reloads the current directory)
- Ctrl + O (Open the directory in OS file explorer)
- Ctrl + S (Saves the current directory)
- Up Arrow will scroll up
- Down Arrow Will scroll down
- Right Arrow will go to Next Page 
- Left Arrow will go the Previous Page
	
#### Top Directory Navigation Toolbar

> 📌 **Note**: This is the directory navigation toolbar. It aids in directory navigation.

> 📌 **Note**: To open a directory in app you have to click the **Open** button located on each folder.

| Button                     | Description                                    |
|----------------------------|------------------------------------------------|
| **Open Current Directory** | Opens the current directory in your file system |
| **Save Directory**         | Saves the current directory and page number for quick navigation back to this location |
| **Load Saved Directory**   | Opens the user selected saved directory |
| **Back Directory**         | Goes back one directory |

#### Bottom Pagination Navigation Toolbar

> 📌 **Note**: This is the pagination navigation toolbar. It aids in loading the *Next* or *Previous* page of images and folders within the current directory.

| Button             | Description                                            |
|--------------------|--------------------------------------------------------|
| **Previous Page**  | Loads the previous page of images or folders within the current directory |
| **Next Page**      | Loads the next page of images or folders within the current directory |
| **Go To Page**     | Loads the user selected page of images or folders within the current directory |
| **Load Favorites** | Loads your favorite folders | 

#### 📂 Folder Navigation Buttons

On each folder there is an **Open** and **Back** button. **Open** opens that folder/directory. **Back** goes back one folder/directory. 

#### 🖼️ Image Navigation Buttons

On each image there is a **Back** button. **Back** goes back one folder/directory.

> 📌 **Note**: The **Open** button on each image is to open the image with an external image viewer. On Windows PC this requires you to  install [nomacs](https://nomacs.org/). On Ubuntu PC this feature will use the default [eog](https://manpages.ubuntu.com/manpages/trusty/man1/eog.1.html) image viewer.

> 📌 **Note**: You could also click **Back Directory** on the top toolbar

### 📂 Create a New Folder

- **File ➡️ Create New Folder** to open the new folder toolbar
- Type the name of the desired new folder and click **Create Folder** button

> 📌 **Note**: You can only create new folders once you navigate into your library. You cannot create a new folder at the root location. This feature is useful if you want to move some images from one folder to a new one within the app.

### 🔍 Filters

- **File ➡️ Filters** to open the filters toolbar

| Button | Description |
|--------|-------------|
| **Filter Images On Rating**| Gets all images at the selected rating. 1-5 star ⭐ | 
| **Filter Folders On Rating** | Gets all folders at the selected rating. 1-10 star ⭐ |
| **Filter Images On Tags** | Gets all the images with the selected tag. |
| **Filter Folders On Tags** | Gets all the folders with the selected tag. |
| **Search Folder Description** | Gets all folders that match the search term. This will search the Folder Name, Folder Description, and Folder Path in the database. |
| **Load Current Directory** | Loads the current directory. Useful if filter does not return any results. |
| **Filter in Current Directory** | Check the box to apply the filter only in the current directory. Unchecked will apply the filter to the entire library. |
| **Get Folders With Images Not Imported** | Gets all folders where images are not yet imported. |
| **Get Folders With Metadata Not Scanned** | Gets all folders where images are imported but metadata is not yet scanned |
| **Get Folders Without Covers** | Gets all folders where images are imported but covers are not yet selected. |

> 📌 **Note**: For tag filters only one tag can be selected. Start typing in the box, and a dropdown will appear with matching tags to select the desired one.

### 🗑️ Clear Favorite Folders

On each folder there is a **Favorite** button. Clicking that will add that folder to a favorite list in the database. There is also a button on the bottom toolbar called **Load Favorite Folders** that button will load all your favorite folders on the screen. To clear your favorites list:

- **File ➡️ Clear Favorite Folders**

> 📌 **Note**: This will just remove that list from the database but the folders will remain in the file system. Also there is no pop up confirm when you click **Clear Favorite Folders** so clicking accidentally will clear them. 

### 🚚 Moving and 🗑️ Deleting Images

- **File ➡️ Manage Images** to open the manage images toolbar
- Open a folder containing images

#### 🚚 Moving Images

- Click **Select Move To Folder** button and choose the folder you want to move images to. 
- Select/Check the images you want moved. (each image has a checkbox)	
- Click **Move Selected** to move the selected images to the desired folder.

#### 🗑️ Deleting Images

There is a **Trash** button on each image. Click that to delete a single image.

- Select/Check the images you want trashed. (each image has a checkbox)
- Click **Trash Selected** to trash the selected images.

> 📌 **Note**: Trashing images and folders just moves them to a folder called "ImagePerfectTRASH". This folder will be created by the app and placed inside your root library folder. 

> 📌 **Note**: There is also a **Select All** button that will select and deselect all the images on the page.

### Total 🖼️ Image Count

To get the total number of images currently imported in your library.

- **File ➡️ Total Images**

### Show All Tags 🏷️

To view a list of all the tags currently in use; either on images or folders.

- **File ➡️ Show All Tags**

### ⚙️ Settings

- **File ➡️ Settings** to open the settings toolbar

| Radio Button | Description |
|--------|-------------|
| **Pick Image Width**| Select the radio buttons to adjust the desired image and folder width. From 300px to 600px |
| **Pick Folder Pagination Size** | Select the radio buttons to adjust the number of folders that appear on each page. From 20 - 100 folders |
| **Pick Image Pagination Size** | Select the radio buttons the adjust the number of images that appear on each page. From 20 - 200 images 