# Image Perfect

![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20Ubuntu-blue)
![License](https://img.shields.io/github/license/BitForgeCraftedCode/ImagePerfect)
![.NET](https://img.shields.io/badge/.NET-8.0-blueviolet)

## 📚 Table Of Contents
- ❓ [Why I Built Image Perfect](#why-i-built-image-perfect)
- ℹ️ [About](#about)
- 👀 [Core Features](#core-features)
- 📷 [Screen Shots](docs/SCREEN_SHOTS.md)
- ⚠️ [Important Metadata Note](docs/IMPORTANT_METADATA_NOTICE.md)
- 🖥️ [System Requirements](docs/SYSTEM_REQUIREMENTS.md)
- 🚀 [Quick Start Windows](docs/QUICK_START_WINDOWS.md)
- 🚀 [Quick Start Ubuntu](docs/QUICK_START_LINUX.md)
- 🔍 [User Guide](docs/USER_GUIDE.md)
- 🧰 [Planned Improvements](docs/PLANNED_IMPROVEMENTS.md)
- 🖥️ [MySQL Server Setup Windows](docs/MYSQL_SERVER_SETUP_WINDOWS.md)
- 🖥️ [MySQL Server Setup Ubuntu](docs/MYSQL_SERVER_SETUP_LINUX.md)
- 📋 [Build And Install Directions](docs/BUILD_INSTALL_DIRECTIONS.md)
- 📊 [Backing Up And Restoring The MySQL Database](docs/BACKUP_RESTORE_MYSQL_DATABASE.md)
- 📦 [Migrating To A New Computer](#migrating-to-a-new-computer)
- 📚 [Tech Stack And Notable Dependencies](docs/TECH_STACK.md)
- 🪪 [License](#license)
- 📢 [Feedback And Contributions](docs/FEEDBACK_CONTRIBUTIONS.md)

<a id="why-i-built-image-perfect"></a>
## ❓Why I Built Image Perfect

I built **Image Perfect** as a modern, high-performance alternative to **Shotwell** on Linux.
While **Shotwell** remains a solid photo manager for smaller collections, it struggles to scale efficiently with very large libraries. **Image Perfect** was designed to overcome that limitation and focus on performance and usability.

**Image Perfect’s goals are**:

1. **Handle large libraries efficiently** — no long import times or excessive memory usage.

2. **Provide excellent folder organization** — intuitive, fast, and file-system aware.

3. **Deliver a great viewing experience** — large in-app previews (up to 600 px), not tiny thumbnails.

4. **Enable easy migration from Shotwell** — Image Perfect reads image metadata to recover tags and ratings.

> 📌 **Note**: To import your Shotwell tags and ratings, ensure that Shotwell is configured to write this information to image files.

<a id="about"></a>
## ℹ️ About

**Image Perfect** is a high-performance, cross-platform (Windows + Ubuntu) image viewer and photo management tool designed for **massive image libraries**. Whether you're organizing thousands or millions of photos, **Image Perfect** stays fast, responsive, and efficient.

Written in **C#**, using **Avalonia UI**, **MySQL**, and the **MVVM** architecture pattern. The app emphasizes **speed**, **clean design**, **direct file system folder organization**, and **scalable performance** — avoiding the long import times and tiny thumbnails in traditional photo managers.

**Key features include**:

- Large adjustable thumbnails (up to 600px wide)
- Smooth performance with huge libraries
- Rich tagging and folder-based organization
- Direct image viewing (no thumbnails written to disk)

> 💡 Future Plans:
Image Perfect is — and will always be — free and open source at its core.
In the future, I may offer an optional Pro version with extra features like duplicate detection or AI-powered facial recognition to help fund development.
Your feedback now helps shape what both the free and Pro versions might look like — so please share your thoughts!

<a id="core-features"></a>
## 👀 Core Features

### 🖼️ Big Thumbnails
- Adjustable image widths from **400px to 600px**
- Images are displayed directly (no caching or writing thumbnails to disk)

### 🏷️ Tagging & ⭐ Rating
- Tag and rate **images and folders**
- Image tags/ratings saved in both the **file** and the **database**
- Folder tags/ratings, and description stored in the **database only**
- Select **cover images** for folders
- Add image tags individually or in bulk (folder bulk tagging planned)
- Folder and image Tags can be removed one at a time or in bulk.
	+ Bulk operation removes tag across the entire database.
	+ Removing tags from individual images and folders is done one at a time.

### 🗲 Speed with Large Libraries
- **No long import times** thanks to MySqlBulkLoader (insert data from a csv file)
- New folders must be manually added (no auto-monitoring)
	+ To avoid double imports the app will check if you selected folders that are already in the library.
- Metadata scanning is user-initiated
	+ Bulk photo import and metadata scanning per folder or filtered set

### 📂 File System Mirroring
- Move, rename, and manage folders/images inside the app — changes reflected in the file system.
	+ #### Current File System Capabilities
		- Move, create, and delete folders
		- Import newly added folders (images must be present)
		- Delete individual or multiple images
		- Move individual or multiple images
	+ #### File System Capabilities To Add
		- Rename folders
		
	+ #### File System Issue/Bug/Limitations (Limitations have no plan to be "Fixed". Workarounds will be provided)
		- Known limitation: Folders imported containing only ZIP files cannot be opened.
			+ To fix delete the folder in app, unzip the files and then re-import it.
		- Known limitation: On Ubuntu/Linux, where the filesystem is case-sensitive, importing folders named e.g., Photos/BeachTrip and Photos/beachtrip causes both folders to show each other's contents.
			+ When adding a library or new folders; any folders with the same name but different case will not be imported. A log file "case_conflict_folders.txt" will list the date and folders not imported. From there users must rename the folders and then import them.  


### 📷 Shotwell Import
- Import existing tags and ratings from Shotwell (if written to images)


<a id="license"></a>
## 🪪 License

**Image Perfect** is licensed under the **GNU Affero General Public License v3.0 (AGPL-3.0)**.

You are free to use, modify, and distribute this software under the terms of the AGPL. If you modify and publicly distribute the software — including via a hosted service — you must make your source code available under the same license.

[License v3.0 (AGPL-3.0)](LICENSE.md)

---

📘 **Full Documentation:** See the [docs/](docs/) folder for complete guides on installation, configuration, and usage.


 


