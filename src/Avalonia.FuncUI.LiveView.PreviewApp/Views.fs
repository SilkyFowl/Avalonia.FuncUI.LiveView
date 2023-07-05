namespace Avalonia.FuncUI.LiveView

open Avalonia
open Avalonia.Styling
open Avalonia.FuncUI.Hosts
open Avalonia.FuncUI
open FsConfig

open Avalonia.FuncUI.LiveView.Types.PreviewApp

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

module Themes =
    open Avalonia.Themes.Fluent
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


module BuildinThemeVariantCase =
    open FSharp.Reflection

    let allCases =
        FSharpType.GetUnionCases(typeof<BuildinThemeVariantCase>)
        |> Array.map (fun info -> FSharpValue.MakeUnion(info, [||]) :?> BuildinThemeVariantCase)

    let toThemeVariant t =
        match t with
        | Default -> ThemeVariant.Default
        | Dark -> ThemeVariant.Dark
        | Light -> ThemeVariant.Light


module Setting =
    open System
    open System.IO
    open System.Runtime.InteropServices
    open System.Text.Json
    open System.Text.Json.Serialization
    open Microsoft.Extensions.Configuration

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


module PreviewService =
    open Avalonia.FuncUI.LiveView.Types
    open Avalonia.FuncUI.LiveView.Types.LiveView

    let initState () =
        new State<Model>(
            {
                evalPendingMsgs = []
                evalStateMap = Map.empty
                evalInteractionDeferred = Waiting
                logMessage = LogMessage.info "init"
                autoUpdate = true
            }
        )

module SelectProjectView =
    open Avalonia.Controls
    open Avalonia.FuncUI.DSL
    open Avalonia.Layout
    open Avalonia.Platform.Storage
    open Avalonia.Media

    let openDllPicker ctr =
        task {
            let provier = TopLevel.GetTopLevel(ctr).StorageProvider
            let! location = provier.TryGetWellKnownFolderAsync(WellKnownFolder.Documents)

            let! result =
                provier.OpenFilePickerAsync(
                    FilePickerOpenOptions(
                        Title = "Open...",
                        SuggestedStartLocation = location,
                        AllowMultiple = false,
                        FileTypeFilter = [
                            FilePickerFileType(
                                "Binary Log",
                                Patterns = [ "*.binlog"; "*.buildlog" ],
                                MimeTypes = [ "application/binlog"; "application/buildlog" ],
                                AppleUniformTypeIdentifiers = [ "public.data" ]
                            )
                        ]
                    )
                )

            match List.ofSeq result with
            | [ picked ] -> return Some picked
            | _ -> return None
        }

    let centerText text =
        TextBlock.create [ TextBlock.textAlignment TextAlignment.Center; TextBlock.text text ]

    let create id onLoadProject attrs =
        Component.create (
            $"select-project-view-$%s{id}",
            fun ctx ->
                let binlogPath = ctx.useState ""
                let projs: IWritable<ProjArgsInfo list> = ctx.useState []
                let selectedProj = ctx.useState None

                ctx.useEffect (
                    (fun () -> MSBuildBinLog.getFscArgs binlogPath.Current |> Seq.toList |> projs.Set),
                    [ EffectTrigger.AfterChange binlogPath ]
                )

                ctx.attrs [ yield! attrs ]

                let defaultMargin = 8
                let buttonWidth = 100

                let stackPanelAttrs = [
                    StackPanel.margin defaultMargin
                    StackPanel.dock Dock.Top
                    StackPanel.spacing 4
                    StackPanel.orientation Orientation.Horizontal
                ]

                DockPanel.create [
                    DockPanel.margin defaultMargin
                    DockPanel.lastChildFill false
                    DockPanel.children [
                        StackPanel.create [
                            yield! stackPanelAttrs
                            StackPanel.children [
                                Button.create [
                                    Button.content (centerText "Load Binlog")
                                    Button.width buttonWidth
                                    Button.onClick (fun e ->
                                        task {
                                            match! openDllPicker ctx.control with
                                            | Some picked -> binlogPath.Set(picked.Path.AbsolutePath)
                                            | None -> ()
                                        }
                                        |> ignore)
                                ]
                                TextBox.create [ TextBox.text binlogPath.Current ]
                            ]
                        ]
                        StackPanel.create [
                            yield! stackPanelAttrs
                            StackPanel.children [
                                Button.create [
                                    Button.content (centerText "Load Project")
                                    Button.width buttonWidth
                                    Button.isEnabled (Option.isSome selectedProj.Current)
                                    Button.onClick (fun _ ->
                                        match selectedProj.Current with
                                        | Some proj -> onLoadProject proj
                                        | None -> ())
                                ]
                                ComboBox.create [
                                    ComboBox.dock Dock.Top
                                    ComboBox.dataItems projs.Current
                                    ComboBox.itemTemplate (
                                        DataTemplateView<ProjArgsInfo>.create (fun p ->
                                            TextBlock.create [ TextBlock.text p.Name ])
                                    )
                                    ComboBox.onSelectedItemChanged (function
                                        | :? ProjArgsInfo as p -> selectedProj.Set(Some p)
                                        | _ -> selectedProj.Set None)
                                ]
                            ]
                        ]
                    ]
                ]
        )

module TimeSpanUpDown =
    open System
    open System.Text.RegularExpressions
    open System.ComponentModel.DataAnnotations
    open Avalonia.Controls
    open Avalonia.FuncUI.DSL
    open Avalonia.Layout

    [<Literal>]
    let TimeSpanFormat = @"hh\:mm\:ss\.fff"

    let allowedPatterns = Regex("[\d:\.]")

    let create id value spinTickTime content attrs =
        Component.create (
            $"timeSpanUpDown-%s{id}",
            fun ctx ->
                let value: IWritable<TimeSpan> = ctx.usePassed value
                let spinTickTime: IWritable<TimeSpan> = ctx.usePassed (spinTickTime, false)

                let errors: IWritable<ValidationResult list> = ctx.useState []
                let hasErrors = not errors.Current.IsEmpty

                ctx.attrs [ yield! attrs ]

                ButtonSpinner.create [
                    ButtonSpinner.onSpin (fun e ->
                        match e.Direction with
                        | SpinDirection.Increase -> value.Current + spinTickTime.Current |> value.Set
                        | SpinDirection.Decrease -> value.Current - spinTickTime.Current |> value.Set
                        | _ -> ())
                    ButtonSpinner.content (
                        StackPanel.create [
                            StackPanel.orientation Orientation.Horizontal
                            StackPanel.spacing 4
                            StackPanel.children [
                                content
                                TextBox.create [
                                    TextBox.text $"{value.Current.ToString TimeSpanFormat}"
                                    TextBox.hasErrors hasErrors
                                    TextBox.onTextInput (fun e ->
                                        if not (allowedPatterns.IsMatch e.Text) then
                                            e.Handled <- true)
                                    TextBox.onTextChanged (fun text ->
                                        try
                                            TimeSpan.Parse text |> value.Set
                                            errors.Set []
                                        with e ->
                                            errors.Set [ ValidationResult e.Message ])
                                ]
                            ]
                        ]
                    )
                ]
        )

module View =
    open System
    open System.Threading

    open Avalonia.Controls
    open Avalonia.Layout

    open Avalonia.FuncUI.DSL
    open Avalonia.FuncUI.Types

    open Avalonia.FuncUI.LiveView.Types.Analyzer
    type Model = Types.LiveView.Model

    type IComponentContext with

        member inline this.useMap(value: IReadable<'t>, mapper, ?renderOnChange) =
            let renderOnChange = defaultArg renderOnChange true
            let value' = this.useStateLazy ((fun _ -> mapper value.Current), renderOnChange)
            this.useEffect ((fun () -> mapper value.Current |> value'.Set), [ EffectTrigger.AfterChange value ])
            value'

        member inline this.usePassedClient(client: IWritable<IAnalyzerClient>) =

            let client = this.usePassed client

            this.useEffect (
                (fun () ->
                    if not client.Current.IsConnected then
                        let cts = new CancellationTokenSource()

                        backgroundTask {
                            do! client.Current.ConnectAsync cts.Token
                            client.Current.StartReceive()

                            this.forceRender ()
                        }
                        |> ignore

                        this.trackDisposable client
                        this.trackDisposable cts),
                [ EffectTrigger.AfterInit ]
            )

            client

    let create setting client model currentProj =
        Component(fun ctx ->
            let setting = ctx.usePassed (setting, false)

            let model: IWritable<Model> = ctx.usePassed (model, false)
            let autoUpdate = ctx.useMap (setting, (fun s -> s.autoUpdate), false)

            ctx.useEffect (
                (fun () ->
                    model.Set {
                        model.Current with
                            autoUpdate = autoUpdate.Current
                    }),
                [ EffectTrigger.AfterChange autoUpdate ]
            )

            let enablePreviewItemViewBackground =
                ctx.useMap (setting, (fun s -> s.enablePreviewItemViewBackground), false)

            let autoUpdateDebounceTime =
                ctx.useMap (setting, (fun s -> s.autoUpdateDebounceTime), false)

            let spinTickTime = ctx.useState (TimeSpan.FromMilliseconds 100, false)

            let selectedThemeIdx =
                Themes.allCases |> Array.findIndex (fun t -> t = setting.Current.theme)

            let client = ctx.usePassedClient client

            let currentProj = ctx.usePassed currentProj

            let settingViews: IView list = [
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
                ComboBox.create [
                    ComboBox.dataItems BuildinThemeVariantCase.allCases
                    ComboBox.selectedItem setting.Current.buildinThemeVariant
                    ComboBox.onSelectedItemChanged (function
                        | :? BuildinThemeVariantCase as newValue when newValue <> setting.Current.buildinThemeVariant ->
                            setting.Set {
                                setting.Current with
                                    buildinThemeVariant = newValue
                            }
                        | _ -> ())
                ]
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
                TimeSpanUpDown.create
                    "preview-app"
                    autoUpdateDebounceTime
                    spinTickTime
                    (TextBlock.create [
                        TextBlock.verticalAlignment VerticalAlignment.Center
                        TextBlock.text "debounce time"
                    ])
                    []

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
            ]

            DockPanel.create [
                DockPanel.children [
                    TextBlock.create [
                        TextBlock.dock Dock.Bottom
                        TextBlock.margin 8
                        TextBlock.text $"{model.Current.logMessage}"
                    ]
                    StackPanel.create [
                        StackPanel.dock Dock.Bottom
                        StackPanel.margin 8
                        StackPanel.spacing 8
                        StackPanel.orientation Orientation.Horizontal
                        StackPanel.children settingViews
                    ]
                    match currentProj.Current with
                    | _ when not client.Current.IsConnected -> TextBlock.create [ TextBlock.text "not Connected.." ]
                    | None ->
                        SelectProjectView.create "preview-app" (Some >> currentProj.Set) [

                        ]
                    | Some proj ->
                        LiveView.create setting client proj model "preview-app" [

                        ]
                ]
            ])

type MainWindow(setting, client, model, currentProj) as this =
    inherit HostWindow(Title = "LiveView", Width = 800, Height = 600)

    let view = View.create setting client model currentProj

    do
        this.Content <- view
#if DEBUG
        this.AttachDevTools()
#endif