[fantomas]: https://github.com/fsprojects/fantomas
[f# formatting]: https://marketplace.visualstudio.com/items?itemName=asti.fantomas-vs
[origin]: https://github.com/SilkyFowl/Avalonia.FuncUI.LiveView
[FSharp.Analyzers.SDK]: https://github.com/ionide/FSharp.Analyzers.SDK
[Avalonia.FuncUI-Docs]: https://avaloniacommunity.github.io/Avalonia.FuncUI.Docs/
[Howto-Video]: https://user-images.githubusercontent.com/16532218/170818646-29ded885-bc2a-4336-909a-b17fc7242345.mp4
[Ionide.Ionide-fsharp]: https://marketplace.visualstudio.com/items?itemName=Ionide.Ionide-fsharp
[FsAutoComplete]: https://github.com/fsharp/FsAutoComplete
[Enjoy-It!!]: https://user-images.githubusercontent.com/16532218/174063805-0d8db77d-f408-4639-aced-d38a38145685.mp4
[funcUi-analyzer]: github/img/README.ja/funcUi-analyzer.png
[cant-analyze-du]: github/img/README.ja/cant-analyze-du.png
[install-Ionide.Ionide-fsharp]: github/img/README.ja/install-Ionide.Ionide-fsharp.png
[First-Debug]: github/img/README.ja/First-Debug.png
[First-Debug-success]: github/img/README.ja/First-Debug-success.png
[DU-with-any-no-value-case]: github/img/README.ja/DU-with-any-no-value-case.png

# Avalonia.FuncUI.LiveView

Avalonia.FuncUI用のfs/fsxファイルライブプレビューア。

[English language is here][origin]

## これは何？

Avalonia.FuncUI.LiveViewは[Avalonia.FuncUI][Avalonia.FuncUI-Docs]のUIをリアルタイムプレビューすることを目指す実験的なライブラリです。
[FSharp.Analyzers.SDK]を利用することで、エディターで編集中しているF#ファイルの内容をリアルタイムでプレビュー表示します。
**プレビューを更新するためにファイルを保存する必要はありません。**

## チュートリアル

> **Warning**
> Avalonia.FuncUI.LiveViewは未完成です。十分なテストが実施されておらず運用環境で使用することは想定されていません。このツールは自己責任で使用してください。

以下はVScodeとPaketを使う場合です。

### 事前準備

VScodeに[Ionide.Ionide-fsharp]をインストールしてください。

![install extension][install-Ionide.Ionide-fsharp]

### FuncUIのセットアップ

作業フォルダを作成してVScodeを起動します。

```sh
mkdir YourFuncUIApp
code YourFuncUIApp/
```

FuncUIのテンプレートをインストールして、プロジェクトを作成します。

```sh
dotnet new -i JaggerJo.Avalonia.FuncUI.Templates
dotnet new funcUI.basic
```

Paketをセットアップします。

```sh
dotnet new tool-manifest
dotnet tool install paket
```

ここで以下のようにファイルを作成、編集します。

- paket.dependencies

```paket.dependencies
source https://api.nuget.org/v3/index.json

storage: none
framework: net6.0
```

- .vscode/launch.json

```json
{
    // IntelliSense を使用して利用可能な属性を学べます。
    // 既存の属性の説明をホバーして表示します。
    // 詳細情報は次を確認してください: https://go.microsoft.com/fwlink/?linkid=830387
    "version": "0.2.0",
    "configurations": [
        {
            "name": "FunUI Launch",
            "type": "coreclr",
            "request": "launch",
            "preLaunchTask": "build",
            "program": "${workspaceFolder}/bin/Debug/net6.0/YourFuncUIApp.dll",
            "args": [],
            "cwd": "${workspaceFolder}",
            "console": "internalConsole",
            "stopAtEntry": false,
            "linux": {
                "env": {
                    "LANG": "en_US.UTF-8"
                }
            }
        },
        {
            "name": "FuncUI Launch(Live Preview)",
            "type": "coreclr",
            "request": "launch",
            "preLaunchTask": "build",
            "program": "${workspaceFolder}/bin/Debug/net6.0/YourFuncUIApp.dll",
            "args": [],
            "cwd": "${workspaceFolder}",
            "console": "internalConsole",
            "stopAtEntry": false,
            "env": {
                "FUNCUI_LIVEPREVIEW": "1"
            },
            "linux": {
                "env": {
                    "LANG": "en_US.UTF-8",
                    "FUNCUI_LIVEPREVIEW": "1"
                }
            }
        }
    ]
}
```

- .vscode/tasks.json

```json
{
    "version": "2.0.0",
    "tasks": [
        {
            "type": "msbuild",
            "problemMatcher": [
                "$msCompile"
            ],
            "group": "build",
            "label": "build",
            "detail": "Build fsproj project using dotnet build"
        }
    ]
}
```

ここでデバッガを起動して、動作するのを確認してください。

![First-Debug]

起動したら次へ進みます。

![First-Debug-success](README.ja/First-Debug-success.png)

### FuncUI Analyzerのセットアップ

以下のコマンドを実行します。

```sh
dotnet paket convert-from-nuget --force
```

コマンドが完了したら以下のように編集します。

- paket.dependencies

```paket.dependencies
source https://api.nuget.org/v3/index.json

storage: none
framework: net6.0
nuget Avalonia.Desktop >= 0.10.12
nuget Avalonia.Diagnostics >= 0.10.12
nuget FSharp.Core content: none
nuget JaggerJo.Avalonia.FuncUI >= 0.5.0
nuget JaggerJo.Avalonia.FuncUI.DSL >= 0.5.0
nuget JaggerJo.Avalonia.FuncUI.Elmish >= 0.5.0
nuget Avalonia.Angle.Windows.Natives <= 2.1.0.2020091801 // こうしないとWindows環境で起動しなくなる
nuget SilkyFowl.Avalonia.FuncUI.LiveView

// [ Analyzers Group ]
group Analyzers
    framework: net6.0
    source https://api.nuget.org/v3/index.json
    storage: storage
    nuget SilkyFowl.Avalonia.FuncUI.LiveView.Analyzer
```

- paket.references

```diff
--- a/paket.references
+++ b/paket.references
@@ -3,4 +3,5 @@ Avalonia.Diagnostics
 JaggerJo.Avalonia.FuncUI
 JaggerJo.Avalonia.FuncUI.DSL
 JaggerJo.Avalonia.FuncUI.Elmish
-FSharp.Core
\ No newline at end of file
+FSharp.Core
+SilkyFowl.Avalonia.FuncUI.LiveView
\ No newline at end of file
```

その後、以下のコマンドでpaketを更新します。

```sh
dotnet paket update
```

ここで設定ファイルを追加します。

- .vscode/settings.json

```json
{
    "FSharp.enableAnalyzers": true,
    "FSharp.analyzersPath": [
        "packages/analyzers"
    ],
}
```

これで`FuncUi Analyzer`が起動します。

![Active Analyzer][funcUi-analyzer]

### LivePreviewのセットアップ

以下のようにコードを変更します。
`[<LivePreview>]`属性の付与された関数がLivePreviewの対象となります。

```diff
diff --git a/Counter.fs b/Counter.fs
index efce7d1..9aea401 100644
--- a/Counter.fs
+++ b/Counter.fs
@@ -5,6 +5,7 @@ module Counter =
     open Avalonia.FuncUI
     open Avalonia.FuncUI.DSL
     open Avalonia.Layout
+    open Avalonia.FuncUI.LiveView.Core.Types

     let view =
         Component(fun ctx ->
@@ -46,3 +47,13 @@ module Counter =
                 ]
             ]
         )
+
+    [<LivePreview>]
+    let preview() =
+          DockPanel.create [
+            DockPanel.children [
+                TextBlock.create [
+                    TextBlock.text "live preview!!"
+                ]
+            ]
+          ]
```

`LiveViewWindow`を起動出来るようにします。
ここでは環境変数`FUNCUI_LIVEPREVIEW`が`1`である場合にLiveViewが有効になるようにしています。

```diff
diff --git a/Program.fs b/Program.fs
index 00ef90f..4d22a95 100644
--- a/Program.fs
+++ b/Program.fs
@@ -1,10 +1,24 @@
 ﻿namespace YourFuncUIApp

+open System
+
 open Avalonia
+open Avalonia.Controls
 open Avalonia.Controls.ApplicationLifetimes
 open Avalonia.Input
 open Avalonia.Themes.Fluent
 open Avalonia.FuncUI.Hosts
+open Avalonia.FuncUI.LiveView
+
+module LiveView =
+    [<Literal>]
+    let FUNCUI_LIVEPREVIEW = "FUNCUI_LIVEPREVIEW"
+
+    let enabled =
+        match Environment.GetEnvironmentVariable FUNCUI_LIVEPREVIEW with
+        | null -> false
+        | "1" -> true
+        | _ -> false

 type MainWindow() as this =
     inherit HostWindow()
@@ -14,10 +28,10 @@ type MainWindow() as this =
         base.Height <- 400.0
         this.Content <- Counter.view

-        //this.VisualRoot.VisualRoot.Renderer.DrawFps <- true
-        //this.VisualRoot.VisualRoot.Renderer.DrawDirtyRects <- true
+#if DEBUG
+        this.AttachDevTools()
+#endif

-
 type App() =
     inherit Application()

@@ -27,7 +41,11 @@ type App() =
     override this.OnFrameworkInitializationCompleted() =
         match this.ApplicationLifetime with
         | :? IClassicDesktopStyleApplicationLifetime as desktopLifetime ->
-            desktopLifetime.MainWindow <- MainWindow()
+            desktopLifetime.MainWindow <-
+                if LiveView.enabled then
+                    LiveViewWindow() :> Window
+                else
+                    MainWindow()
         | _ -> ()

 module Program =

```

デバッガーの設定を`FuncUI Launch(Live Preview)`変更して起動します。

[Enjoy-It!!]

## 既知の不具合、制限

- [ ] 早く書く。

### もしファイル内に全てが値のあるケースラベルで構成された判別共用体があったらプレビュー出来ない

![cant...][cant-analyze-du]

### 回避方法

#### 値を持たないケースを1つ以上含めた判別共用体にする

![DU-with-any-no-value-case]

#### 判別共用体は別ファイルに定義する

```fsharp
// TypesDefinition.fs
module ElmishModule =
    type State = { watermark: string }
    let init = { watermark = "" }

    type Msg =
        | SetWatermark of string

    let update msg state =
        match msg with
        | SetWatermark test -> { state with watermark = test }
```

```fsharp
// OtherFile.fs
open Sample.ElmishModule

let view (state:State) dispatch =

    StackPanel.create [
        StackPanel.spacing 10.0
        StackPanel.children [
            TextBox.create [
                TextBox.watermark state.watermark
                TextBox.horizontalAlignment HorizontalAlignment.Stretch
            ]
            Button.create [
                Button.background "DarkBlue"
                Button.content "Set Watermark"
                Button.onClick (fun _ -> SetWatermark "test" |> dispatch)
                Button.horizontalAlignment HorizontalAlignment.Stretch
            ]


            Button.create [
                Button.content "Unset Watermark"
                Button.onClick (fun _ -> SetWatermark "" |> dispatch)
                Button.horizontalAlignment HorizontalAlignment.Stretch
            ]
        ]
    ]


type Host() as this =
    inherit Hosts.HostControl()

    do
        Elmish.Program.mkSimple (fun () -> init) update view
        |> Program.withHost this
        |> Elmish.Program.run

[<LivePreview>]
let preview () = ViewBuilder.Create<Host> []
```

## 仕組み

- [ ] 早く書く。

## 計画

-> SilkyFowl/Avalonia.FuncUI.LiveView#4
