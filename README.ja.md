[fantomas]: https://github.com/fsprojects/fantomas
[f# formatting]: https://marketplace.visualstudio.com/items?itemName=asti.fantomas-vs
[origin]: https://github.com/SilkyFowl/Avalonia.FuncUI.LiveView
[FSharp.Analyzers.SDK]: https://github.com/ionide/FSharp.Analyzers.SDK
[Avalonia.FuncUI-Docs]: https://avaloniacommunity.github.io/Avalonia.FuncUI.Docs/
[Howto-Video]: https://user-images.githubusercontent.com/16532218/170818646-29ded885-bc2a-4336-909a-b17fc7242345.mp4
[Ionide.Ionide-fsharp]: https://marketplace.visualstudio.com/items?itemName=Ionide.Ionide-fsharp
[FsAutoComplete]: https://github.com/fsharp/FsAutoComplete
[SilkyFowl.Avalonia.FuncUI.LiveView.Demo]: https://github.com/SilkyFowl/SilkyFowl.Avalonia.FuncUI.LiveView.Demo
[SilkyFowl.Avalonia.FuncUI.LiveView.Demo.Paket]: https://github.com/SilkyFowl/SilkyFowl.Avalonia.FuncUI.LiveView.Demo.Paket
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

## デモリポジトリ

- [SilkyFowl.Avalonia.FuncUI.LiveView.Demo] 
- [SilkyFowl.Avalonia.FuncUI.LiveView.Demo.Paket]

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
code .
```

プロジェクトを作成していきます。

```sh
dotnet new tool-manifest
dotnet new gitignore
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
nuget Avalonia.FuncUI 1.1.0
nuget Avalonia.Desktop 11.0.5
nuget Avalonia.Diagnostics 11.0.5
nuget Avalonia.Themes.Fluent 11.0.5
nuget SilkyFowl.Avalonia.FuncUI.LiveView.Attribute 0.0.4-preview01
```


```sh
dotnet paket convert-from-nuget --force --no-install --no-auto-restore
dotnet paket add -p ./src/YourFuncUIApp/YourFuncUIApp.fsproj Avalonia.FuncUI --no-install
dotnet paket add -p ./src/YourFuncUIApp/YourFuncUIApp.fsproj Avalonia.Desktop --no-install
dotnet paket add -p ./src/YourFuncUIApp/YourFuncUIApp.fsproj Avalonia.Diagnostics --no-install
dotnet paket add -p ./src/YourFuncUIApp/YourFuncUIApp.fsproj Avalonia.Themes.Fluent --no-install
dotnet paket add -p ./src/YourFuncUIApp/YourFuncUIApp.fsproj SilkyFowl.Avalonia.FuncUI.LiveView.Attribute --no-install
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
    <PackageReference Include="Avalonia.Desktop" Version="11.0.5" />
    <PackageReference Include="Avalonia.Themes.Fluent" Version="11.0.5" />
    <PackageReference Include="Avalonia.FuncUI" Version="1.1.0" />
    <PackageReference Include="SilkyFowl.Avalonia.FuncUI.LiveView.Attribute" Version="0.0.4-preview01" />
  </ItemGroup>
</Project>
```

```sh
dotnet restore
```

#### コードを書く

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

module Main =
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
    
    [<LivePreview>]
    let preview () = view()

type MainWindow() =
    inherit HostWindow()

    do
        base.Title <- "Counter Example"
        base.Content <- Main.view ()

type App() =
    inherit Application()

    override this.Initialize() =
        this.Styles.Add(FluentTheme())
        this.RequestedThemeVariant <- Styling.ThemeVariant.Dark

    override this.OnFrameworkInitializationCompleted() =
        match this.ApplicationLifetime with
        | :? IClassicDesktopStyleApplicationLifetime as desktopLifetime ->
            desktopLifetime.MainWindow <- MainWindow()
        | _ -> ()

#if DEBUG
        this.AttachDevTools()
#endif

#if !LIVEPREVIEW
module Program =

    [<EntryPoint>]
    let main (args: string[]) =
        AppBuilder
            .Configure<App>()
            .UsePlatformDetect()
            .UseSkia()
            .StartWithClassicDesktopLifetime(args)
#endif
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
    "FSharp.enableAnalyzers": true
}
```

ここでデバッガを起動して、設定によって動作するのを確認してください。

![First-Debug]

起動したら次へ進みます。

![First-Debug-success]


### FuncUI Analyzerのセットアップ

#### インストール

> **Note**
> v0.0.3からSilkyFowl.Avalonia.FuncUI.LiveView.Analyzerのインストール方法が変わりました。

```sh
dotnet tool install SilkyFowl.Avalonia.FuncUI.LiveView.Analyzer --version 0.0.4-preview01 --tool-path analyzers
```

#### 動作確認

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

### LivePreviewのセットアップ

#### インストール

```sh
dotnet tool install SilkyFowl.Avalonia.FuncUI.LiveView.Cli --version 0.0.4-preview01
```

> **Note**
> v0.0.4以降、LivePreviewはdotnet-toolから利用することを推奨しますが、v0.0.3までのようにSilkyFowl.Avalonia.FuncUI.LiveViewライブラリから利用する方法も可能です。

#### 動作確認

```sh
dotnet funcui-liveview
```

#### 起動設定

funcui-liveviewは起動してからPreviewを行うプロジェクトを選択する必要がありますが、以下の環境変数を設定することで起動時に自動的にPreviewを行うプロジェクトを選択することが出来ます。

- `FUNCUI_LIVEVIEW_WATICHING_PROJECT_INFO_PATH`...Previewを行うプロジェクトのパス
- `FUNCUI_LIVEVIEW_WATICHING_PROJECT_INFO_TARGET_FRAMEWORK`...Previewを行うプロジェクトのターゲットフレームワーク

vscodeの場合は、以下のようなタスクを追加しておくといいでしょう。

```json
        {
            "label": "start funcui-liveview",
            "type": "shell",
            "command": "dotnet",
            "args": [
                "funcui-liveview"
            ],
            "problemMatcher": [],
            "isBackground": true,
            "options": {
                "env": {
                    "FUNCUI_LIVEVIEW_WATICHING_PROJECT_INFO_PATH": "${workspaceFolder}/src/App/App.fsproj",
                    "FUNCUI_LIVEVIEW_WATICHING_PROJECT_INFO_TARGET_FRAMEWORK": "net7.0"
                }
            }
        },
        {
            "label": "watch App project",
            "type": "shell",
            "command": "dotnet",
            "args": [
                "watch",
                "build", // or "run" or "test", etc.
                "--project",
                "${workspaceFolder}/src/App/App.fsproj"
            ],
        },
        {
            "label": "start watch App project and funcui-liveview",
            "dependsOn": [
                "watch App project",
                "start funcui-liveview"
            ],
        }
```

## 計画

-> SilkyFowl/Avalonia.FuncUI.LiveView#4
