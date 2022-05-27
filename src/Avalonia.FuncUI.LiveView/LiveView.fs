namespace Avalonia.FuncUI.LiveView

open System
open Avalonia.Controls
open Avalonia.Media
open Avalonia.Layout
open Avalonia.FuncUI
open Avalonia.FuncUI.DSL

open Avalonia.FuncUI.LiveView.Core.Types
open Avalonia.FuncUI.LiveView.MessagePack

type StateStore =
    { EvalText: IWritable<string>
      EvalResult: IWritable<obj>
      EvalWarings: IWritable<obj []>
      Status: IWritable<LogMessage> }

module StateStore =
    open System.Text.RegularExpressions
    let private fsiSession = FsiSession.create ()

    /// この文字列が含まれていたらEvalする。
    [<Literal>]
    let MatchText = "//#funcuianalyzer"

    /// Evalするかを判定する。
    let isCollectText text =
        not <| String.IsNullOrEmpty text
        && Regex.IsMatch(text, MatchText)

    /// `state`の情報に基づいてEvalする。
    let evalInteraction state =
        FsiSession.evalInteraction
            isCollectText
            fsiSession
            state.Status.Set
            state.EvalText
            state.EvalWarings
            state.EvalResult

    /// `evalInteraction`の非同期版。
    let evalInteractionAsync state _ =
        async { evalInteraction state }
        |> Async.StartImmediate

    /// `StateStore`を初期化する。
    let init () =
        let initText =
            $"""
{MatchText}
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

Counter.view
            """

        let initResult =
            TextBlock.create [
                TextBlock.verticalAlignment VerticalAlignment.Center
                TextBlock.horizontalAlignment HorizontalAlignment.Center
                TextBlock.text "Results are displayed here."
            ]
            |> VirtualDom.VirtualDom.create

        { EvalText = new State<_>(initText)
          EvalResult = new State<_>(box initResult)
          EvalWarings = new State<_>([||])
          Status = new State<_>(LogInfo "") }

open Avalonia.FuncUI.Hosts

module LiveView =
    let view shared client =
        Component (fun ctx ->

            // sharedの購読
            let evalText = ctx.usePassed (shared.EvalText, false)
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
                StateStore.evalInteractionAsync shared ()

            ctx.useEffect (
                (fun _ ->
                    evalText.Observable
                    |> Observable.subscribe (fun _ ->
                        if autoEval.Current then
                            evalInteractionAsync ())),
                [ EffectTrigger.AfterInit ]
            )

            Grid.create [
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
                        TextBox.isVisible showEvalText.Current
                        if showEvalText.Current then
                            TextBox.row 1
                            TextBox.column 0
                            TextBox.columnSpan 3
                            TextBox.acceptsReturn true
                            TextBox.textWrapping TextWrapping.Wrap
                            TextBox.text evalText.Current
                            TextBox.onTextChanged evalText.Set

                            if not <| Array.isEmpty evalWarnings.Current then
                                TextBox.errors evalWarnings.Current
                    ]
                    GridSplitter.create [
                        GridSplitter.isVisible showEvalText.Current
                        if showEvalText.Current then
                            GridSplitter.row 2
                            GridSplitter.column 0
                            GridSplitter.columnSpan 3
                    ]
                    ContentControl.create [
                        if showEvalText.Current then
                            Border.row 3
                        else
                            Border.row 1
                            Border.rowSpan 3
                        TextBox.column 0
                        TextBox.columnSpan 3
                        TextBox.column 0
                        ContentControl.content evalResult.Current
                    ]
                    CheckBox.create [
                        CheckBox.row 4
                        CheckBox.column 0
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
                    TextBlock.create [
                        TextBlock.row 5
                        TextBlock.column 2
                        TextBlock.text $"{status.Current}"
                    ]
                ]
            ])

type LiveViewWindow() =
    inherit HostWindow(Title = "LiveView", Width = 800, Height = 600)
    // open Ava
    /// `Interactive`のStore。
    /// ※本来、Storeはアプリケーション一つだけであるのが望ましい。
    let shared = StateStore.init ()

    let client =
        Client.initClient shared.Status.Set Settings.iPAddress Settings.port shared.EvalText.Set

    do base.Content <- LiveView.view shared client