[← Back to Docs Index](README.md)

<a id="quick-start-windows"></a>
## 🚀 Quick Start Windows
### Option 1: Easiest Install (Recommended)
1. **Download the Installer**: 📥 Get [ImagePerfect v1.0-beta](https://github.com/BitForgeCraftedCode/ImagePerfect/releases/tag/v1.0-beta)
2. **Run the Installer** and follow the prompts.
3. When prompted by Windows Firewall with "**Allow mysqld.exe**", click Allow (required for the built-in MySQL server to run).
4. Launch **ImagePerfect** from the Start Menu or Desktop shortcut.

> See the [User Guide](USER_GUIDE.md) to begin organizing your images.

> ⚠️ **Unsigned Installer Notice**
> 
> The current Windows installer is **unsigned**, which may trigger warnings during download or installation.
> To ensure the installer has not been tampered with, please verify its integrity using the SHA-256 hash listed on the [release page](https://github.com/BitForgeCraftedCode/ImagePerfect/releases).
> 
> Example on Windows PowerShell:
> 
> ```powershell
> Get-FileHash ImagePerfectInstaller-v1.0-beta.exe -Algorithm SHA256
> ```
> 
> Compare the output to the hash shown in the release notes. If they match, the installer is safe to run.

>💡 Additional Security Notes:
>
>**Built from Source**: The installer is generated directly from the source code in this repository, so you can inspect the code yourself before running the installer.
>
>**Official Releases Only**: Always download installers from the official GitHub [release page](https://github.com/BitForgeCraftedCode/ImagePerfect/releases) to avoid tampered copies.

### Option 2: Manual Setup (Advanced Users)

1. **Install and Configure MySQL**: Follow the [MySQL setup instructions](MYSQL_SERVER_SETUP_WINDOWS.md) and execute the schema commands.
2. **Build and Install ImagePerfect**: [Follow The Build And Install Directions](BUILD_INSTALL_DIRECTIONS.md)
3. Run `ImagePerfect.exe` from the build output folder.

> See the [User Guide](USER_GUIDE.md) to begin organizing your images.