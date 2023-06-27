namespace Avalonia.FuncUI.LiveView

open Avalonia.FuncUI.Hosts
open Avalonia.FuncUI
open Avalonia
open FsConfig

[<AutoOpen>]
module internal Helper =
    open System.Collections.Generic
    open Avalonia.Interactivity

    let inline initList<'list, 'x when 'list: (new: unit -> 'list) and 'list: (member AddRange: IEnumerable<'x> -> unit)>
        ls
        =
        let collection = new 'list ()
        collection.AddRange ls
        collection

    module IDisposable =
        open System

        let create onDispose =
            { new IDisposable with
                member _.Dispose() : unit = onDispose ()
            }

        let empty =
            { new IDisposable with
                member _.Dispose() = ()
            }

        let dispose (disposable: IDisposable) = disposable.Dispose()

    module IAsyncDisposable =
        open System
        open System.Threading.Tasks

        type PatternBaseAsyncDisposable<'t when 't: (member DisposeAsync: unit -> ValueTask)> = 't

        let inline wrap<'t when PatternBaseAsyncDisposable<'t>> (asyncDisposable: 't) =
            { new IAsyncDisposable with
                member _.DisposeAsync() = asyncDisposable.DisposeAsync()
            }


    module StyleInclude =
        open Avalonia.Markup.Xaml.Styling
        open System

        let ofSource source =
            StyleInclude(baseUri = null, Source = Uri source)


    module IReadable =
        let subscribe fn (readable: IReadable<'t>) = readable.Subscribe fn

    let inline (|Source|_|) (e: RoutedEventArgs) : 't option =
        match e.Source with
        | :? 't as v -> Some v
        | _ -> None

    module CheckBox =
        open Avalonia.Controls

        let inline (|IsChecked|_|) e =
            match e with
            | Source(cb: CheckBox) -> Option.ofNullable cb.IsChecked
            | _ -> None

type Themes =
    | Fluent
    | Simple

module Themes =
    open Avalonia
    open Avalonia.Themes.Fluent
    open Avalonia.Styling
    open Avalonia.Themes.Simple

    open FSharp.Reflection

    let allCases =
        FSharpType.GetUnionCases(typeof<Themes>)
        |> Array.map (fun info -> FSharpValue.MakeUnion(info, [||]) :?> Themes)

    let fluentTheme =
        initList<Styles, IStyle> [
            FluentTheme()
            StyleInclude.ofSource "avares://Avalonia.Controls.ColorPicker/Themes/Fluent/Fluent.xaml"
            StyleInclude.ofSource "avares://Avalonia.Controls.DataGrid/Themes/Fluent.xaml"
        ]

    let simpleTheme =
        initList<Styles, IStyle> [
            SimpleTheme()
            StyleInclude.ofSource "avares://Avalonia.Controls.ColorPicker/Themes/Simple/Simple.xaml"
            StyleInclude.ofSource "avares://Avalonia.Controls.DataGrid/Themes/Simple.xaml"
        ]

    let init (application: Application) =

        if application.Styles.Count = 0 then
            for _ in 0 .. fluentTheme.Count - 1 do
                Style() |> application.Styles.Add

    let set thema (application: Application) =
        match thema with
        | Simple -> simpleTheme
        | Fluent -> fluentTheme
        |> Seq.iteri (fun i style -> application.Styles[i] <- style)

module Application =
    open Avalonia
    open Avalonia.Controls
    open Avalonia.Controls.ApplicationLifetimes

    let reopenMainWindow (init: unit -> Window) (app: Application) =
        match app.ApplicationLifetime with
        | :? IClassicDesktopStyleApplicationLifetime as desktopLifetime ->
            let oldWindow = desktopLifetime.MainWindow
            let newWindow = init ()
            desktopLifetime.MainWindow <- newWindow
            newWindow.Show()

            if not (isNull oldWindow) then
                oldWindow.Close()
        | _ -> ()


type Setting = {
    autoUpdate: bool
    enablePreviewItemViewBackground: bool
    theme: Themes
}

module Setting =
    open System.IO
    open System.Text.Json
    open System.Text.Json.Serialization
    open Microsoft.Extensions.Configuration
    open System.Runtime.InteropServices
    open FsConfig
    open System

    [<Literal>]
    let DefaultSettingJsonFile = "settings.default.json"

    [<Literal>]
    let SettingJsonFile = "settings.json"

    let configRoot =
        EnvConfig.Get<string> "XDG_CONFIG_HOME"
        |> Result.defaultWith (fun _ ->
            if RuntimeInformation.IsOSPlatform OSPlatform.Windows then
                Environment.GetFolderPath Environment.SpecialFolder.LocalApplicationData
            else
                Environment.GetFolderPath Environment.SpecialFolder.ApplicationData)

    let configdir = Path.Combine(configRoot, "funcui-preview")

    let configFile = Path.Combine(configdir, SettingJsonFile)

    let private jsonOptions = JsonFSharpOptions.Default().ToJsonSerializerOptions()

    let toJson value =
        JsonSerializer.Serialize<Setting>(value, jsonOptions)

    let saveAsync path value =
        task {
            use stream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite)
            use _ = IAsyncDisposable.wrap stream
            do! JsonSerializer.SerializeAsync<Setting>(stream, value, jsonOptions)
        }

    let configurationRoot =
        ConfigurationBuilder()
            .AddJsonFile(DefaultSettingJsonFile)
            .AddJsonFile(configFile, optional = true)
            .Build()

    let getSetting () =
        AppConfig(configurationRoot).Get<Setting>()


module View =
    open Avalonia.Controls
    open Avalonia.FuncUI.DSL
    open Avalonia.Layout
    open Avalonia.Interactivity

    let create setting =
        Component(fun ctx ->
            let setting = ctx.usePassed (setting)
            let autoUpdate = setting.Map(fun s -> s.autoUpdate)

            let enablePreviewItemViewBackground =
                setting.Map(fun s -> s.enablePreviewItemViewBackground)


            let selectedThemeIdx =
                Themes.allCases |> Array.findIndex (fun t -> t = setting.Current.theme)

            DockPanel.create [
                DockPanel.children [
                    StackPanel.create [
                        StackPanel.dock Dock.Bottom
                        StackPanel.row 1
                        StackPanel.column 0
                        StackPanel.margin 8
                        StackPanel.spacing 8
                        StackPanel.orientation Orientation.Horizontal
                        StackPanel.children [
                            CheckBox.create [
                                CheckBox.content "auto update"
                                CheckBox.isChecked autoUpdate.Current
                                CheckBox.onIsCheckedChanged (function
                                    | CheckBox.IsChecked newValue ->
                                        setting.Set {
                                            setting.Current with
                                                autoUpdate = newValue
                                        }
                                    | _ -> ())
                            ]
                            CheckBox.create [
                                CheckBox.content "fill background"
                                CheckBox.isChecked enablePreviewItemViewBackground.Current
                                CheckBox.onIsCheckedChanged (function
                                    | CheckBox.IsChecked newValue ->
                                        setting.Set {
                                            setting.Current with
                                                enablePreviewItemViewBackground = newValue
                                        }
                                    | _ -> ())
                            ]
                            ComboBox.create [
                                ComboBox.dataItems Themes.allCases
                                ComboBox.selectedIndex selectedThemeIdx
                                ComboBox.onSelectedItemChanged (function
                                    | :? Themes as newValue when newValue <> setting.Current.theme ->
                                        setting.Set {
                                            setting.Current with
                                                theme = newValue
                                        }
                                    | _ -> ())
                            ]
                        ]
                    ]
                ]
            ])

type MainWindow(setting) as this =
    inherit HostWindow(Title = "LiveView", Width = 800, Height = 600)

    let view = View.create setting

    do
        this.Content <- view
#if DEBUG
        this.AttachDevTools()
#endif