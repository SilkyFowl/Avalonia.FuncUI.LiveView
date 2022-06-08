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
                let num =
                    Store.num
                    |> State.readMap (fun i -> 4.0 ** i)
                    |> ctx.usePassedRead

                TextBlock.create [
                    TextBlock.foreground Brushes.LightSlateGray
                    TextBlock.fontSize 20
                    TextBlock.horizontalAlignment HorizontalAlignment.Center
                    TextBlock.verticalAlignment VerticalAlignment.Center
                    TextBlock.text $"Foo: {num.Current}"
                ]
        )

    [<LivePreview>]
    let draft2 () =
        Component.create (
            "draft2",
            fun ctx ->
                let num = ctx.usePassed Store.num

                Border.create [
                    Border.borderThickness 0.5
                    Border.borderBrush Brushes.White

                    TextBlock.create [
                        TextBlock.foreground "White"
                        TextBlock.horizontalAlignment HorizontalAlignment.Center
                        TextBlock.verticalAlignment VerticalAlignment.Center
                        TextBlock.fontSize 30
                        TextBlock.text $"Bar:{num.Current * 3}"
                    ]
                    |> Border.child
                ]
        )

    [<LivePreview>]
    let draft3 () = counter Store.num

    [<LivePreview>]
    let dtaft4 () =
        Grid.create [
            Grid.rowDefinitions "Auto,Auto,Auto"
            Grid.columnDefinitions "Auto,*"
            Grid.margin 8
            Grid.children [
                TextBlock.create [
                    TextBlock.row 0
                    TextBlock.columnSpan 2
                    TextBlock.fontSize 20 
                    TextBlock.fontWeight FontWeight.SemiBold
                    TextBlock.verticalAlignment VerticalAlignment.Center
                    TextBlock.margin (0, 0, 0, 4)
                    TextBlock.text "Hoge"
                ]

                TextBlock.create [
                    TextBlock.row 2
                    TextBlock.column 0
                    TextBlock.verticalAlignment VerticalAlignment.Center
                    TextBlock.margin (4, 0)
                    TextBlock.text "Fuga"
                ]
                TextBox.create [
                    TextBlock.row 2
                    TextBox.column 1
                    TextBox.margin (4, 0)
                ]
            ]
        ]

    [<LivePreview>]
    let preview4 () =
        Expander.create [
            Expander.header "test"
            Expander.isExpanded true
            Expander.content (counter Store.num)
        ]

    let cmp =
        Component (fun ctx ->
            let num = ctx.usePassed Store.num
            counter num)