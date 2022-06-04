//#funcuianalyzer

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

module Counter =
    let f name (content:Types.IView) =
        Grid.create [
            Grid.dock Dock.Top
            Grid.rowDefinitions "Auto,*"
            Grid.children [
                TextBlock.create [
                    TextBlock.row 0
                    TextBlock.text name
                ]
                Border.create [
                    Border.row 1
                    Border.child content
                ]
            ]

        ]
    [<LivePreview>]
    let view numState =
        Component.create (
            "Counter",
            fun ctx ->
                let state = ctx.usePassed numState
                
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
                            TextBlock.fontSize 48.0
                            TextBlock.foreground Brushes.White
                            TextBlock.horizontalAlignment HorizontalAlignment.Center
                            TextBlock.text (string state.Current)
                        ]
                    ]
                ]
        )