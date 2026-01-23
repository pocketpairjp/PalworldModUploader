# 02-Package.md

To publish a mod as a Workshop item, or to make it loadable by the official mod loader, you need to fit the mod into a folder that contains files in the required format. We call a folder that follows this required format a "package", and creating that folder "packaging".

## Palworld Mod Package
To make a mod loadable by the official mod loader, you need the following two things. Using Palworld Mod Uploader, you can create the basic settings and templates via the GUI.

### 1. Workshop Content Directory

This is the folder where packages are placed. In most cases, this is the `1623730` folder inside Steam's Workshop folder.

```
C:\Program Files (x86)\Steam\steamapps\workshop\content\1623730
```

Mods you subscribe to on Steam are automatically placed here, and when creating a mod from scratch you also start by creating a folder here.

For the game client, this folder is automatically detected via Steam integration. For a dedicated server, you need to explicitly specify the folder. See the guide below.

**Install Mods on a Server**
https://docs.palworldgame.com/ja/

The package folder name matches the Steam item ID. If you create a new package using Palworld Mod Uploader, it will automatically match the Steam item ID.

### 2. Info.json File

The `Info.json` file is a JSON file that contains metadata required for the official mod loader to recognize the mod, such as where to install it. Each package contains one `Info.json` file, and the presence of this file is what makes the folder recognized as a mod directory.

`Info.json` has the following format.

```
{
  "ModName": "Ultra Fast Handiwork Quivern",
  "PackageName": "UltraFastHandiworkQuivern",
  "Thumbnail": "thumbnail.png",
  "Version": "1.0.0-1",
  "DebugMode": false,
  "MinRevision": 82182,
  "Author": "pocketpair_dev",
  "Dependencies": [],
  "Tags": [],
  "InstallRule": [
    {
      "Type": "Lua",
      "Targets": [
        "./Scripts"
      ]
    }
  ]
}
```

| Key          | Description |
| ------------ | ----------- |
| ModName      | Mod name |
| PackageName  | Package name |
| Thumbnail    | Image shown in-game and on Steam |
| Version      | Mod package version |
| DebugMode    | If true, the mod is uninstalled and reinstalled on every launch (even if Version is unchanged). |
| MinRevision  | Minimum revision required. This corresponds to the last 5 digits of the version shown in the title. |
| Author       | Author name |
| Dependencies | Array of other mod package names this mod depends on |
| Tags         | Array of Steam Workshop tags (PalSchema, UE4SS, Model Replacement, Utilities, Gameplay, User Interface) |
| InstallRule  | Mod installation rules |

If you use Palworld Mod Uploader, you can configure these via the GUI. For detailed specifications (for example, manual editing), refer to **04-Tech.md**.

---

Next: [03-ModUploader.md](03-ModUploader.md)
