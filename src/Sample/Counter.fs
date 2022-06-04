namespace Sample



module Sample =
    open Avalonia.FuncUI
    open Avalonia.FuncUI.DSL
    open Avalonia.Controls
    open Avalonia.Media
    open Avalonia.Layout
    open Avalonia.FuncUI.LiveView.Core.Types

    let view numState =
        Component(fun ctx ->
                let num = ctx.usePassed numState

                DockPanel.create [
                    DockPanel.verticalAlignment VerticalAlignment.Center
                    DockPanel.horizontalAlignment HorizontalAlignment.Center
                    DockPanel.children [
                        Button.create [
                            Button.width 64

                            Button.horizontalAlignment HorizontalAlignment.Center
                            Button.horizontalContentAlignment HorizontalAlignment.Center
                            Button.content "Reset"
                            Button.onClick (fun _ -> num.Set 0)
                            Button.dock Dock.Bottom
                        ]
                        Button.create [
                            Button.width 64
                            Button.horizontalAlignment HorizontalAlignment.Center
                            Button.horizontalContentAlignment HorizontalAlignment.Center
                            Button.content "-"
                            Button.onClick (fun _ -> num.Current - 1 |> num.Set)
                            Button.dock Dock.Bottom
                        ]
                        Button.create [
                            Button.width 64
                            Button.horizontalAlignment HorizontalAlignment.Center
                            Button.horizontalContentAlignment HorizontalAlignment.Center
                            Button.content "+"
                            Button.onClick (fun _ -> num.Current + 1 |> num.Set)
                            Button.dock Dock.Bottom
                        ]
                        TextBlock.create [
                            TextBlock.dock Dock.Top
                            TextBlock.fontSize 48.0
                            TextBlock.foreground Brushes.White
                            TextBlock.horizontalAlignment HorizontalAlignment.Center
                            TextBlock.text (string num.Current)
                        ]
                    ]
                ]
        )
    
    [<LivePreview>]
    let preview =
        view Store.num
