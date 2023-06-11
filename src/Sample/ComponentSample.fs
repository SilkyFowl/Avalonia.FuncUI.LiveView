module Sample.ComponentSample

open Avalonia
open Avalonia.FuncUI
open Avalonia.FuncUI.DSL
open Avalonia.Controls
open Avalonia.Controls.Shapes
open Avalonia.Media
open Avalonia.Media.Imaging
open Avalonia.Layout
open Avalonia.FuncUI.LiveView.Core.Types
open Avalonia.Controls.Primitives
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Markup.Xaml.Converters
open Avalonia.Media.Immutable
open Avalonia.FuncUI.Builder

let counter numState attrs =

    Component.create (
        "counter",
        fun ctx ->
            let num = ctx.usePassed numState

            ctx.attrs [ yield! attrs ]

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
                        TextBlock.horizontalAlignment HorizontalAlignment.Center
                        TextBlock.text (string num.Current)
                    ]
                ]
            ]
    )


module Previewer =
    open Avalonia.FuncUI.Types

    let colorSetting id color attrs =
        Component.create (
            $"colorSetting-{id}",
            fun ctx ->
                let colors =
                    (typeof<Colors>).GetProperties()
                    |> Array.choose (fun p ->
                        let gm = p.GetGetMethod()

                        if gm.IsStatic then
                            match gm.Invoke(null, null) with
                            | :? Color as color -> Some color
                            | _ -> None
                        else
                            None)
                    |> ctx.useState

                let color = ctx.usePassed color

                ctx.attrs [ yield! attrs ]

                ComboBox.create [
                    ComboBox.dataItems colors.Current
                    ComboBox.selectedItem color.Current
                    ComboBox.onSelectedItemChanged (function
                        | :? Color as c -> color.Set c
                        | _ -> ())
                    ComboBox.itemTemplate (
                        DataTemplateView<Color>.create (fun c ->
                            StackPanel.create [
                                StackPanel.orientation Orientation.Horizontal
                                StackPanel.spacing 2
                                StackPanel.background Brushes.Transparent
                                StackPanel.cursor (new Cursor(StandardCursorType.Hand))
                                StackPanel.children [
                                    Ellipse.create [
                                        Ellipse.width 12
                                        Ellipse.height 12
                                        Ellipse.verticalAlignment VerticalAlignment.Center
                                        Ellipse.fill (ImmutableSolidColorBrush c)
                                    ]
                                    TextBlock.create [
                                        TextBlock.verticalAlignment VerticalAlignment.Center
                                        TextBlock.text $"%O{c}"
                                    ]
                                ]
                            ])
                    )
                ]
        )

    let gridLineBrush =
        DrawingBrush(
            GeometryDrawing(
                Pen = Pen(brush = Brushes.Gray, thickness = 0.5),
                Geometry =
                    GeometryGroup(
                        Children = GeometryCollection(seq { RectangleGeometry(Rect(0, 0, 10, 10)) }),
                        FillRule = FillRule.EvenOdd

                    )
            ),
            TileMode = TileMode.Tile,
            SourceRect = RelativeRect(0, 0, 10, 10, RelativeUnit.Absolute),
            DestinationRect = RelativeRect(0, 0, 10, 10, RelativeUnit.Absolute)
        )

    let create id (view: IView) =
        let viewId = $"previewer-{id}"
 
        Component.create (
            viewId,
            fun ctx -> 
                let backgroundColor =
                    TopLevel.GetTopLevel ctx.control
                    |> Option.ofObj
                    |> Option.bind (fun tl ->
                        match Option.ofObj tl.Background with
                        | Some(:? SolidColorBrush as cb) -> Some cb.Color
                        | _ -> None)
                    |> Option.defaultValue Colors.Transparent
                    |> ctx.useState
                
                let enableBackGround = ctx.useState true


                ctx.attrs [
                    Component.horizontalAlignment HorizontalAlignment.Stretch
                    Component.verticalAlignment VerticalAlignment.Stretch
                    Component.background gridLineBrush
                ]

                Grid.create [
                    Grid.margin 8
                    Grid.rowDefinitions "Auto,Auto"
                    Grid.columnDefinitions "Auto,Auto,Auto"
                    Grid.horizontalAlignment HorizontalAlignment.Center
                    Grid.verticalAlignment VerticalAlignment.Center
                    Grid.children [
                        CheckBox.create [
                            CheckBox.isChecked enableBackGround.Current
                            CheckBox.onChecked(fun e -> enableBackGround.Set true)
                            CheckBox.onUnchecked(fun e -> enableBackGround.Set false)
                        ]
                        TextBlock.create [
                            TextBlock.row 1
                            TextBlock.column 1
                            TextBlock.margin 8
                            TextBlock.horizontalAlignment HorizontalAlignment.Center
                            TextBlock.verticalAlignment VerticalAlignment.Center
                            TextBlock.text $"backgroundColor"
                        ]
                        colorSetting viewId backgroundColor [ Border.row 1; Border.column 2 ]
                        Border.create [
                            Border.margin 8
                            Border.columnSpan 2
                            Border.child view
                            if enableBackGround.Current then
                                Border.background backgroundColor.Current
                        ]
                    ]

                ]

        )


    [<LivePreview>]
    let preview () =
        let view =
            DockPanel.create [
                DockPanel.children [
                    TextBox.create [ TextBox.margin 8; TextBox.dock Dock.Bottom; TextBox.text "DTAFT" ]
                    counter Store.num [ Border.margin 8 ]
                ]
            ]



        Border.create [ Border.child (create "preview" view) ]





[<LivePreview>]
let draft2 () =
    Component.create (
        "draft2",
        fun ctx ->
            let num = ctx.usePassed Store.num




            Border.create [
                Border.borderThickness 0.5
                Border.borderBrush Brushes.White
                Border.background "Azure"
                StackPanel.create [
                    StackPanel.children [
                        Canvas.create [
                            Canvas.children [
                                Path.create [
                                    Path.fill Brushes.DarkCyan
                                    Path.opacity 0.7
                                    Path.data "M 0,0 c 0,0 50,0 50,-50 c 0,0 50,0 50,50 h -50 v 50 l -50,-50 Z"
                                ]
                            ]
                        ]
                        TextBlock.create [
                            TextBlock.foreground "Blue"

                            TextBlock.horizontalAlignment HorizontalAlignment.Left
                            TextBlock.verticalAlignment VerticalAlignment.Center
                            TextBlock.fontSize 22
                            TextBlock.text $"Bar:{num.Current * 3}"
                        ]
                    ]
                ]
                |> Border.child
            ]
    )

[<LivePreview>]
let draft3 () = counter Store.num []

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
                TextBlock.foreground Brushes.Red
                TextBlock.fontWeight FontWeight.SemiBold
                TextBlock.verticalAlignment VerticalAlignment.Center
                TextBlock.margin (0, 0, 0, 4)
                TextBlock.text "Hoge"
            ]

            TextBlock.create [
                TextBlock.row 2
                TextBlock.column 0
                TextBlock.background "DarkBlue"
                TextBlock.verticalAlignment VerticalAlignment.Center
                TextBlock.margin (4, 0)
                TextBlock.text "Fuga...."
            ]
            TextBox.create [ TextBlock.row 2; TextBox.column 1; TextBox.margin (4, 0) ]
        ]
    ]

[<LivePreview>]
let preview4 () =
    Expander.create [
        Expander.header "test..."
        Expander.isExpanded true
        Expander.content (counter Store.num [])
    ]

[<LivePreview>]
let previewObj () =
    [ 1..10 ]
    |> List.map (fun i ->
        match Store.num.CurrentAny with
        | :? int as num -> i * num
        | _ -> i * 3)

let cmp =
    Component(fun ctx ->
        let num = ctx.usePassed Store.num
        counter num [])