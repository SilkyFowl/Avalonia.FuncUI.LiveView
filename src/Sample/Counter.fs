namespace Sample

module Sample =
    open Avalonia.FuncUI
    open Avalonia.FuncUI.DSL
    open Avalonia.Controls
    open Avalonia.Media
    open Avalonia.Layout
    open Avalonia.FuncUI.LiveView.Core.Types

    let counter numState =
        Component.create (
            "counter",
            fun ctx ->
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
                            Button.onClick (fun _ -> num.Current - 10 |> num.Set)
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
    let draft () =
        Component.create (
            "draft",
            fun ctx ->
                let num = ctx.usePassed Store.num

                TextBlock.create [
                    TextBlock.foreground Brushes.LightBlue
                    TextBlock.horizontalAlignment HorizontalAlignment.Center
                    TextBlock.verticalAlignment VerticalAlignment.Center
                    TextBlock.text $"Foo{num.Current}"
                ]
        )


    [<LivePreview>]
    let draft2 () =
        Component.create (
            "draft2",
            fun ctx ->
                let num = ctx.usePassed Store.num

                TextBlock.create [
                    TextBlock.foreground "White"
                    TextBlock.horizontalAlignment HorizontalAlignment.Center
                    TextBlock.verticalAlignment VerticalAlignment.Center
                    TextBlock.fontSize 30
                    TextBlock.text $"Bar: {num.Current * 2}"
                ]
        )

    [<LivePreview>]
    let draft3 () =
        counter Store.num

    [<LivePreview>]
    let dtaft4() =
        Grid.create [
            Grid.columnDefinitions "Auto,*"
            Grid.children [
                TextBlock.create [
                    TextBlock.column 0
                    TextBlock.verticalAlignment VerticalAlignment.Center
                    TextBlock.margin (4,0)
                    TextBlock.text "Hoge"
                ]
                TextBox.create [
                    TextBox.column 1
                    TextBox.margin (4,0)
                ]
            ]
        ]

    let cmp =
        Component (fun ctx ->
            let num = ctx.usePassed Store.num
            counter num)