# 02-Package.md

作成したModをワークショップアイテムとして公開したり、公式Modローダーが読み込める形にするにはModを所定の形式のファイルを持つフォルダに当てはめる必要があります。所定の形式のフォルダのことを「パッケージ」、そのフォルダを作成することを「パッケージ化」と呼びます。

## Palworld Mod Package
公式ModローダーでModを読み込める形式にするには、下記の2つの準備が必要です。Palworld Mod Uploaderを利用することで、基本的な設定とテンプレートの生成をGUI上で行うことができます。

### 1. ワークショップディレクトリ

パッケージが配置されるフォルダです。多くの場合、Steamのワークショップフォルダ内に配置された `1623730` フォルダが該当します。

```
C:\Program Files (x86)\Steam\steamapps\workshop\content\1623730
```

SteamでサブスクライブしたModは自動的にここに配置されるほか、Modを一から作成する場合もここにフォルダを作成して始めます。

ゲーム本体の場合、このフォルダの場所はSteamと連携して自動的に取得されます。専用サーバーの場合、フォルダを明示的に指定する必要があります。下記ガイドをご覧ください。

**サーバーにModを導入**
https://docs.palworldgame.com/ja/

パッケージのフォルダ名はSteamのアイテムIDと一致します。Palworld Mod Uploaderを利用してパッケージを新規作成した場合、自動的にSteamのアイテムIDと一致します。

### 2. Info.jsonファイル

`Info.json` ファイルは、公式ModローダーがModを認識するために必要な情報やインストール先などのメタデータが含まれるJSONファイルです。1つのパッケージには1つの `Info.json` ファイルが配置され、このファイルの存在によりそのフォルダがModディレクトリであると認識されます。

`Info.json` は下記のような形式を持ちます。

```
{
  "ModName": "Ultra Fast Handiwork Quivern",
  "PackageName": "UltraFastHandiworkQuivern",
  "Thumbnail": "thumbnail.png",
  "Version": "1.0.0-1",
  "MinRevision": 82182,
  "Author": "pocketpair_dev",
  "Dependencies": [],
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

| Key          | 説明                                      |
| ------------ |-----------------------------------------|
| ModName      | Mod名                                    |
| PackageName  | パッケージ名                                  |
| Thumbnail    | ゲーム内やSteam上で表示される画像                     |
| Version      | Modパッケージのバージョン                          |
| MinRevision  | 動作に必要な最小リビジョン タイトルで表示されるバージョンの下5桁と対応します |
| Author       | 作者名                                     |
| Dependencies | このModが依存する他のModのパッケージ名の配列               |
| InstallRule  | Modのインストール方法                            |

Palworld Mod Uploaderを利用した場合は、GUI経由で設定を行うことができます。手動で設定したい場合など、詳細な仕様については **04-Tech.md** を参照してください。

---

次: [03-ModUploader.md](03-ModUploader.md)
