# 01-General.md

2025年12月アップデート `v0.7` で導入された公式Modローダー・ワークショップ連携について解説します。このガイドはMod開発者向けです。利用については、下記ページをご覧ください。

**Mod使用ガイドライン**
https://eula.pocketpair.jp/palworld-mod-guideline

**サーバーにModを導入**
https://docs.palworldgame.com/ja/

## The First Step

Modに公式対応するにあたり、私たちは既存のModコミュニティを尊重することを優先しました。
多くの既存のModは、特定のディレクトリ構造に当てはめるだけでワークショップを通して簡単に利用することができます。

現在のところ、公式Modローダーで対応しているのは下記の5種類です。
- Paks (Resource Replacement)
- Lua (UE4SS)
- LogicMods (UE4SS)
- UE4SS本体
- PalSchemaに依存するMod

## Modの作り方

このリポジトリに含まれるガイドではLuaやPaksの作成の方法といったMod本体の作り方を直接説明しません。Mod本体の作成方法に関しては、Web検索等を利用して調査をお願いいたします。

### コミュニティ製Modding Guideのご紹介
コミュニティが支えているModding Guideをご紹介します。

**Palworld Modding Docs** PalworldのModdingの基礎やツールについて、幅広く扱っているドキュメントです。

https://pwmodding.wiki/

**PalSchema** データテーブルやアセットの変更・開発体験をよりよくする目的で作成されたPalSchemaのガイドです。いわゆる前提Modとして動作します。

https://okaetsu.github.io/PalSchema/

## 対応プラットフォーム
公式Modローダーは下記プラットフォームで動作します。

- Windows Steam
- Windows 専用サーバー

## 問題の報告
Modに関わる問題を報告したい場合、問題によって窓口が異なります。

### 公式Modローダーの問題
Modは正しいのに読み込まれない・うまく動作しない場合、「Palworld お問い合わせフォーム」から問題のご報告をお願いいたします。

**Palworld お問い合わせフォーム**
https://forms.pocketpair.jp/palworld

### Palworld Mod Uploaderの問題
Palworld Mod Uploaderに関する技術的な問題が発生している場合、GitHubのIssueを使ってご報告をお願いいたします。
回答はベストエフォートとなり、全てのIssueに返信できない可能性もありますがご了承ください。

**pocketpairjp/PalworldModUploader - Issues**
https://github.com/pocketpairjp/PalworldModUploader/issues

### Mod本体の問題
特定のModが動作しない・ゲームがクラッシュするなどの問題は、ポケットペアの開発者が修正することが出来ません。

---

次: [02-Package.md](02-Package.md)
