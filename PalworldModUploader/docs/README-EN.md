# Palworld Mod Uploader for Experimental Testing

This guide provides the information required to test the official mod loader and Steam Workshop integration scheduled to be introduced in the December update.

## The First Step

As we move toward officially supporting mods, we prioritize respecting the existing modding community.
Instead of creating new, dedicated mod APIs or mechanisms, we built a system that allows creators to easily publish their existing mods to the Steam Workshop with minimal changes.

Currently, the following four types are supported:

* Paks (Resource replacement)
* Lua (UE4SS)
* LogicMods (UE4SS)
* UE4SS core
* PalSchema-dependent Mods

## Packaging Your Mod

To make existing or newly created mods loadable by the Palworld game client—and to publish them to the Steam Workshop—they must be packaged in the required format.

### Preparation

You need to have Steam running.

### Join the Steam Group

To enable Workshop features, send a request to join the Steam group and contact a Pocketpair representative.
[https://steamcommunity.com/groups/palexp](https://steamcommunity.com/groups/palexp)

### Launch PalworldModUploader.exe

You may see a warning such as “No subscriptions found, so the Workshop directory is not visible.”

Subscribe to *any* mod beforehand or manually specify the directory via the `...` at the top of the window and point it to:
`C:\Program Files (x86)\Steam\steamapps\workshop\content\1623730`

If `1623730` does not exist, please create it.
If your Steam installation is in a different location, adjust the path accordingly.

### Press `Create New Mod`

A new Steam Workshop ID for Palworld will be registered, allowing you to create a new mod. It will be private by default.

### Select the Type of Mod You Want to Create

All mods except UE4SS core can be configured from the display.

### Insert Your Mod Files

Once `Create New Mod` succeeds, a folder will open. Place your mod files according to the type of mod you’re creating.

**Pak files: `Paks/`**
Common for appearance changes or database edits. These mods overwrite Unreal Engine resources directly.

**Lua scripts: `Scripts/`**
Common for adjusting constraints. They modify Unreal Engine C++ behavior, usually via UE4SS.

**LogicMods: `LogicMods/`**
Used for adding new features or major logic changes within the game.
Most LogicMods depend on UE4SS. Mods created before the December update may not work properly.

**PalSchema: `PalSchema/`**
Used by mods that depend on PalSchema. Typically used together with UE4SS and the PalSchema core.

### Edit Info.json

Basic settings can be edited through the Palworld Mod Uploader.

#### Editing Manually

Modify `ModName`, `PackageName`, and `Auther`, and specify the blocks under `InstallRule` according to the types of files you placed.

*Important notes*

* If multiple mods share the same `PackageName`, they will not function correctly.
* If you change a mod after it has already been activated by the Palworld game client, you must increase the `Version`.

#### Example Configuration

Below is an example `InstallRule` for a mod containing Pak, Lua, and LogicMods.
You may remove unused directories. Targets generally do not need editing.

```
{
  "ModName": "MyAwesomeMod",
  "PackageName": "MyAwesomeMod",
  "Author": "yourname",
  "Thumbnail": "thumbnail.png",
  "Version": "1.0.0",
  "MinRevision": 82182,
  "Dependencies": [],
  "InstallRule": [
    {
      "Type": "Lua",
      "Targets": [
        "./Scripts/"
      ]
    },
    {
      "Type": "Paks",
      "Targets": [
        "./Paks/"
      ]
    },
    {
      "Type": "LogicMods",
      "Targets": [
        "./LogicMods/"
      ]
    }
  ]
}
```

After editing, if your mod appears in the Mod Uploader under `Mod Name` and `Auther`, the setup is complete.

### Enabling the Mod In-Game

After launching the game, go to Options → Mod Management.
Turn on **Allow Mod Usage**, enable your mod, then restart the game.

If your mod does not appear in the list, check the following:

* The Workshop directory specified when launching Palworld Mod Uploader is incorrect
* `Info.json` is not valid JSON (e.g., trailing commas)

### Testing the Mod In-Game

After restarting, the mod will be active.
Try creating a new world and verify that the mod is functioning.

Appearance-changing mods are often linked to specific player types in the character creation screen.

### Publishing

Selecting your mod and clicking **Upload To Steam** will publish your packaged mod to the Workshop.

It will upload as **private** by default; after adjusting settings on the Steam Workshop page, you may publish it.

You are welcome to publish your mod, but note:

* Once uploaded, **everyone in the “Palworld Experimental” group** will be able to subscribe to it.
* Some Workshop items may be removed before the December update is officially released.

## Technical Specifications

This section explains the technical specifications.

### Install Type

Install Type determines the directory where each component will be installed.
They correspond as follows:

**UE4SS**     → `Mods\NativeMods\UE4SS`
**Lua**       → `Mods\NativeMods\UE4SS\Mods\{PackageName}`
**PalSchema** → `Mods\NativeMods\UE4SS\Mods\PalSchema\mods`
**LogicMods** → `Pal\Content\Paks\LogicMods`
**Paks**      → `Pal\Content\Paks\~WorkshopMods`

### Package Name

The official Palworld mod loader identifies mods based on the Package Name.
As long as **multiple mods with the same Package Name are not enabled simultaneously**, uniqueness is not required.

#### Using the Same Package Name for Different Mods

It is possible to intentionally use the same Package Name across different mods.
For example, if you want to use a custom-configured UE4SS as a dependency instead of the default, you can set the Package Name to match the official UE4SS and use your custom version as the required dependency.

#### Mods with Restrictions

**PalSchema Core**
The PalSchema core *must* have the `PackageName` set to **`PalSchema`**.
This is because mods depending on PalSchema are always placed in:
`Mods\NativeMods\UE4SS\Mods\PalSchema\mods`
