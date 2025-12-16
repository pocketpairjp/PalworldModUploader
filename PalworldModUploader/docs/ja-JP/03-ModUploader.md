# 03-ModUploader.md

Palworld Mod Uploaderは、Workshopディレクトリ内のModパッケージ（`Info.json`）を編集し、Steam Workshopへアップロードするためのツールです。

## 使い方

### 0. 下準備

Steamを起動し、Palworldを所持しているアカウントでログインしておく必要があります。

### 1. Palworld Mod Uploaderの起動

起動時にSteamの初期化に失敗すると `Steam Initialization Error` が表示され、ツールは終了します。Steamクライアントが起動しているか、ログインしているかを確認してください。

### 2. Workshopディレクトリを設定する

画面上部の `Workshop Content Directory` に、Workshopの `1623730` フォルダを指定します。通常は次のパスになります。

```
C:\Program Files (x86)\Steam\steamapps\workshop\content\1623730
```

起動時にサブスクライブ済みアイテムから自動検出できない場合、`Workshop Content Not Found` が表示されます。`...` から手動でフォルダを選択してください。Steamのインストール先を変更している場合はパスも変わります。

指定したパスは `workshop_path.txt` として `PalworldModUploader.exe` と同じフォルダに保存されます。 Modディレクトリに手動で変更した内容を読み込み直したい場合は `Reload` を押してください。

### 3. `Create New Mod` で新規作成する

`Create New Mod` を押すと、作成したいModのタイプ（Lua / Paks / LogicMods / PalSchema）を選択できます。

作成に成功すると、Workshopディレクトリ配下にフォルダが作成され、エクスプローラーで開きます。フォルダには次が生成されます。

- `Info.json`（ひな形。`ModName`/`PackageName` は `MyAwesomeMod`、`Author` はSteamの表示名が入ります）
- `thumbnail.png`（同梱されている場合）
- 選択したタイプのフォルダ（ `Scripts/`、`Paks/` など）
- `.workshop.json`（ツール用のメタデータ。Published IDや前回アップロードした `Version` を保存します）

この時点でSteam Workshopには空のアイテムとして登録されます。
初回はSteam Workshopの利用規約への同意が求められることがあります。その場合は規約に同意してからやり直してください。

#### Steam Workshopへの登録をバイパスする

`Shift` を押しながら `Create New Mod` をクリックすると、Steamに登録せずにローカルフォルダだけを作成します。自分だけが利用するデバッグ用Modやテスト用Modを作成したい場合に便利です。

この方法で作成したフォルダにはPublished IDが無いため、そのままでは `Upload To Steam` は行えません。公開したい場合は通常の `Create New Mod` で作成したフォルダへ内容を移してください。

### 4. Modファイルを格納する

Create New Modが成功するとフォルダが開きます。導入したいModの形式に沿ってファイルを設置してください。

**Pakファイル: `Paks/`**
見た目変更やデータベース変更系でよくあるModタイプです。Unreal Engineのリソースを直接上書きすることでModdingを実現します。

**Luaスクリプト: `Scripts/`**
制限変更系でよくあるModタイプです。Unreal EngineのC++コードに干渉することでModdingを実現します。

**LogicMods: `LogicMods/`**
新しい要素を追加したりゲーム内のロジックを大きく書き換えるModでよく利用されます。

**PalSchema: `PalSchema/`**
PalSchemaに依存するModで利用されます。たいていの場合、UE4SSとPalSchema本体で利用されます。

### 5. Mod情報を編集する（`Save Info.json`）

一覧からModフォルダを選択すると、右側で `Info.json` の内容を編集できます。

`Subscribed` がONになっているMod（Steamからインストールされたもの）は閲覧のみで、`Save Info.json` と `Upload To Steam` は利用できません。

各要素
| Key          | 説明                                      |
| ------------ |-----------------------------------------|
| Mod Name      | Mod名                                    |
| Package Name  | パッケージ名  英数字（`A-Z`, `a-z`, `0-9`）のみである必要があります。|
| Thumbnail    | ゲーム内やSteam上で表示される画像                     |
| Version      | Modパッケージのバージョン                          |
| MinRevision  | 動作に必要な最小リビジョン タイトルで表示されるバージョンの下5桁と対応します |
| Author       | 作者名                                     |
| Dependencies | このModが依存する他のModのパッケージ名の配列               |
| Install Rule Types  | Modのインストール方法 手動で変更した場合、操作出来ません |

編集後は `Save Info.json` を押して保存します。

### 6. ゲームを開きModをテストする

Palworldを起動し、オプションの「Mod管理」からModを有効化して動作確認します。表示されない場合は下記を確認してください。

- Workshopディレクトリの指定が正しい
- `Info.json` が適切なJSONになっている

### 7. Steam Workshopにアップロードする（`Upload To Steam`）

作成したModを選択し、`Upload To Steam` を押すとアップロードできます。

アップロード時に次がチェックされます。

- `Info.json` が読み込めること
- `PackageName` が空でないこと、英数字のみであること
- `InstallRule` が1つ以上あり、`Type` と `Targets` が設定されていること

また、前回アップロード時の `Version` と同じ場合、Palworld側でアップデートとして認識されない可能性があるため警告が出ます。アップデート時は `Version` を変更してください。

アップロード開始前にChange Notesの入力が求められます。完了するとSteam Workshopページが開きます。 公開設定や説明文、追加の画像はSteam Workshopページ側で設定してください。

## サーバーで利用するModを作成する

サーバー向けModを作成する場合、 `Info.json` を手動で修正し、必要なファイルを `"IsServer": true` として指定するオブジェクトを追加する必要があります。詳しくは `04-Tech.md` を参照してください。

Dedicated Serverへの導入手順や `Mods/PalModSettings.ini` の設定は、公式ドキュメントも参考になります。

**サーバーにModを導入**
https://docs.palworldgame.com/ja/
