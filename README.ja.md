[fantomas]: https://github.com/fsprojects/fantomas
[f# formatting]: https://marketplace.visualstudio.com/items?itemName=asti.fantomas-vs
[origin]: https://github.com/SilkyFowl/Avalonia.FuncUI.LiveView
[Howto-Video]: https://user-images.githubusercontent.com/16532218/170818646-29ded885-bc2a-4336-909a-b17fc7242345.mp4

# Avalonia.FuncUI.LiveView

Avalonia.FuncUI用のfs/fsxファイルライブプレビューア。

[English language is here][origin]

## これは何？

Avalonia.FuncUI.LiveViewはAvalonia.FuncUIのUIをリアルタイムプレビューすることを目指す実験的なFsAutoCompleteの拡張機能です。
FSharp.Analyzers.SDKを利用することで、エディターで編集中しているF#ファイルの内容をリアルタイムでプレビュー表示します。
**プレビューを更新するためにファイルを保存する必要はありません。**

## 使い方

> **Warning**
> Avalonia.FuncUI.LiveViewは未完成です。十分なテストが実施されておらず運用環境で使用することは想定されていません。このツールは自己責任で使用してください。

```sh
# リポジトリをクローンする
gh repo clone SilkyFowl/Avalonia.FuncUI.LiveView
cd Avalonia.FuncUI.LiveView

# セットアップ
dotnet tool restore
dotnet paket restore
dotnet fake run
dotnet fake run ./build.fsx -t SetLocalAnalyzer

# エディターを開く
code .
```

[エディターを開いた後の操作][Howto-Video]

## TODO

- v0.0.1-alpha
  - 機能追加は一旦終わり
  - Nugetに発行する。

### 必須

- テストコードを作成する。
- パッケージサイズの最適化
  - アナライザーが50MBなのは大きすぎる。必要なライブラリだけを組み込むようにビルドスクリプトを改良する必要がある。

### Docの拡充

- `PREVIEW`ディレクティブの説明
  - サンプルコードでもこっちを使ったものにするべきか？
  - 動作確認目的でデバッグするときにLivePreviewはいらないことを考えると、`DEBUG`ディレクティブとは別に管理して方が良いか
- VScode以外で使う例
  - 原理上、vimなどでも使えるはず

### 通信処理の整理

- IPアドレス、Portを設定する方法を考える
  - 環境変数が良い？

### パフォーマンス

- 更新が遅くなるケースを洗い出す。(サンプルを追加する)
  - もしかしたら複雑なロジックを内包するViewをPreviewするのは遅くなるかもしれない。
  - Elmishのサンプルで検証してみよう。

### 機能追加

- Fsiだけで完結できるようにする。
  - Cliの追加が妥当か？それこそ`XAML Studio`を参考にするべきか

#### vNext

- Appniumの対応
  - StoryBookのアレをやれると良さそう。
  - Avaloniaのリポジトリにサンプルコードあり
- 更新したコードがブレークポイントで止まるようにする。
  - コードで生成される見た目を確認するという目標からは少し外れる。
  - よってFsharpのAnalyzer APIの更新が完了してから着手する。
