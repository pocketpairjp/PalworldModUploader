# 01-General.md

This document explains the official mod loader and Steam Workshop integration introduced in the December 2025 update `v0.7`. This guide is for mod creators. For general usage, please refer to the pages below.

**Mod Usage Guidelines**
https://eula.pocketpair.jp/palworld-mod-guideline

**Install Mods on a Server**
https://docs.palworldgame.com/ja/

## The First Step

As we move toward officially supporting mods, we prioritize respecting the existing modding community.
Many existing mods can be used via the Workshop simply by fitting them into the expected directory structure.

Currently, the official mod loader supports the following five types.
- Paks (Resource Replacement)
- Lua (UE4SS)
- LogicMods (UE4SS)
- UE4SS core
- PalSchema-dependent mods

## Creating Mods

The guides in this repository do not directly explain how to create the mod itself (for example, how to create Lua or Paks mods). Please research mod creation using web search and other resources.

### Community Modding Guides
Here are some community-driven modding guides.

**Palworld Modding Docs** Documentation that broadly covers the basics of Palworld modding and related tools.

https://pwmodding.wiki/

**PalSchema** A guide to PalSchema, created to improve the development experience for editing data tables and assets. It works as a dependency mod.

https://okaetsu.github.io/PalSchema/

## Supported Platforms
The official mod loader works on the following platforms.

- Windows (Steam)
- Windows dedicated server

## Reporting Issues
Where to report an issue depends on what the issue is related to.

### Issues with the Official Mod Loader
If the mod itself is correct but it does not load or does not work properly, please report the issue via the "Palworld Contact Form".

**Palworld Contact Form**
https://forms.pocketpair.jp/palworld

### Issues with Palworld Mod Uploader
If you encounter a technical issue with Palworld Mod Uploader, please report it using GitHub Issues.
Responses are best-effort, and we may not be able to reply to every issue.

**pocketpairjp/PalworldModUploader - Issues**
https://github.com/pocketpairjp/PalworldModUploader/issues

### Issues with the Mod Itself
Issues such as a specific mod not working or the game crashing cannot be fixed by Pocketpair developers.

---

Next: [02-Package.md](02-Package.md)
