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

Live fs/fsx previewer for Avalonia.FuncUI.

[日本語(Javanese language)](README.ja.md)

## What is this ?

Avalonia.FuncUI.LiveView is an experimental FsAutoComplete extension that aims to provide a real-time preview of the UI of Avalonia.FuncUI.
Analyzers.SDK, it displays a real-time preview of the content of the F# file you are editing in the editor.
**No need to save the file to update the preview.**

## How to use

> **Warning**
> Avalonia.FuncUI.LiveView is incomplete. It has not been fully tested and is not intended for use in a production environment. Please use this tool at your own risk.

The following is a case of using VScode and Paket.

### Preliminary Preparation

Install [Ionide.Ionide-fsharp] in VScode.

![install extension][install-Ionide.Ionide-fsharp]

### FuncUI Setup

Create a working directory and launch VScode.

```sh
mkdir YourFuncUIApp
code YourFuncUIApp/
```

Install FuncUI template and create a project.

```sh
dotnet new -i JaggerJo.Avalonia.FuncUI.Templates
dotnet new funcUI.basic
```

Set up Paket.

```sh
dotnet new tool-manifest
dotnet tool install paket
```

Now create and edit the file as follows

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

Now start the debugger and make sure it works.

![First-Debug]

When launched, proceed to the next step.

![First-Debug-success]

### FuncUI Analyzer setup

Run the following commands.

```sh
dotnet paket convert-from-nuget --force
```

When command is complete, edit as follows

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
nuget Avalonia.Angle.Windows.Natives <= 2.1.0.2020091801 // If you don't do this, it won't start in a Windows environment.
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

Then update paket with the following command.

```sh
dotnet paket update
```

Add a configuration file here.

- .vscode/settings.json

```json
{
    "FSharp.enableAnalyzers": true,
    "FSharp.analyzersPath": [
        "packages/analyzers"
    ],
}
```

This will start the `FuncUi Analyzer`.

![Active Analyzer][funcUi-analyzer]

### LivePreview Setup

Change the code as follows.
Functions with the `[<LivePreview>]` attribute are subject to LivePreview.

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

Enables the `LiveViewWindow` to be activated.
In this case, LiveView is enabled when the environment variable `FUNCUI_LIVEPREVIEW` is `1`.

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

Change the debugger setting to `FuncUI Launch(Live Preview)` and start it.

[Enjoy-It!!]

## Known Issues, Limitations

- [ ] Write quickly.

### If there is a DU in the file that consists entirely of valued case labels, it cannot be previewed

![cant...][cant-analyze-du]

### workaround

#### Make it a DU that includes one or more cases with no value

![DU-with-any-no-value-case]

#### DU are defined in a separate file

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

## mechanism

- [ ] Write quickly.

## Plan

-> SilkyFowl/Avalonia.FuncUI.LiveView#4
