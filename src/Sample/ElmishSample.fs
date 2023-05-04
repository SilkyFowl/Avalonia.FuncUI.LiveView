/// 参考：https://github.com/fsprojects/Avalonia.FuncUI/blob/master/src/Avalonia.FuncUI.ControlCatalog/Views/Tabs/TextBoxDemo.fs
module Sample.ElmishSample.DefineDUInFile

open Avalonia.Controls
open Avalonia.Layout
open Avalonia.FuncUI
open Avalonia.FuncUI.DSL
open Avalonia.FuncUI.Elmish
open Avalonia.FuncUI.Builder
open Avalonia.FuncUI.LiveView.Core.Types

type State = { watermark: string }

let init = { watermark = "" }

/// LivePreviewを行いたいコードで判別共用体を定義する際は、値のないケースラベルを1つ以上含めないといけない。
/// 全てのケースラベルに値が設定されているとAnalyzerが失敗する。
type Msg =
    | SetWatermark of string
    | UnSetWatermark


let update msg state =
    match msg with
    | SetWatermark test -> { state with watermark = test }
    | UnSetWatermark -> { state with watermark = "" }

let view state dispatch =
    StackPanel.create [
        StackPanel.spacing 10.0
        StackPanel.children [
            TextBox.create [
                TextBox.watermark state.watermark
                TextBox.horizontalAlignment HorizontalAlignment.Stretch
            ]
            Button.create [
                Button.content "Set Watermark"
                Button.background "DarkBlue"
                Button.onClick (fun _ -> dispatch (SetWatermark "test"))
                Button.horizontalAlignment HorizontalAlignment.Stretch
            ]

            Button.create [
                Button.content "Unset Watermark"
                Button.onClick (fun _ -> dispatch (UnSetWatermark))
                Button.horizontalAlignment HorizontalAlignment.Stretch
            ]
        ]
    ]

type Host() as this =
    inherit Hosts.HostControl()

    do
        Elmish.Program.mkSimple (fun () -> init) update view
        |> Program.withHost this
        |> Program.runWithAvaloniaSyncDispatch ()

[<LivePreview>]
let preview () = ViewBuilder.Create<Host> []