[Setup]
AppName=ImagePerfect
AppVersion=1.02-beta
DefaultDirName=C:\Users\Public
DefaultGroupName=ImagePerfect
ArchitecturesInstallIn64BitMode=x64
OutputDir=.\Output
OutputBaseFilename=ImagePerfectInstaller-v1.02-beta
Compression=lzma
SolidCompression=yes
PrivilegesRequired=admin
CloseApplications=yes
; hides the "choose installation folder" page
DisableDirPage=yes
DisableProgramGroupPage=yes
 
[Dirs]
Name: "{app}\ImagePerfect\mysql\data"; Permissions: users-full
Name: "{app}\ImagePerfect\mysql\logs"; Permissions: users-full

[Files]
; Copy the app
Source: "C:\Users\arogala\Documents\GitHub\ImagePerfect\InnoSetup\imageperfect-1.02-win-x64\*"; DestDir: "{app}\ImagePerfect"; Flags: ignoreversion recursesubdirs createallsubdirs 

; Copy MySQL
Source: "C:\Users\arogala\Documents\GitHub\ImagePerfect\InnoSetup\mysql-8.4.6-winx64\*"; DestDir: "{app}\ImagePerfect\mysql"; Flags: ignoreversion recursesubdirs createallsubdirs

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional icons:"; Flags: unchecked

[Icons]
Name: "{group}\ImagePerfect"; Filename: "{app}\ImagePerfect\ImagePerfect.exe"; WorkingDir: "{app}\ImagePerfect"; IconFilename: "{app}\ImagePerfect\icons8-image-256.ico"; 
Name: "{userdesktop}\ImagePerfect"; Filename: "{app}\ImagePerfect\ImagePerfect.exe"; WorkingDir: "{app}\ImagePerfect"; IconFilename: "{app}\ImagePerfect\icons8-image-256.ico"; Tasks: desktopicon

[Run]
; Pre-install cleanup: remove any old ImagePerfect MySQL scheduled task
Filename: "schtasks.exe"; Parameters: "/Delete /TN ""ImagePerfect MySQL"" /F"; Flags: runhidden

; Create scheduled task to run MySQL with SYSTEM account and proper working directory
Filename: "schtasks.exe"; \
    Parameters: "/Create /SC ONSTART /RU SYSTEM /TN ""ImagePerfect MySQL"" /TR ""\""{app}\ImagePerfect\mysql\bin\mysqld.exe\"" --defaults-file=\""{app}\ImagePerfect\mysql\my.ini\"""" /RL HIGHEST /F"; \
    Flags: runhidden


; Start MySQL immediately so user does not have to reboot
Filename: "{app}\ImagePerfect\mysql\bin\mysqld.exe"; \
    Parameters: "--defaults-file=""{app}\ImagePerfect\mysql\my.ini"""; \
    Flags: runhidden nowait

[UninstallRun]
; 1) Remove scheduled task first (won't fail if it doesn't exist)
Filename: "schtasks.exe"; Parameters: "/Delete /TN ""ImagePerfect MySQL"" /F"; Flags: runhidden

; 2) Kill any running MySQL server process -- kill child processes as well
Filename: "taskkill.exe"; Parameters: "/F /IM mysqld.exe /T"; Flags: runhidden waituntilterminated

; 3) small time delay after taskkill so files are not locked for delete
Filename: "ping.exe"; Parameters: "127.0.0.1 -n 3 > nul"; Flags: runhidden

; 4) (Optional second kill to guarantee process is gone)
Filename: "taskkill.exe"; Parameters: "/F /IM mysqld.exe /T"; Flags: runhidden waituntilterminated

[UninstallDelete]
; Remove the entire installation folder (including data created after install)

Type: filesandordirs; Name: "{app}\ImagePerfect"
Type: filesandordirs; Name: "{app}\ImagePerfect\mysql"
