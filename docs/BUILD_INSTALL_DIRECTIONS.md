[← Back to Docs Index](README.md)

<a id="build-and-install-directions"></a>
## 📋 Build And Install Directions

> 📌 **First, ensure MySQL Server is set up and running.**

- Clone this repository
```
git clone https://github.com/BitForgeCraftedCode/ImagePerfect.git
```

### Windows
- Open solution file in Visual Studio
- Right click on project file and click publish
- Set up your publish profile
	- Select local folder publish
	- Configuration: Release | Any CPU
	- Target framework: net8.0
	- Deployment mode: Self-contained
	- Target runtime: win-x64

Then to run the application double click on ImagePerfect.exe or you could also right click the exe and send to desktop as a shortcut.

> 📌 **Note**: If your MySql password (pwd) and user (uid) differs from what is in the appsettings.json file in this repository. You must change it.

```
{
  "ConnectionStrings": {
    "DefaultConnection": "server=127.0.0.1;uid=root;pwd=your-password;database=imageperfect;AllowLoadLocalInfile=true"
  }
}
```

> 🔐 **Security Note**: Never commit your real MySQL credentials to source control.

### Ubuntu
- Open solution file in Visual Studio
- Right click on project file and click publish
- Set up your publish profile
	- Select local folder publish
	- Configuration: Release | Any CPU
	- Target framework: net8.0
	- Deployment mode: Self-contained
	- Target runtime: linux-x64
	
Copy the publish files from your Windows PC to your Linux one. Or just use JetBrains Rider in Linux. The steps will be almost the same.

Then to run just open terminal in the build folder and run this command
```
./ImagePerfect
```

> 📌 **Note**: If your MySql password (pwd) and user (uid) differs from what is in the appsettings.json file in this repository. You must change it.

```
{
  "ConnectionStrings": {
    "DefaultConnection": "server=127.0.0.1;uid=root;pwd=your-password;database=imageperfect;AllowLoadLocalInfile=true"
  }
}
```

> 🔐 **Security Note**: Never commit your real MySQL credentials to source control.