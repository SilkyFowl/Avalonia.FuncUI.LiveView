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

#### Network

Use `localhost:8080` for communication between Analyzer and LivePreview.
Communication method will be improved in the future.

#### VScode

Install [Ionide.Ionide-fsharp] in VScode.

![install extension][install-Ionide.Ionide-fsharp]

### FuncUI Setup

Create a working directory and launch VScode.

```sh
mkdir YourFuncUIApp
cd YourFuncUIApp
code .
```

Create a project.


```sh
dotnet new tool-manifest
dotnet new gitignore
dotnet new sln
dotnet new console -o ./src/YourFuncUIApp -lang f#
dotnet sln add ./src/YourFuncUIApp/YourFuncUIApp.fsproj
```

#### Setup F# formatter

```sh
dotnet tool install fantomas
```

Create `.editorconfig`. The contents are as follows:

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

#### Paket Setup

```sh
dotnet tool install paket
```

Create `paket.dependencies`. The contents are as follows:

```paket.dependencies
source https://api.nuget.org/v3/index.json

storage: none

nuget FSharp.Core content: none
nuget Avalonia.FuncUI 1.0.1
nuget Avalonia.Desktop 11.0.3
nuget Avalonia.Themes.Fluent 11.0.3
nuget SilkyFowl.Avalonia.FuncUI.LiveView 0.0.3
```


```sh
dotnet paket convert-from-nuget --force --no-install --no-auto-restore
dotnet paket add -p ./src/YourFuncUIApp/YourFuncUIApp.fsproj Avalonia.FuncUI --no-install
dotnet paket add -p ./src/YourFuncUIApp/YourFuncUIApp.fsproj Avalonia.Desktop --no-install
dotnet paket add -p ./src/YourFuncUIApp/YourFuncUIApp.fsproj Avalonia.Themes.Fluent --no-install
dotnet paket add -p ./src/YourFuncUIApp/YourFuncUIApp.fsproj SilkyFowl.Avalonia.FuncUI.LiveView --no-install
dotnet paket install
```

#### Without Paket

Add a dependency to `YourFuncUIApp.fsproj` as follows:

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
    <PackageReference Include="SilkyFowl.Avalonia.FuncUI.LiveView" Version="0.0.3" />
  </ItemGroup>
</Project>
```

```sh
dotnet restore
```

### Write code

Rewrite `Program.fs` as follows:

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

Start the program and check if it works.

```sh
dotnet run --project ./src/YourFuncUIApp/YourFuncUIApp.fsproj
```

### vscode settings

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
    "FSharp.enableAnalyzers": true
}
```

Now start the debugger and make sure it works.

![First-Debug]

When launched, proceed to the next step.

![First-Debug-success]


### Setting up FuncUI Analyzer

Install Analyzer.

> **Warning**
> Installation of SilkyFowl.Avalonia.FuncUI.LiveView.Analyzer has changed since v0.0.3.

```sh
dotnet tool install SilkyFowl.Avalonia.FuncUI.LiveView.Analyzer --version 0.0.3 --tool-path analyzers
```

### Check if FuncUI Analyzer works

- `FSharp.enableAnalyzers` is true
- Analyzer Dll exists in `FSharp.analyzersPath`.

With these conditions, editing **the F# code recognized in the `Solution Explorer` of Ionide for F#** will start the `FuncUi Analyzer`.

![Active Analyzer][funcUi-analyzer]

> **Warning**
> You can analyze `fsx` scripts, etc. that are not recognized by the `Solution Explorer`, but you cannot start the `FuncUi Analyzer`.
>
> ![fsx-in-explorer]
>
> ![there-is-no-fsx-in-fs-explorer]

### Launch LivePreview

#### When using debugger

Change the debugger setting to `FuncUI Launch(Live Preview)` and start it.

[Enjoy-It!!]

#### When not using debugger

Set environment variables.

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
> Without the debugger, response to code changes is improved.

```sh
dotnet build -c Release
dotnet ./src/YourFuncUIApp/bin/Release/net7.0/YourFuncUIApp.dll
```

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
