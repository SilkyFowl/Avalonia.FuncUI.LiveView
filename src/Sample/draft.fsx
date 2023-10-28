module Draft
#if !LIVEPREVIEW
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
#r "Avalonia.FuncUI.LiveView.Attribute"
#endif

open Avalonia.Controls
open Avalonia.Media
open Avalonia.Layout
open Avalonia.FuncUI
open Avalonia.FuncUI.DSL
open Avalonia.FuncUI.LiveView
open Sample

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
                            Button.onClick (fun _ -> state.Current - rnd.Next(1, 10) |> state.Set)
                            Button.dock Dock.Bottom
                        ]
                        Button.create [
                            Button.width 64
                            Button.horizontalAlignment HorizontalAlignment.Center
                            Button.horizontalContentAlignment HorizontalAlignment.Center
                            Button.content "+"
                            Button.onClick (fun _ -> state.Current + 10 |> state.Set)
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
    let redCounter () =
        view "preview1" [ Component.background Brushes.DarkRed ] id Store.num

    [<LivePreview>]
    let greenCounter () =
        Store.num |> view "preview2" [ Component.background Brushes.DarkGreen ] id

module Memo =

    [<LivePreview>]
    let blueCounter () =
        printfn "called Memo.preview."

        Store.num
        |> Counter.view "preview3" [ Component.background Brushes.DarkBlue ] (fun i -> i + 3)