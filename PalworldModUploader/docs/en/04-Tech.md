# 04-Tech.md

This guide mainly explains the contents of Info.json and the behavior of the official mod loader.

## PalModSettings.ini

PalModSettings.ini is a file used to store enabled mods and settings. It is stored in `Mods\PalModSettings.ini`. In the game client, you can operate it through the "Mod Management" menu instead of editing the file directly. On a dedicated server, you edit this file directly to configure mods.

```
[PalModSettings]
bGlobalEnableMod=True
WorkshopRootDir=C:\Program Files (x86)\Steam\steamapps\workshop\content\1623730
ActiveModList=SuperFastHandiworkQuivern
ActiveModList=UE4SS
```

**bGlobalEnableMod**
Specifies whether mods are enabled globally.

**WorkshopRootDir**
Specifies the Workshop directory. It is automatically added when you start the game client while subscribed to any Workshop item. On a dedicated server, you need to set it manually.

**ActiveModList**
Specifies the PackageName of currently enabled mods. Multiple entries are allowed.
At startup, the mod loader checks all `Info.json` files under `WorkshopRootDir`, and enables mods whose PackageName matches.

## Info.json

This section introduces the Info.json specification.

### Package Name

Package Name is used to identify a mod. It does not need to be strictly unique across all Workshop items, but if you subscribe to multiple mods that share the same Package Name, only one of them will be enabled, and the order is not guaranteed.

When a mod is extracted into its installation location based on Install Type, the Package Name is used as the folder name.

**Mods with Package Name restrictions**
The PalSchema core must have the Package Name set to PalSchema. This is because mods that depend on PalSchema are always extracted to `Mods\NativeMods\UE4SS\Mods\PalSchema\mods`.

### Install Rules & Types

Install Rules are an array of objects used to configure where mods included in the package are placed.

```json
{
  "InstallRule": [
    {
      "Type": "Lua",
      "Targets": [
        "./Scripts"
      ]
    },
    {
      "Type": "Lua",
      "IsServer": true,
      "Targets": [
        "./Scripts"
      ]
    }
  ]
}
```

Install Type is the key that determines the installation directory. Each value corresponds to the following paths.

| Type       | Path |
|-----------|------|
| UE4SS      | `Mods\NativeMods\UE4SS` |
| Lua        | `Mods\NativeMods\UE4SS\Mods\{PackageName}` |
| PalSchema  | `Mods\NativeMods\UE4SS\Mods\PalSchema\mods` |
| LogicMods  | `Pal\Content\Paks\LogicMods` |
| Paks       | `Pal\Content\Paks\~WorkshopMods` |

**Mods for dedicated servers**

To create mods for a dedicated server, you need to add an InstallRule with `"IsServer": true`. In the example below, the dedicated server uses the exact same files as the game client.

```json
{
  "InstallRule": [
    {
      "Type": "Lua",
      "Targets": [
        "./Scripts"
      ]
    },
    {
      "Type": "Lua",
      "IsServer": true,
      "Targets": [
        "./Scripts"
      ]
    }
  ]
}
```

By changing Targets, you can also create a single mod package that supports both the game client and a dedicated server.

## Installing Mods

In the official mod loader, mods are installed when Palworld starts. Whether a mod is enabled is determined based on `Mods\PalModSettings.ini`, and if needed, the mod loader copies mods from the Workshop content directory into the Palworld game directory to install them.

## Updating Mods

The official mod loader compares the Version in the package's Info.json in the Workshop content directory with the Version of the currently installed mod, as plain strings. If they are different, the mod is reinstalled.

Because of this, the Version value does not need to strictly match the mod's internal version, and you do not need to use semantic versioning.

As a mod author, when updating a mod, you should change Version before uploading the update (unless you have a special reason not to).

---

Back to start: [01-General.md](01-General.md)
