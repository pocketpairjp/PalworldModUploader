# 03-ModUploader.md

Palworld Mod Uploader is a tool for editing mod packages (`Info.json`) in the Workshop content directory and uploading them to the Workshop.

## Usage

### 0. Preparation

You need to start Steam and log in with an account that owns Palworld.

### 1. Launch Palworld Mod Uploader

If Steam initialization fails at startup, `Steam Initialization Error` is shown and the tool exits. Check whether the Steam client is running and whether you are logged in.

### 2. Set the Workshop content directory

In `Workshop Content Directory` at the top of the window, specify the Workshop `1623730` folder. Usually it is the following path.

```
C:\Program Files (x86)\Steam\steamapps\workshop\content\1623730
```

If it cannot be auto-detected from subscribed items at startup, `Workshop Content Not Found` is shown. Select the folder manually via `...`. If you installed Steam to a different location, the path will also be different.

The specified path is saved as `workshop_path.txt` in the same folder as `PalworldModUploader.exe`. If you want to reload changes made manually in the mod directory, press `Reload`.

### 3. Create a new mod with `Create New Mod`

When you press `Create New Mod`, you can choose the type of mod you want to create (Lua / Paks / LogicMods / PalSchema).

When creation succeeds, a folder is created under the Workshop content directory and opened in Explorer. The following items are generated.

- `Info.json` (template. `ModName`/`PackageName` are set to `MyAwesomeMod`, and `Author` is set to your Steam display name)
- `thumbnail.png` (if bundled)
- A folder for the selected type (`Scripts\`, `Paks\`, etc.)
- `.workshop.json` (tool metadata. Stores the Published ID and the last uploaded `Version`)

At this point, it is registered on the Workshop as an empty item.
On first use, you may be asked to accept the Workshop terms of service. If so, accept the terms and try again.

#### Bypass Workshop registration

While holding `Shift`, click `Create New Mod` to create only a local folder without registering it on Steam. This is useful when creating debug mods or test mods only you will use.

Since folders created this way do not have a Published ID, you cannot run `Upload To Steam` as-is. If you want to publish it, move the contents to a folder created with the normal `Create New Mod`.

### 4. Place mod files

When Create New Mod succeeds, a folder opens. Place files according to the format of the mod you want to include.

**Pak files: `Paks\`**
A common mod type for visual changes or database edits. Modding is achieved by directly overriding Unreal Engine resources.

**Lua scripts: `Scripts\`**
A common mod type for changing limits and constraints. Modding is achieved by interacting with Unreal Engine C++ code.

**LogicMods: `LogicMods\`**
Often used for mods that add new elements or significantly rewrite in-game logic.

**PalSchema: `PalSchema\`**
Used for mods that depend on PalSchema. In most cases, it is used with UE4SS and the PalSchema core.

### 5. Edit mod information (`Save Info.json`)

When you select a mod folder from the list, you can edit the contents of `Info.json` on the right side.

Mods with `Subscribed` turned ON (installed from Steam) are view-only, and you cannot use `Save Info.json` or `Upload To Steam`.

Fields
| Key          | Description |
| ------------ | ----------- |
| Mod Name      | Mod name |
| Package Name  | Package name. It must contain only alphanumerics (`A-Z`, `a-z`, `0-9`). |
| Thumbnail    | Image shown in-game and on Steam |
| Version      | Mod package version |
| MinRevision  | Minimum revision required. This corresponds to the last 5 digits of the version shown in the title. |
| Author       | Author name |
| Dependencies | Array of other mod package names this mod depends on |
| Tags         | Steam Workshop tags (PalSchema, UE4SS, Model Replacement, Utilities, Gameplay, User Interface) |
| Install Rule Types  | Mod installation methods. If changed manually, you cannot edit it in the UI. |

After editing, press `Save Info.json` to save.

### 6. Launch the game and test the mod

Launch Palworld and enable the mod from Options → Mod Management. If the mod is not shown, check the following.

- The Workshop content directory is set correctly
- `Info.json` is valid JSON

### 7. Upload to the Workshop (`Upload To Steam`)

Select the mod you created and press `Upload To Steam` to upload it.

The following items are checked during upload.

- `Info.json` can be read
- `PackageName` is not empty and contains only alphanumerics
- At least one `InstallRule` exists, and `Type` and `Targets` are set

Also, if `Version` is the same as the previous upload `Version`, a warning is shown because Palworld may not recognize it as an update. When updating a mod, change `Version`.

Before the upload starts, you will be prompted to enter Change Notes. When the upload completes, the Workshop page opens. Set visibility, description, and additional images on the Workshop page.

## Creating mods for servers

When creating a mod for a server, you need to manually edit `Info.json` and add an object that specifies the required files with `"IsServer": true`. For details, refer to `04-Tech.md`.

For dedicated server installation steps and `Mods\PalModSettings.ini` configuration, the official documentation is also helpful.

**Install Mods on a Server**
https://docs.palworldgame.com/settings-and-operation/mod/

---

Next: [04-Tech.md](04-Tech.md)
