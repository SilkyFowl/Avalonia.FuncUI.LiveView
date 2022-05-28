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
