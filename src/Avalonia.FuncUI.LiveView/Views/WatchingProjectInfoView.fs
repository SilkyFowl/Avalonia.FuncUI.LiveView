module Avalonia.FuncUI.LiveView.Views.WatchingProjectInfoView

open Avalonia.Controls
open Avalonia.Layout
open Avalonia.Media

open Avalonia.FuncUI
open Avalonia.FuncUI.DSL

open Avalonia.FuncUI.LiveView
open Avalonia.FuncUI.LiveView.Types
open Avalonia.FuncUI.LiveView.Types.Watcher

type Props =
    { Watcher: IReadable<IWatcherService>
      Server: IReadable<Protocol.IServer>
      Log: LogMessage -> unit }

module ShowEvalTextCheckBox =
    let create (check: IWritable<bool>) attrs =
        CheckBox.create [
            yield! attrs
            CheckBox.content "Show EvalText"
            CheckBox.isChecked check.Current
            CheckBox.onChecked (fun _ -> check.Set true)
            CheckBox.onUnchecked (fun _ -> check.Set false)
        ]

module AutoEvalCheckBox =
    let create (check: IWritable<bool>) attrs =
        CheckBox.create [
            yield! attrs
            CheckBox.content "Auto EvalText"
            CheckBox.isChecked check.Current
            CheckBox.onChecked (fun _ -> check.Set true)
            CheckBox.onUnchecked (fun _ -> check.Set false)
        ]

module EvalTextView =
    let create (msgs: Msg list) showEvalText attrs =
        TextBox.create [
            yield! attrs
            match msgs with
            | { Contents = contents } :: _ when showEvalText ->
                TextBox.acceptsReturn true
                TextBox.textWrapping TextWrapping.Wrap
                TextBox.text (String.concat "" contents)
            | _ -> TextBox.isVisible false
        ]

module PreviewFuncsView =
    let create (previewFuncs: PreviewFunc list) attrs =
        ScrollViewer.create [
            yield! attrs
            ScrollViewer.content (
                DockPanel.create [
                    DockPanel.children [
                        for result in previewFuncs do
                            PreviewFuncView.create result [ Border.dock Dock.Top; Border.borderThickness 2 ]
                    ]
                ]
            )
        ]

module EvalButton =
    let create (requestEval: unit -> unit) attrs =
        Button.create [
            yield! attrs
            Button.content "eval manualy"
            Button.onClick (fun _ -> requestEval ())
        ]

let create id (props: Props) attrs =
    Component.create (
        $"live-view-watch-project-{id}",
        fun ctx ->
            let server = ctx.usePassedRead (props.Server, false)
            let watcher = ctx.usePassedRead (props.Watcher, false)
            let log = props.Log


            let showEvalText = ctx.useState (false, true)
            let autoEval = ctx.useState (true, false)

            let msgs = ctx.useState<Msg list> []

            let evalResults = ctx.useState<list<PreviewFunc>> []
            let evalExn = ctx.useState<exn option> None
            let evalDiagnostics = ctx.useState<Diagnostic list> ([], false)

            ctx.useEffect (
                (fun _ ->
                    Disposables.create [
                        server.Current.OnMsgReceived
                        |> Observable.subscribe (fun x -> msgs.Set(x :: msgs.Current))
                        watcher.Current.OnEvalResult
                        |> Observable.subscribe (function
                            | Ok { msg = msg
                                   previewFuncs = previewFuncs
                                   warnings = warnings } ->
                                evalDiagnostics.Set warnings
                                evalResults.Set previewFuncs
                            | Error { msg = msg
                                      error = ex
                                      warnings = warnings } ->
                                evalDiagnostics.Set warnings
                                evalExn.Set(Some ex))
                    ]),
                [ EffectTrigger.AfterInit ]
            )

            let tryEvalAsync () =
                match msgs.Current with
                | headMsg :: _ -> backgroundTask { watcher.Current.RequestEval headMsg } |> ignore
                | _ -> ()

            ctx.useEffect (
                (fun () ->
                    if autoEval.Current then
                        tryEvalAsync ()),
                [ EffectTrigger.AfterChange msgs ]
            )

            ctx.attrs [ yield! attrs ]

            Grid.create [
                Grid.rowDefinitions "Auto,*,4,*,Auto,Auto"
                Grid.columnDefinitions "Auto,*,Auto"
                Grid.children [
                    ShowEvalTextCheckBox.create showEvalText [ CheckBox.row 0; CheckBox.column 0 ]
                    EvalTextView.create msgs.Current showEvalText.Current [
                        if showEvalText.Current then
                            TextBox.row 1
                            TextBox.column 0
                            TextBox.columnSpan 3
                    ]
                    GridSplitter.create [
                        if showEvalText.Current then
                            GridSplitter.row 2
                            GridSplitter.column 0
                            GridSplitter.columnSpan 3
                        else
                            GridSplitter.isVisible false
                    ]
                    PreviewFuncsView.create evalResults.Current [
                        ScrollViewer.margin (4, 4)
                        ScrollViewer.padding (4, 0)
                        ScrollViewer.verticalAlignment VerticalAlignment.Top
                        if showEvalText.Current then
                            ScrollViewer.row 3
                        else
                            ScrollViewer.row 1
                            ScrollViewer.rowSpan 3
                        ScrollViewer.column 0
                        ScrollViewer.columnSpan 3
                    ]
                    AutoEvalCheckBox.create autoEval [ CheckBox.row 4; CheckBox.column 0; CheckBox.margin 4 ]
                    EvalButton.create tryEvalAsync [
                        Button.row 4
                        TextBox.column 1
                        Button.horizontalAlignment HorizontalAlignment.Left
                    ]
                    DiagnosticsView.create evalDiagnostics.Current [
                        Expander.row 5
                        Expander.column 0
                        Expander.columnSpan 3
                    ]
                ]
            ]
    )