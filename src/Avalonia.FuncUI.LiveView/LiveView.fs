namespace Avalonia.FuncUI.LiveView

open System
open System.IO
open Avalonia
open Avalonia.Controls
open Avalonia.Media
open Avalonia.Layout
open Avalonia.FuncUI
open Avalonia.FuncUI.Types
open Avalonia.FuncUI.DSL

open Avalonia.FuncUI.LiveView.Types
open Avalonia.FuncUI.LiveView.Protocol

type StateStore =
    { Msg: IWritable<Msg>
      EvalResult: IWritable<list<string * Control>>
      EvalWarings: IWritable<obj[]>
      Status: IWritable<LogMessage>
      TempScriptFileInfo: FileInfo }

module StateStore =
    open Avalonia.FuncUI.VirtualDom
    let private fsiSession = FsiSession.create ()

    /// `state`の情報に基づいてEvalする。
    let evalInteraction state =
        FsiSession.evalInteraction
            fsiSession
            state.Status.Set
            state.TempScriptFileInfo
            state.Msg.Current
            state.EvalWarings
            state.EvalResult

    /// `StateStore`を初期化する。
    let init () =
        let initText =
            $"""
module Counter =
    open Avalonia.FuncUI
    open Avalonia.Controls
    open Avalonia.FuncUI.DSL
    open Avalonia.Layout
    open Avalonia.Media

    let view =
        Component.create("Counter",fun ctx ->
            let state = ctx.useState 0
            DockPanel.create [
                DockPanel.verticalAlignment VerticalAlignment.Center
                DockPanel.horizontalAlignment HorizontalAlignment.Center
                DockPanel.children [
                    Button.create [
                        Button.width 64
                        Button.horizontalAlignment HorizontalAlignment.Center
                        Button.horizontalContentAlignment HorizontalAlignment.Center
                        Button.content "Reset"
                        Button.onClick (fun _ -> state.Set 0)
                        Button.dock Dock.Bottom
                    ]
                    Button.create [
                        Button.width 64
                        Button.horizontalAlignment HorizontalAlignment.Center
                        Button.horizontalContentAlignment HorizontalAlignment.Center
                        Button.content "-"
                        Button.onClick (fun _ -> state.Current - 1 |> state.Set)
                        Button.dock Dock.Bottom
                    ]
                    Button.create [
                        Button.width 64
                        Button.horizontalAlignment HorizontalAlignment.Center
                        Button.horizontalContentAlignment HorizontalAlignment.Center
                        Button.content "+"
                        Button.onClick (fun _ -> state.Current + 1 |> state.Set)
                        Button.dock Dock.Bottom
                    ]
                    TextBlock.create [
                        TextBlock.dock Dock.Top
                        TextBlock.foreground Brushes.White
                        TextBlock.fontSize 48.0
                        TextBlock.horizontalAlignment HorizontalAlignment.Center
                        TextBlock.text (string state.Current)
                    ]
                ]
            ]
        )
            """

        let initResult =
            TextBlock.create [
                TextBlock.verticalAlignment VerticalAlignment.Center
                TextBlock.horizontalAlignment HorizontalAlignment.Center
                TextBlock.text "Results are displayed here."
            ]
            |> VirtualDom.create

        { Msg = new State<_>(Msg.create "init" [| initText |])
          EvalResult = new State<_>([ "init", initResult ])
          EvalWarings = new State<_>([||])
          Status = new State<_>(LogInfo "")
          TempScriptFileInfo = Path.ChangeExtension(Path.GetTempFileName(), "fsx") |> FileInfo }

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

module LiveView =

    open Avalonia.Styling

    let view shared client =

        let buttonBackground =
            Application.Current.FindResource "ButtonBackground" :?> IBrush

        Component(fun ctx ->

            // sharedの購読
            let evalText =
                ctx.usePassedRead (shared.Msg |> State.readMap (fun m -> m.Contents), true)

            let evalResult = ctx.usePassed (shared.EvalResult)
            let evalWarnings = ctx.usePassed (shared.EvalWarings)
            let status = ctx.usePassed shared.Status

            /// `true`ならEvalTextが更新されたら自動でEvalする。
            let autoEval = ctx.useState true
            /// `true`ならEvalTextを表示する。
            let showEvalText = ctx.useState false

            ctx.trackDisposable client

            /// Evalを実行する。
            let evalInteractionAsync _ =
                StateStore.evalInteraction shared |> ignore

            ctx.useEffect (
                (fun _ ->
                    evalText.Observable
                    |> Observable.subscribe (fun _ ->
                        if autoEval.Current then
                            evalInteractionAsync ())),
                [ EffectTrigger.AfterInit ]
            )

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
                        if showEvalText.Current then
                            TextBox.row 1
                            TextBox.column 0
                            TextBox.columnSpan 3
                            TextBox.acceptsReturn true
                            TextBox.textWrapping TextWrapping.Wrap
                            TextBox.text (String.concat "" evalText.Current)

                            if not <| Array.isEmpty evalWarnings.Current then
                                TextBox.errors evalWarnings.Current
                        else
                            TextBox.isVisible false
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
                                    for (name, content) in evalResult.Current do
                                        Border.create [
                                            Border.dock Dock.Top
                                            Border.borderThickness 2
                                            Border.borderBrush buttonBackground
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
                                                        Border.create [
                                                            Border.row 1
                                                            Border.height 2
                                                            Border.background buttonBackground
                                                        ]
                                                        Border.create [ Border.row 2; Border.child content ]
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
                        Button.onClick evalInteractionAsync
                    ]
                    TextBlock.create [ TextBlock.row 5; TextBlock.column 2; TextBlock.text $"{status.Current}" ]
                ]
            ])

type LiveViewWindow() =
    inherit HostWindow(Title = "LiveView", Width = 800, Height = 600)

    /// `Interactive`のStore。
    /// ※本来、Storeはアプリケーション一つだけであるのが望ましい。
    let shared = StateStore.init ()

    let client = Server.create ()

    do
        client.OnLogMessage |> Event.add shared.Status.Set

        client.OnMsgReceived |> Event.add shared.Msg.Set

        base.Content <- LiveView.view shared client
        base.AttachDevTools()