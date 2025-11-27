# Palworld Mod Uploiader for Experimental Testing
このガイドは12月アップデートで導入される予定の公式Modローダー・Steam Workshop連携をテストするために必要な情報のガイドです。

## The First Step
Modに公式対応するにあたり、私たちは既存のModコミュニティを尊重することを優先しています。
新たなMod専用のAPIや仕組みは作らず、できる限り簡単に今のModをそのままSteam Workshopに公開できる仕組みを作りました。

今のところ、対応しているのは下記の4つです。
- Paks (Resource replacement)
- Lua (UE4SS)
- LogicMods (UE4SS)
- UE4SS本体
- PalSchema依存Mod

## Modのパッケージ化
既存のModや作成したModをPalworldのゲーム本体が読み込める形にしたり、Steam Workshop向けに公開するために、所定の形式に沿ったパッケージ化が必要です。

### 下準備
Steamを起動しておく必要があります。

### Steamグループへの参加
ワークショップを有効化するために、Steamグループに参加依頼を送り、Pocketpairの担当者にご連絡ください。
https://steamcommunity.com/groups/palexp

### PalworldModUploader.exeを起動
「何もサブスクしてないからワークショップディレクトリが見えない」という旨の警告が出ることがあります。

何等かのModをあらかじめサブスクライブするか、画面上部 `...` から `C:\Program Files (x86)\Steam\steamapps\workshop\content\1623730` を指定して開いてください。
`1623730` がない場合は作成してください。Steamインストール先を変えている方は変更が必要な可能性があります。

### `Create New Mod` を押す

PalworldのSteamワークショップにIDが登録され、Modの作成が行えるようになります。デフォルトで非公開です。

### Modを詰め込む
Create New Modが成功するとフォルダが開きます。導入したいModの形式に沿ってファイルを設置してください。

**Pakファイル: `Paks/`**
見た目変更やデータベース変更系でよくあるModタイプです。Unreal Engineのリソースを直接上書きすることでModdingを実現します。

**Luaスクリプト: `Scripts/`**
制限変更系でよくあるModタイプです。Unreal EngineのC++コードに干渉することでModdingを実現します。
大抵の場合、UE4SSに依存します。

**LogicMods: `LogicMods/`**
新しい要素を追加したりゲーム内のロジックを大きく書き換えるModでよく利用されます。
大抵の場合、UE4SSに依存します。12月アップデートまでに作成されたModはうまく動かない可能性が大きいです。

**PalSchema: `PalSchema/`**
PalSchemaに依存するModで利用されます。たいていの場合、UE4SSとPalSchema本体で利用されます。


### Info.jsonを編集する
`ModName`, `PackageName`, `Auther` を変更し、 `InstallRule` に詰めたModの種別のブロックを記入します。

*注意点*
- 同じPackageNameが設定されているModが複数存在するとうまく動作しません。
- 一度Palworldゲーム本体が有効にしたModを後から変更する場合、　`Version` を上げる必要があります。

#### 設定例
下記はPak, Lua, LogicModsすべてにModを詰めた場合の `InstallRule` の例です。利用しないディレクトリは削ってもOKです。Targetsは基本的に変更する必要はありません。

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

書き換えを行い、Mod Uploaderの `Mod Name`, `Auther` にModが表示されていれば完了です。

### ゲーム内からModを有効化する
起動後、オプションからMod管理を選択します。Mod使用を許可するをONにし、作成したModを有効化してください。
保存したら再起動します。

この画面に表示されない場合は下記を確認してください。
- Palworld Mod Uploader起動時に指定したワークショップディレクトリが正しくない
- `Info.json` が適切なJSONではない (不要なカンマがある等)

### ゲーム内で挙動を確認する
再起動後からModが有効になった状態になっています。新しいワールドを作成する等して、Modが有効になっているか確認してみてください。
見た目変更系のModは、キャラクリエイト画面で特定のプレイヤーTypeに紐づいていることが多いです。

### 公開について
Modを選択した状態でUpload To Steamを行うと自身のパッケージ化したModをワークショップに公開することができます。
デフォルトで非公開でアップロードされ、Steam Workshopページから設定を行った後公開することができます。

Modを公開していただいても問題はありませんが、公開操作を行った場合は `Palworld Experimental` グループ全員がサブスクライブできる状態になりますのでご注意ください。
また、12月アップデートリリース前にいくつかのWorkshopアイテムは削除される可能性があります。
