# Palworld Mod Uploiader for Experimental Testing

This guide contains the information needed to test the official mod loader and Steam Workshop integration scheduled to be introduced in the December update.

## The First Step

As we move toward officially supporting mods, our top priority is to respect the existing modding community.
Rather than creating a brand-new, mod-exclusive API or system, we’ve built things so that current mods can be published to Steam Workshop with as few changes as possible.

For now, the following four types are supported:

* Paks (Resource replacement)
* Lua (UE4SS)
* LogicMods (UE4SS)
* UE4SS itself

## Packaging Mods

To have existing or newly created mods recognized by the Palworld game client and to publish them to Steam Workshop, they must be packaged in a specified format.

### Preparation

You need to have Steam running.

### Join the Steam Group

To enable Workshop features, send a join request to the Steam group below and then contact the Pocketpair representative.

[https://steamcommunity.com/groups/palexp](https://steamcommunity.com/groups/palexp)

### Launch `PalworldModUploader.exe`

You may see a warning like: “You are not subscribed to anything, so the workshop directory cannot be found.”

In that case, either subscribe to any mod in advance, or from the `...` button at the top of the window, open:

`C:\Program Files (x86)\Steam\steamapps\workshop\content\1623730`

If `1623730` does not exist, create it.
If you changed your Steam install location, you may need to adjust this path.

### Click `Create New Mod`

An ID will be registered on the Palworld Steam Workshop, and you will be able to create a new mod entry.
New mods are private by default.

### Put Your Mod Files In

After `Create New Mod` succeeds, a folder will open.
Place your files into this folder according to the type of mod you’re installing.

**Pak files: `Paks/`**
This is a common mod type for visual changes or database edits.
Modding is achieved by directly overwriting Unreal Engine resources.

**Lua scripts: `Scripts/`**
This is a common mod type for changing limits or rules.
Modding is done by interfering with Unreal Engine’s C++ code.
In most cases, these depend on UE4SS.

**LogicMods: `LogicMods/`**
These are often used to add new elements or heavily rewrite in-game logic.
In most cases, they depend on UE4SS.
Mods created before the December update are very likely not to work correctly.

**PalSchema: `PalSchema/`**
Used by Mods that depend on PalSchema. Typically used by UE4SS and PalSchema itself.

---

### Edit `Info.json`

Change `ModName`, `PackageName`, and `Auther`, and in `InstallRule`, fill in blocks for the types of mods you have placed.

*Notes*

* If multiple mods share the same `PackageName`, they will not work correctly.
* If you change a mod after it has already been enabled by the Palworld game client, you must increase the `Version`.

#### Example Settings

Below is an example `InstallRule` when you have mods in Pak, Lua, and LogicMods.
You can remove any directories you don’t use.
You generally don’t need to change `Targets`.

```json
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

After editing, if your mod appears in the Mod Uploader’s `Mod Name` and `Auther` fields, you’re done.

---

### Enabling Mods In-Game

After launching the game, open Options and go to Mod Management.
Turn **Allow Mod Use** ON and enable the mod you created.
Save, then restart the game.

If the mod doesn’t appear on this screen, check the following:

* The Workshop directory specified when launching Palworld Mod Uploader is correct.
* `Info.json` is valid JSON (no extra commas, etc.).

---

### Checking Behavior In-Game

After restarting, mods will be enabled.

Create a new world or similar to verify that the mod is working.
For visual-change mods, content is often linked to specific player Types in the character creation screen.

---

### About Publishing

With a mod selected, choose **Upload To Steam** to publish your packaged mod to the Workshop.
It will be uploaded as **private** by default; you can then adjust the settings on the Steam Workshop page and make it public.

You are free to publish mods, but please note that once you upload, all members of the `Palworld Experimental` group will be able to subscribe to them.
Also, some Workshop items may be deleted before the December update is officially released.
