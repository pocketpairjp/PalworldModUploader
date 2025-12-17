# Palworld Mod Uploader

This repository contains the **Mod Uploader** that supports the official Mod Loader added in **Palworld v0.7**.

## Download

[https://github.com/pocketpairjp/PalworldModUploader/releases](https://github.com/pocketpairjp/PalworldModUploader/releases)

## Usage Guide

For instructions on how to actually upload a mod, please see `PalworldModUploader/docs`.

[https://github.com/pocketpairjp/PalworldModUploader/tree/main/PalworldModUploader/docs/en](https://github.com/pocketpairjp/PalworldModUploader/tree/main/PalworldModUploader/docs/en)

## Build

```
dotnet publish -c Release -r win-x64 --self-contained -p:RuntimeIdentifier=win-x64 -p:PublishSingleFile=true
```

## Issues

If you encounter any technical issues related to the Palworld Mod Uploader, please report them using GitHub Issues.
Responses will be provided on a best-effort basis, and please note that we may not be able to reply to all issues.

## Credits

Steamworks.NET – [LICENSE](https://github.com/rlabrecque/Steamworks.NET/blob/master/LICENSE.txt)
