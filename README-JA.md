# Palworld Mod Uploader

Palworld v0.7 で追加された、公式Modローダーに対応するMod Uploaderのリポジトリです。

## ダウンロード

[https://github.com/pocketpairjp/PalworldModUploader/releases](https://github.com/pocketpairjp/PalworldModUploader/releases)

## 利用ガイド
実際にModをアップロードするガイドは `PalworldModUploader/docs` をご覧ください。

[https://github.com/pocketpairjp/PalworldModUploader/tree/main/PalworldModUploader/docs/ja](https://github.com/pocketpairjp/PalworldModUploader/tree/main/PalworldModUploader/docs/ja)

## ビルド

```
dotnet publish -c Release -r win-x64 --self-contained -p:RuntimeIdentifier=win-x64 -p:PublishSingleFile=true
```

## Issue

Palworld Mod Uploaderに関する技術的な問題が発生している場合、GitHubのIssueを使ってご報告をお願いいたします。
回答はベストエフォートとなり、全てのIssueに返信できない可能性もありますがご了承ください。

## Credits

Steamworks.NET　-  [LICENSE](https://github.com/rlabrecque/Steamworks.NET/blob/master/LICENSE.txt)
