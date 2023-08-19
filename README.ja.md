[fantomas]: https://github.com/fsprojects/fantomas
[f# formatting]: https://marketplace.visualstudio.com/items?itemName=asti.fantomas-vs
[origin]: https://github.com/SilkyFowl/Avalonia.FuncUI.LiveView
[FSharp.Analyzers.SDK]: https://github.com/ionide/FSharp.Analyzers.SDK
[Avalonia.FuncUI-Docs]: https://avaloniacommunity.github.io/Avalonia.FuncUI.Docs/
[Howto-Video]: https://user-images.githubusercontent.com/16532218/170818646-29ded885-bc2a-4336-909a-b17fc7242345.mp4
[Ionide.Ionide-fsharp]: https://marketplace.visualstudio.com/items?itemName=Ionide.Ionide-fsharp
[FsAutoComplete]: https://github.com/fsharp/FsAutoComplete
[Enjoy-It!!]: https://user-images.githubusercontent.com/16532218/174063805-0d8db77d-f408-4639-aced-d38a38145685.mp4
[funcUi-analyzer]: github/img/README/funcUi-analyzer.png
[cant-analyze-du]: github/img/README/cant-analyze-du.png
[install-Ionide.Ionide-fsharp]: github/img/README/install-Ionide.Ionide-fsharp.png
[First-Debug]: github/img/README/First-Debug.png
[First-Debug-success]: github/img/README/First-Debug-success.png
[DU-with-any-no-value-case]: github/img/README/DU-with-any-no-value-case.png
[fsx-in-explorer]: github/img/README/fsx-in-explorer.png
[there-is-no-fsx-in-fs-explorer]: github/img/README/there-is-no-fsx-in-fs-explorer.png

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

#### Network

AnalyzerとLivePreviewの通信で`localhost:8080`を使用します。
通信方式については今後改善予定です。

#### VScode

[Ionide.Ionide-fsharp]をインストールしてください。

![install extension][install-Ionide.Ionide-fsharp]

### FuncUIのセットアップ

作業フォルダを作成してVScodeを起動します。

```sh
mkdir YourFuncUIApp
cd YourFuncUIApp
dotnet new tool-manifest
dotnet new gitignore
code .
```

プロジェクトを作成します。


```sh
dotnet new sln
dotnet new console -o ./src/YourFuncUIApp -lang f#
dotnet sln add ./src/YourFuncUIApp/YourFuncUIApp.fsproj
```

#### F#のフォーマッタのセットアップ

```sh
dotnet tool install fantomas
```

`.\.editorconfig`を作成します。内容は以下の通りです。

```editorconfig
root = true

[*]
indent_style=space
indent_size=4
charset=utf-8
trim_trailing_whitespace=true
insert_final_newline=false

[*.{fs,fsx}]
fsharp_experimental_elmish=true
```

#### Paketのセットアップ

```sh
dotnet tool install paket
```

`paket.dependencies`を作成します。内容は以下の通りです。

```paket.dependencies
source https://api.nuget.org/v3/index.json

storage: none

nuget FSharp.Core content: none
nuget Avalonia.FuncUI 1.0.1
nuget Avalonia.Desktop 11.0.3
nuget Avalonia.Themes.Fluent 11.0.3
nuget SilkyFowl.Avalonia.FuncUI.LiveView 0.0.1.1

group Analyzers
    source https://api.nuget.org/v3/index.json
    storage: storage
    nuget SilkyFowl.Avalonia.FuncUI.LiveView.Analyzer 0.0.1.1
```


```sh
dotnet paket convert-from-nuget --force --no-install --no-auto-restore
dotnet paket add -p ./src/YourFuncUIApp/YourFuncUIApp.fsproj Avalonia.FuncUI --no-install
dotnet paket add -p ./src/YourFuncUIApp/YourFuncUIApp.fsproj Avalonia.Desktop --no-install
dotnet paket add -p ./src/YourFuncUIApp/YourFuncUIApp.fsproj Avalonia.Themes.Fluent --no-install
dotnet paket add -p ./src/YourFuncUIApp/YourFuncUIApp.fsproj SilkyFowl.Avalonia.FuncUI.LiveView --no-install
dotnet paket install
```

#### Paketを使わない場合

`YourFuncUIApp.fsproj`に依存関係を追加します。

```fsproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net7.0</TargetFramework>
  </PropertyGroup>

  <ItemGroup>
    <Compile Include="Program.fs" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Avalonia.Desktop" Version="11.0.3" />
    <PackageReference Include="Avalonia.Themes.Fluent" Version="11.0.3" />
    <PackageReference Include="Avalonia.FuncUI" Version="1.0.1" />
    <PackageReference Include="SilkyFowl.Avalonia.FuncUI.LiveView" Version="0.0.1.1" />
    <!-- MessagePackのバージョン指定しないと古いライブラリが参照されてしまう。 -->
    <PackageReference Include="MessagePack" Version="2.5.124" />
  </ItemGroup>
</Project>
```

`nuget`でアナライザをインストールします。

```sh
nuget install SilkyFowl.Avalonia.FuncUI.LiveView.Analyzer -Version 0.0.1.1 -OutputDirectory packages/analyzers
```

### 動作確認

`Program.fs`を以下のように書き換えます。

```fs
namespace CounterApp

open Avalonia
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.Themes.Fluent
open Avalonia.FuncUI.Hosts
open Avalonia.Controls
open Avalonia.FuncUI
open Avalonia.FuncUI.DSL
open Avalonia.Layout

open Avalonia.FuncUI.LiveView
open Avalonia.FuncUI.LiveView.Core.Types

module Main =
    [<LivePreview>]
    let view () =
        Component(fun ctx ->
            let state = ctx.useState 0

            DockPanel.create [
                DockPanel.children [
                    Button.create [
                        Button.dock Dock.Bottom
                        Button.onClick (fun _ -> state.Set(state.Current - 1))
                        Button.content "-"
                        Button.horizontalAlignment HorizontalAlignment.Stretch
                        Button.horizontalContentAlignment HorizontalAlignment.Center
                    ]
                    Button.create [
                        Button.dock Dock.Bottom
                        Button.onClick (fun _ -> state.Set(state.Current + 1))
                        Button.content "+"
                        Button.horizontalAlignment HorizontalAlignment.Stretch
                        Button.horizontalContentAlignment HorizontalAlignment.Center
                    ]
                    TextBlock.create [
                        TextBlock.dock Dock.Top
                        TextBlock.fontSize 48.0
                        TextBlock.verticalAlignment VerticalAlignment.Center
                        TextBlock.horizontalAlignment HorizontalAlignment.Center
                        TextBlock.text (string state.Current)
                    ]
                ]
            ])

type MainWindow() =
    inherit HostWindow()

    do
        base.Title <- "Counter Example"
        base.Content <- Main.view ()

module LiveView =
    let enabled =
        match System.Environment.GetEnvironmentVariable("FUNCUI_LIVEPREVIEW") with
        | null -> false
        | "1" -> true
        | _ -> false

type App() =
    inherit Application()

    override this.Initialize() =
        this.Styles.Add(FluentTheme())
        this.RequestedThemeVariant <- Styling.ThemeVariant.Dark

    override this.OnFrameworkInitializationCompleted() =
        match this.ApplicationLifetime with
        | :? IClassicDesktopStyleApplicationLifetime as desktopLifetime ->
            desktopLifetime.MainWindow <-
                if LiveView.enabled then
                    LiveViewWindow() :> Window
                else
                    MainWindow()
        | _ -> ()

#if DEBUG
        this.AttachDevTools()
#endif

module Program =

    [<EntryPoint>]
    let main (args: string[]) =
        AppBuilder
            .Configure<App>()
            .UsePlatformDetect()
            .UseSkia()
            .StartWithClassicDesktopLifetime(args)
```

起動して、プログラムが動作するか確認してください。

```sh
dotnet run --project ./src/YourFuncUIApp/YourFuncUIApp.fsproj
```

### vscodeの設定

- .vscode/launch.json

```json
{
    "configurations": [
        {
            "name": "FuncUI Launch",
            "type": "coreclr",
            "request": "launch",
            "preLaunchTask": "build",
            "program": "${workspaceFolder}/src/YourFuncUIApp/bin/Debug/net7.0/YourFuncUIApp.dll",
            "args": [],
            "cwd": "${workspaceFolder}",
            "stopAtEntry": false,
            "console": "internalConsole"
        },
        {
            "name": "FuncUI Launch (Preview)",
            "type": "coreclr",
            "request": "launch",
            "preLaunchTask": "build",
            "program": "${workspaceFolder}/src/YourFuncUIApp/bin/Debug/net7.0/YourFuncUIApp.dll",
            "args": [],
            "env": {
                "FUNCUI_LIVEPREVIEW": "1"
            },
            "cwd": "${workspaceFolder}",
            "stopAtEntry": false,
            "console": "internalConsole"
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
            "label": "build",
            "command": "dotnet",
            "type": "shell",
            "args": [
                "build",
            ],
            "problemMatcher": [
                "$msCompile"
            ],
            "group": "build"
        },
    ]
}
```

- .vscode/settings.json

```json
{
    "FSharp.enableAnalyzers": true,
    "FSharp.analyzersPath": [
        "packages/analyzers"
    ],
}
```

ここでデバッガを起動して、動作するのを確認してください。

![First-Debug]

起動したら次へ進みます。

![First-Debug-success]

### FuncUI Analyzerの動作確認

- `FSharp.enableAnalyzers`が`true`
- `FSharp.analyzersPath`にAnalyzerのDllが存在する

この条件をみたした状態で**Ionide for F#の`Solution Explorer`に認識されているF#コード**を編集すると`FuncUi Analyzer`が起動します。

![Active Analyzer][funcUi-analyzer]

> **Warning**
> 以下のような`Solution Explorer`に認識されてない`fsx`スクリプトなどもAnalyze出来ますが、`FuncUi Analyzer`の起動は出来ません。
>
> ![fsx-in-explorer]
>
> ![there-is-no-fsx-in-fs-explorer]

### LivePreviewの起動

#### デバッガーを使う場合

デバッガーの設定を`FuncUI Launch(Live Preview)`変更して起動します。

[Enjoy-It!!]

#### デバッガーを使わない場合

環境変数を設定します。

bash

```bash
export FUNCUI_LIVEPREVIEW=1
```

cmd

```bat
set FUNCUI_LIVEPREVIEW=1
```

powershell

```powershell
$env:FUNCUI_LIVEPREVIEW = 1
```

> **Note**
> デバッガーを使わないと、コード変更に対するレスポンスが向上します。

```sh
dotnet build -c Release
dotnet ./src/YourFuncUIApp/bin/Release/net7.0/YourFuncUIApp.dll
```

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
