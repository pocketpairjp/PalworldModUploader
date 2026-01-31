# 04-Tech.md

このガイドでは、主にInfo.jsonの内容と公式Modローダーの挙動について解説します。

## PalModSettings.ini

PalModSettings.ini は、有効となっているModや設定を保存するためのファイルで `Mods\PalModSettings.ini` に格納されています。ゲーム本体では、このファイルを直接編集する代わりに「Mod管理」メニューを通して操作を行うことができます。専用サーバーではこのファイルを直接編集してModの設定を行います。

```
[PalModSettings]
bGlobalEnableMod=True
WorkshopRootDir=C:\Program Files (x86)\Steam\steamapps\workshop\content\1623730
ActiveModList=SuperFastHandiworkQuivern
ActiveModList=UE4SS
```

**bGlobalEnableMod**
全体でModを有効化するかどうかを指定します。

**WorkshopRootDir** 
ワークショップのディレクトリを指定します。Steamから何らかのワークショップアイテムをサブスクライブした状態でゲーム本体を起動すると自動的に追記されます。専用サーバーの場合は手動で設定する必要があります。

**ActiveModList**
現在有効なModのPackageNameを指定します。複数指定可能です。
起動時に `WorkshopRootDir` 配下の `Info.json` をすべて確認し、PackageNameが合致したModが有効となります。

## Info.json

Info.json の仕様について紹介します。

### Package Name

Package NameはModを識別するために利用されます。厳密に他のワークショップアイテムと被らないようにする必要はありませんが、同じPackage Nameが含まれるModをサブスクライブした場合はどちらか片方しか有効にならず、順番も保証されません。

Install Typeに応じたインストール先に展開される際、Modを含むPackage Nameがフォルダ名として利用されます。

**Package Name に制約のあるMod**
PalSchema本体は、Package Nameを必ずPalSchemaにする必要があります。これは、PalSchemaに依存するModが必ず `Mods\NativeMods\UE4SS\Mods\PalSchema\mods` に展開されるためです。

### Debug Mode

Info.json に `DebugMode` を指定できます。`true` の場合、起動時に毎回アンインストール → 再インストール（Workshopから再コピー）が行われます。`false` または未指定の場合は、Version差分があるときのみ再インストールされます。

### Install Rules & Types

Install Rulesは、パッケージに含まれているModをどこに配置するかを設定するためのオブジェクトの配列です。

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

Install Typeはインストール先のディレクトリを決定するキーになります。それぞれ、下記の通りに対応しています。

| 種類        | パス |
|------------|------|
| UE4SS      | `Mods\NativeMods\UE4SS` |
| Lua        | `Mods\NativeMods\UE4SS\Mods\{PackageName}` |
| PalSchema  | `Mods\NativeMods\UE4SS\Mods\PalSchema\mods` |
| LogicMods  | `Pal\Content\Paks\LogicMods` |
| Paks       | `Pal\Content\Paks\~WorkshopMods` |

**専用サーバー向けMod**

専用サーバー向けModを作成する場合は、 `"IsServer": true` を指定したInstallRuleを追加する必要があります。下記の例では、ゲーム本体と全く同じファイルを専用サーバーでも利用します。

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

Targetsを変更することで、1つのModパッケージでゲーム本体・専用サーバー両対応のModを作成することもできます。

## Modのインストール

公式Modローダーでは、Palworldの起動時にModのインストール処理を行います。インストール処理は `Mods\PalModSettings.ini` を元に有効化・無効化が判断され、必要に応じてワークショップディレクトリからPalworldゲーム本体にModをコピーしインストールします。

## Modのアップデート

公式Modローダーはワークショップディレクトリに含まれるパッケージのInfo.jsonのVersionと、現在インストールされているModのVersionを単純に文字列として比較し、異なっている場合にModの再インストールを行います。

そのため、Versionに指定する値は厳密にMod本体のバージョンと一致している必要は無く、セマンティックバージョニングを採用する必要もありません。

Mod作者としてModをアップデートする場合は、特殊な目的がある場合を除き、Versionを変更してからアップデートする必要があります。

なお、Info.json の `DebugMode` が true の場合は、Versionが同じでも起動時に毎回再インストールされます。

---

最初に戻る: [01-General.md](01-General.md)
