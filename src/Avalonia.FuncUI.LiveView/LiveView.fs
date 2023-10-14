namespace Avalonia.FuncUI.LiveView

open Avalonia
open Avalonia.Controls
open Avalonia.Media
open Avalonia.Layout
open Avalonia.FuncUI
open Avalonia.FuncUI.Types
open Avalonia.FuncUI.DSL

open Avalonia.FuncUI.LiveView.Types
open Avalonia.FuncUI.LiveView.Protocol
open Avalonia.FuncUI.Hosts

[<AutoOpen>]
module StyledElement =
    open Avalonia.Styling

    type StyledElement with

        /// 参考:
        static member styles(styleSeq: list<(Selector -> Selector) * list<IAttr<'a>>>) =
            let styles = Styles()

            for (selector, setters) in styleSeq do
                let s = Style(fun x -> selector x)

                for attr in setters do
                    match attr.Property with
                    | ValueSome p ->
                        match p.Accessor with
                        | InstanceProperty x -> failwith "Can't support instance property"
                        | AvaloniaProperty x -> s.Setters.Add(Setter(x, p.Value))
                    | ValueNone -> ()

                styles.Add s

            StyledElement.styles styles

module FilePicker =
    open Avalonia.Platform.Storage

    let openSimgleFileAsync ctr title filters =
        task {
            let provier = TopLevel.GetTopLevel(ctr).StorageProvider
            let! location = provier.TryGetWellKnownFolderAsync(WellKnownFolder.Documents)

            let! result =
                provier.OpenFilePickerAsync(
                    FilePickerOpenOptions(
                        Title = title,
                        SuggestedStartLocation = location,
                        AllowMultiple = false,
                        FileTypeFilter = filters
                    )
                )

            match List.ofSeq result with
            | [ picked ] -> return Some picked
            | _ -> return None
        }

    let openProjectOrSolutionFileAsync ctr =
        openSimgleFileAsync ctr "Open Project or Solution" [
            FilePickerFileType(
                "MsBuild Project Or Solution File",
                Patterns = [ "*.fsproj"; "*.csproj"; "*.sln" ],
                AppleUniformTypeIdentifiers = [ "public.data" ]
            )
        ]

module ProjectInfoSelecter =
    let view onOpenSelectProject =
        Component.create (
            "project-info-selecter",
            fun ctx ->

                let loadedProjects = ctx.useState<list<ProjectInfo>> []

                let projectInfo = ctx.useState<ProjectInfo option> None

                let openFile _ =
                    backgroundTask {
                        match! FilePicker.openProjectOrSolutionFileAsync ctx.control with
                        | Some picked ->
                            MSBuildLocator.registerIfNotRegistered ()

                            if picked.Name.EndsWith(".sln") then
                                ProjectInfo.loadFromSlnFile picked.Path.LocalPath
                            else
                                ProjectInfo.loadFromProjFile picked.Path.LocalPath
                            |> List.ofSeq
                            |> function
                                | [ Ok info ] -> onOpenSelectProject info
                                | results ->
                                    results
                                    |> List.choose (function
                                        | Ok x -> Some x
                                        | Error _ -> None)
                                    |> loadedProjects.Set
                        | None -> ()
                    }
                    |> ignore

                let selectedProjectView =
                    StackPanel.create [
                        StackPanel.orientation Orientation.Horizontal
                        StackPanel.children [
                            Button.create [
                                Button.margin 4
                                Button.content "Open project or load solution."
                                Button.onClick openFile
                            ]
                        ]
                    ]

                let loadedProjectsView =
                    ListBox.create [
                        ListBox.margin 4
                        ListBox.isEnabled (List.isEmpty loadedProjects.Current |> not)
                        ListBox.viewItems [
                            match loadedProjects.Current with
                            | [] ->
                                ListBoxItem.create [
                                    ListBoxItem.isEnabled false
                                    ListBoxItem.content "No project loaded."
                                ]
                            | loadedProjects ->
                                for x in loadedProjects do
                                    ListBoxItem.create [
                                        ListBoxItem.content $"{x.Name} - {x.TargetFramework}"
                                        ListBoxItem.onDoubleTapped (fun _ -> onOpenSelectProject x)
                                    ]
                        ]
                        ListBox.onSelectedIndexChanged (fun x ->
                            if x < 0 then
                                projectInfo.Set None
                            else
                                List.tryItem x loadedProjects.Current |> projectInfo.Set)
                    ]

                let openSelectedProjectButton =
                    Button.create [
                        Button.isEnabled (projectInfo.Current |> Option.isSome)
                        Button.margin 4
                        Button.content "Open selected project"
                        Button.onClick (fun _ -> Option.iter onOpenSelectProject projectInfo.Current)
                    ]

                StackPanel.create [
                    StackPanel.orientation Orientation.Vertical
                    StackPanel.children [ selectedProjectView; loadedProjectsView; openSelectedProjectButton ]
                ]
        )

    [<LivePreview>]
    let filePickerView () = view ignore


module LiveView =
    open System.Reflection

    open Avalonia.Styling
    open Avalonia.Controls.Primitives
    open Avalonia.FuncUI.VirtualDom

    open Avalonia.FuncUI.LiveView.Types.Watcher

    let evalPreviewFunc (fn: unit -> obj) =
        try
            match fn () with
            | :? IView as view -> view
            | :? Control as view -> ContentControl.create [ ContentControl.content view ]
            | other -> TextBlock.create [ TextBlock.text $"%A{other}" ]
            |> VirtualDom.create
        with :? TargetInvocationException as e ->
            StackPanel.create [
                StackPanel.children [
                    TextBlock.create [
                        TextBlock.foreground Brushes.Red
                        TextBlock.text $"%A{e.InnerException.GetType()}"
                    ]
                    TextBlock.create [
                        TextBlock.foreground Brushes.Red
                        TextBlock.text $"%s{e.InnerException.Message}"
                    ]
                    TextBlock.create [
                        TextBlock.text $"%s{e.InnerException.StackTrace}"
                        TextBlock.textWrapping TextWrapping.WrapWithOverflow
                    ]
                ]
            ]
            |> VirtualDom.create

    let mainView (watcher: IWatcherService) (server: Protocol.IServer) =

        Component.create (
            "live-view-main",
            fun ctx ->
                let requestEvalAsync msg =
                    backgroundTask { watcher.RequestEval msg } |> ignore

                let logs = ctx.useState<LogMessage list> []

                ctx.useEffect (
                    (fun _ ->
                        server.OnLogMessage
                        |> Event.merge watcher.OnLogMessage
                        |> Event.add (fun x -> logs.Set(x :: logs.Current))),
                    [ EffectTrigger.AfterInit ]
                )

                let msgs = ctx.useState<Msg list> []

                ctx.useEffect (
                    (fun _ -> server.OnMsgReceived |> Event.add (fun x -> msgs.Set(x :: msgs.Current))),
                    [ EffectTrigger.AfterInit ]
                )

                let evalResults = ctx.useState<list<string * (unit -> obj)>> []
                let evalExn = ctx.useState<exn option> None
                let evalWarnings = ctx.useState<Diagnostic list> ([], false)

                ctx.useEffect (
                    (fun _ ->
                        watcher.OnEvalResult
                        |> Event.add (function
                            | Ok { msg = msg
                                   previewFuncs = previewFuncs
                                   warnings = warnings } ->
                                evalWarnings.Set warnings
                                evalResults.Set previewFuncs
                            | Error { msg = msg
                                      error = ex
                                      warnings = warnings } ->
                                evalWarnings.Set warnings
                                evalExn.Set(Some ex))),
                    [ EffectTrigger.AfterInit ]
                )

                let autoEval = ctx.useState true

                ctx.useEffect (
                    (fun _ ->
                        server.OnMsgReceived
                        |> Event.filter (fun _ -> autoEval.Current)
                        |> Event.add requestEvalAsync),
                    [ EffectTrigger.AfterInit ]
                )

                let showEvalText = ctx.useState false
                let rootGridName = "live-preview-root"

                ctx.attrs [
                    Component.styles [
                        (fun (x: Selector) -> x.Name(rootGridName).Child()),
                        [ Layoutable.margin 8; Layoutable.verticalAlignment VerticalAlignment.Center ]
                    ]
                ]

                Grid.create [
                    Grid.name rootGridName
                    Grid.rowDefinitions "Auto,*,4,*,Auto"
                    Grid.columnDefinitions "Auto,*,Auto"
                    Grid.children [
                        CheckBox.create [
                            CheckBox.row 0
                            CheckBox.column 0
                            CheckBox.content "Show EvalText"
                            CheckBox.isChecked showEvalText.Current
                            CheckBox.onChecked (fun _ -> showEvalText.Set true)
                            CheckBox.onUnchecked (fun _ -> showEvalText.Set false)
                        ]
                        TextBox.create [
                            match msgs.Current with
                            | { Contents = contents } :: _ when showEvalText.Current ->
                                TextBox.row 1
                                TextBox.column 0
                                TextBox.columnSpan 3
                                TextBox.acceptsReturn true
                                TextBox.textWrapping TextWrapping.Wrap
                                TextBox.text (String.concat "" contents)

                                if not <| List.isEmpty evalWarnings.Current then
                                    evalWarnings.Current |> List.map (fun x -> box $"%A{x}") |> TextBox.errors
                            | _ -> TextBox.isVisible false
                        ]

                        GridSplitter.create [
                            if showEvalText.Current then
                                GridSplitter.row 2
                                GridSplitter.column 0
                                GridSplitter.columnSpan 3
                            else
                                GridSplitter.isVisible false
                        ]
                        ScrollViewer.create [
                            if showEvalText.Current then
                                ScrollViewer.row 3
                            else
                                ScrollViewer.row 1
                                ScrollViewer.rowSpan 3
                            ScrollViewer.margin (4, 4)
                            ScrollViewer.padding (4, 0)
                            ScrollViewer.verticalAlignment VerticalAlignment.Top
                            ScrollViewer.column 0
                            ScrollViewer.columnSpan 3
                            ScrollViewer.column 0
                            ScrollViewer.content (
                                DockPanel.create [
                                    DockPanel.children [
                                        for (name, content) in evalResults.Current do
                                            Border.create [
                                                Border.dock Dock.Top
                                                Border.borderThickness 2
                                                Border.child (
                                                    Grid.create [
                                                        Grid.rowDefinitions "Auto,Auto,Auto"
                                                        Grid.children [
                                                            TextBlock.create [
                                                                TextBlock.row 0
                                                                TextBlock.fontSize 20
                                                                TextBlock.margin 4
                                                                TextBlock.fontWeight FontWeight.SemiBold
                                                                TextBlock.text name
                                                            ]
                                                            Border.create [ Border.row 1; Border.height 2 ]
                                                            Border.create [
                                                                Border.row 2
                                                                Border.child (evalPreviewFunc content)
                                                            ]
                                                        ]
                                                    ]
                                                )
                                            ]
                                    ]
                                ]
                            )
                        ]
                        CheckBox.create [
                            CheckBox.row 4
                            CheckBox.column 0
                            CheckBox.margin 4
                            CheckBox.content "Auto EvalText"
                            CheckBox.isChecked autoEval.Current
                            CheckBox.onChecked (fun _ -> autoEval.Set true)
                            CheckBox.onUnchecked (fun _ -> autoEval.Set false)
                        ]
                        Button.create [
                            Button.row 4
                            TextBox.column 1
                            Button.horizontalAlignment HorizontalAlignment.Left
                            Button.content "eval manualy"
                            Button.onClick (fun _ ->
                                match msgs.Current with
                                | headMsg :: _ -> requestEvalAsync headMsg
                                | _ -> ())
                        ]
                        TextBlock.create [
                            TextBlock.row 5
                            TextBlock.column 2
                            TextBlock.maxLines 1
                            TextBlock.multiline false
                            TextBlock.horizontalScrollBarVisibility ScrollBarVisibility.Disabled
                            TextBlock.text (
                                match logs.Current with
                                | [] -> ""
                                | LogInfo msg :: _ -> $"Info: {msg}"
                                | LogDebug msg :: _ -> $"Debug: {msg}"
                                | LogError msg :: _ -> $"Error: {msg}"
                            )
                        ]
                    ]
                ]
        )

    let view (watcher: IWatcherService) (server: Protocol.IServer) =

        Component(fun ctx ->
            let projectInfo = ctx.useState watcher.WatchingProjectInfo

            match projectInfo.Current with
            | None ->
                ProjectInfoSelecter.view (fun x ->
                    watcher.Watch x
                    projectInfo.Set(Some x))
            | Some info ->
                (Window.GetTopLevel(ctx.control) :?> Window).Title <- $"LiveView - {info.Name}"
                mainView watcher server)


type LiveViewWindow() =
    inherit HostWindow(Title = "LiveView", Width = 800, Height = 600)


    let watcher = Watcher.createService ()

    let client = Server.create ()

    do
        base.Content <- LiveView.view watcher client
        base.AttachDevTools()

    override _.OnClosed e =
        Disposable.dispose watcher
        Disposable.dispose client
        base.OnClosed e