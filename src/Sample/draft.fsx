module Draft
#if !PREVIEW
#I "bin/Debug/net6.0"
#r "Avalonia.Desktop"
#r "Avalonia.Controls"
#r "Avalonia.Visuals"
#r "Avalonia.Input"
#r "Avalonia.Interactivity"
#r "Avalonia.Layout"
#r "Avalonia.Styling"
#r "Avalonia.Animation"
#r "Avalonia.Base"
#r "Avalonia.FuncUI"
#r "Avalonia.FuncUI.DSL"
#r "Sample"
#r "Avalonia.FuncUI.LiveView.Core"
#endif

open Avalonia.Controls
open Avalonia.Media
open Avalonia.Layout
open Avalonia.FuncUI
open Avalonia.FuncUI.DSL
open Avalonia.FuncUI.LiveView.Core.Types

open Sample

/// Script demo
/// TODO: 更新速度が遅くなる理由を解明、解決する
/// 今わかること
/// - view関数にlambda式が混ざると遅くなる。
///     - このファイルをしばらく編集してから他のファイルに移ったらしばらく固まった
///       ヒントになるか？
module Counter =

    let view name attrs fNum numState =
        Component.create (
            name,
            fun ctx ->
                let rnd = System.Random()
                let state = ctx.usePassed numState

                ctx.attrs attrs

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
                            Button.onClick (fun _ -> state.Current - rnd.Next(1,10) |> state.Set)
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
                            TextBlock.fontSize 48.0
                            TextBlock.foreground "Gray"
                            TextBlock.horizontalAlignment HorizontalAlignment.Center
                            TextBlock.text (fNum state.Current |> string)
                        ]
                    ]
                ]
        )

    [<LivePreview>]
    let preview () =
        view "preview1" [
            Component.background Brushes.DarkRed
        ] id Store.num

    [<LivePreview>]
    let preview2 () =
        Store.num
        |> view "preview2" [
            Component.background Brushes.DarkGreen
        ] id

module Memo =

    [<LivePreview>]
    let preview () =
        printfn "called Memo.preview"

        Store.num
        |> Counter.view "preview3" [
            Component.background Brushes.DarkBlue
        ] (fun i -> i + 1 )