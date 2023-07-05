module Sample.ComponentSample

open Avalonia
open Avalonia.FuncUI
open Avalonia.FuncUI.DSL
open Avalonia.Controls
open Avalonia.Controls.Shapes
open Avalonia.Media
open Avalonia.Media.Imaging
open Avalonia.Layout
open Avalonia.FuncUI.LiveView
open Avalonia.Controls.Primitives
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Markup.Xaml.Converters
open Avalonia.Media.Immutable
open Avalonia.FuncUI.Builder
open System

type ColorPicker with

    static member create attrs = ViewBuilder.Create<ColorPicker> attrs


    static member color<'t when 't :> ColorPicker> color =
        AttrBuilder<'t>.CreateProperty(ColorPicker.ColorProperty, color, ValueNone)

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
    open Avalonia.Collections
    open System.Collections.Generic

    let colorSetting id color enableBackGround attrs =
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
                let enableBackGround = ctx.usePassedRead enableBackGround

                ctx.attrs [ yield! attrs ]

                ComboBox.create [
                    ComboBox.isEnabled enableBackGround.Current
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

    let gridLineBrush gridStep =
        DrawingBrush(
            GeometryDrawing(
                Pen = Pen(brush = Brushes.Gray, thickness = 0.5),
                Geometry =
                    GeometryGroup(
                        Children = GeometryCollection(seq { RectangleGeometry(Rect(0, 0, gridStep, gridStep)) }),
                        FillRule = FillRule.EvenOdd

                    )
            ),
            TileMode = TileMode.Tile,
            SourceRect = RelativeRect(0, 0, gridStep, gridStep, RelativeUnit.Absolute),
            DestinationRect = RelativeRect(0, 0, gridStep, gridStep, RelativeUnit.Absolute)
        )

    let inline initList<'list, 'x when 'list: (new: unit -> 'list) and 'list: (member AddRange: IEnumerable<'x> -> unit)>
        ls
        =
        let collection = new 'list ()
        collection.AddRange ls
        collection

    module DrawingBrush =
        let gridLine (stepInfo: list<float * Pen>) =
            let lcmOfList lst =
                let lcm a b =
                    let rec gcd a b = if b = 0. then a else gcd b (a % b)
                    abs (a * b) / gcd a b

                List.fold lcm 1. lst


            let getPtMap offsets =
                let offsets = offsets |> List.sortDescending |> List.distinct
                let lcm = lcmOfList offsets

                let pts =
                    [
                        for offset in offsets do
                            for i = 0. to (lcm / offset) do
                                offset, i * offset
                    ]
                    |> List.distinctBy snd
                    |> List.sortBy snd

                Map [
                    for offset in offsets do
                        offset, pts |> List.filter (fun (key, _) -> key = offset) |> List.map snd
                ]

            let hFigure length y =
                PathFigure(
                    IsClosed = false,
                    StartPoint = Point(0, y),
                    Segments = initList [ LineSegment(Point = Point(length, y)) ]
                )

            let vFigure length x =
                PathFigure(
                    IsClosed = false,
                    StartPoint = Point(x, 0),
                    Segments = initList [ LineSegment(Point = Point(x, length)) ]
                )

            let vhFigures length offsets =
                initList [
                    for offset in offsets do
                        hFigure length offset
                        vFigure length offset
                ]

            let vhLineDrawing length offsets pen =
                GeometryDrawing(Pen = pen, Geometry = PathGeometry(Figures = vhFigures length offsets))

            let lcm, ptMap =
                let pts = List.map fst stepInfo
                lcmOfList pts, getPtMap pts

            DrawingBrush(
                DrawingGroup(
                    Children =
                        initList [
                            for offset, pen in stepInfo do
                                vhLineDrawing lcm ptMap[offset] pen
                        ]
                ),
                TileMode = TileMode.Tile,
                SourceRect = RelativeRect(0, 0, lcm, lcm, RelativeUnit.Absolute),
                DestinationRect = RelativeRect(0, 0, lcm, lcm, RelativeUnit.Absolute),
                Transform = TranslateTransform(80, 10)
            )

        let brush =
            gridLine [
                10, Pen(Brush.Parse "#838383ff", thickness = 0.5)
                50, Pen(Brush.Parse "#8d8d8d", thickness = 0.8, dashStyle = DashStyle.Dash)
                100, Pen(Brush.Parse "#7e7e7e", thickness = 0.8)
            ]

    let grid gridStep =
        let lcmOfList lst =
            let lcm a b =
                let rec gcd a b = if b = 0 then a else gcd b (a % b)
                abs (a * b) / gcd a b

            List.fold lcm 1 lst


        let getPtMap offsets =
            let offsets = offsets |> List.sortDescending |> List.distinct
            let lcm = lcmOfList offsets

            let pts =
                [
                    for offset in offsets do
                        for i = 0 to (lcm / offset) do
                            offset, i * offset
                ]
                |> List.distinctBy snd
                |> List.sortBy snd

            Map [
                for offset in offsets do
                    offset, pts |> List.filter (fun (key, _) -> key = offset) |> List.map snd
            ]

        let hFigure length y =
            PathFigure(
                IsClosed = false,
                StartPoint = Point(0, y),
                Segments = initList [ LineSegment(Point = Point(length, y)) ]
            )

        let vFigure length x =
            PathFigure(
                IsClosed = false,
                StartPoint = Point(x, 0),
                Segments = initList [ LineSegment(Point = Point(x, length)) ]
            )

        let vhFigures length offsets =
            initList [
                for offset in offsets do
                    hFigure length offset
                    vFigure length offset
            ]

        let vhLineDrawing length offsets pen =
            GeometryDrawing(Pen = pen, Geometry = PathGeometry(Figures = vhFigures length offsets))

        let lcm = lcmOfList [ 4; 5; 10 ] // 20

        // pts
        // |> List.map(fun pt -> List.init (lcm / pt) (fun i ->pt, (i + 1) * pt) )

        DrawingBrush(
            DrawingGroup(
                Children =
                    initList [
                        Pen(Brush.Parse "#ffffff", thickness = 0.8) |> vhLineDrawing lcm [ 0; 20 ]
                        Pen(Brush.Parse "#f38b8b", thickness = 0.5) |> vhLineDrawing lcm [ 10 ]
                        Pen(Brush.Parse "#2e2e2e", thickness = 1, dashStyle = DashStyle([ 6; 1 ], 0))
                        |> vhLineDrawing lcm [ 5; 15 ]
                        Pen(Brush.Parse "#0a4864", thickness = 0.5, dashStyle = DashStyle.Dot)
                        |> vhLineDrawing lcm [ 4; 8; 12; 16 ]
                    ]
            ),
            TileMode = TileMode.Tile,
            SourceRect = RelativeRect(0, 0, lcm, lcm, RelativeUnit.Absolute),
            DestinationRect = RelativeRect(0, 0, lcm, lcm, RelativeUnit.Absolute)

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

                let enableBackGround = ctx.useState false


                ctx.attrs [
                    Component.horizontalAlignment HorizontalAlignment.Stretch
                    Component.verticalAlignment VerticalAlignment.Stretch
                    // Component.background (gridLineBrush 20)
                    // Component.background (grid 20)
                    Component.background (DrawingBrush.brush)
                ]

                Grid.create [
                    Grid.margin 8
                    Grid.rowDefinitions "Auto,Auto"
                    Grid.columnDefinitions "Auto,Auto,Auto"
                    Grid.horizontalAlignment HorizontalAlignment.Center
                    Grid.verticalAlignment VerticalAlignment.Center
                    Grid.children [
                        StackPanel.create [
                            StackPanel.row 1
                            StackPanel.columnSpan 3
                            StackPanel.margin 8
                            StackPanel.spacing 8
                            StackPanel.orientation Orientation.Horizontal
                            StackPanel.children [
                                CheckBox.create [
                                    CheckBox.row 1
                                    CheckBox.content $"backgroundColor"
                                    CheckBox.isChecked enableBackGround.Current
                                    CheckBox.onChecked (fun e -> enableBackGround.Set true)
                                    CheckBox.onUnchecked (fun e -> enableBackGround.Set false)
                                ]

                                colorSetting viewId backgroundColor enableBackGround [ Border.row 1; Border.column 2 ]
                                Button.create [
                                    Button.flyout (
                                        Flyout.create [
                                            Flyout.placement PlacementMode.RightEdgeAlignedBottom
                                            Flyout.showMode FlyoutShowMode.Standard
                                            Flyout.content (
                                                ColorPicker.create [ ColorPicker.color backgroundColor.Current ]
                                            )
                                        ]
                                    )
                                    Button.content (TextBlock.create [ TextBlock.text $"{backgroundColor.Current}" ])
                                ]
                                ColorPicker.create [ ColorPicker.color backgroundColor.Current ]
                            ]
                        ]
                        Border.create [
                            Border.margin 8
                            Border.columnSpan 3
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
let draft () =
    Component.create (
        "draft",
        fun ctx ->
            Menu.create [
                Menu.viewItems [
                    MenuItem.create [
                        MenuItem.header "File"
                        MenuItem.viewItems [
                            MenuItem.create [ MenuItem.header "Open Project" ]
                            MenuItem.create [ MenuItem.header "Close Project." ]
                        ]
                    ]
                ]
            ]
    )



[<LivePreview>]
let draft2 () =
    Component.create (
        "draft2",
        fun ctx ->
            let num = ctx.usePassed Store.num

            // draft Path...
            let p = PathGeometry.Parse "M0,10 h100 m0,10 h-100 m0,10 h100 m0,10 h-100"
            // List.ofSeq p.Figures |> List.iter (printfn "%A")

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
                        Canvas.create [
                            Path.height 100
                            Path.width 100
                            Canvas.background Brushes.Gray
                            Canvas.children [
                                Path.create [
                                    Path.stroke Brushes.White
                                    Path.strokeThickness 0.5
                                    Path.opacity 0.7
                                    Path.data "M0,10 h100 m0,10 h-100 m0,10 h100 m0,10 h-100"
                                ]
                                Path.create [
                                    Path.stroke Brushes.Black
                                    Path.strokeThickness 8
                                    Path.strokeDashArray [ 1.; 4. ]
                                    Path.opacity 0.7
                                    Path.data "M0,0 h100 m0,50 h-100"
                                ]
                            ]
                        ]
                    ]
                ]
                |> Border.child
            ]
    )

type IComponentContext with

    member inline this.useEffectdeDounceTime(handler, triggers: list<EffectTrigger>, dueTime) =
        let createTimer () = new Threading.PeriodicTimer(dueTime)
        let timer = this.useState (createTimer (), false)

        this.useEffect (
            (fun () ->
                timer.Current.Dispose()
                let newTimer = createTimer ()

                task {
                    match! newTimer.WaitForNextTickAsync() with
                    | true -> handler ()
                    | false -> ()
                }
                |> ignore

                timer.Set newTimer),
            triggers
        )

[<LivePreview>]
let draft3 () =
    Component.create (
        "draft-3",
        fun ctx ->
            let num = ctx.usePassed Store.num
            let debounceNum = ctx.useState num.Current

            ctx.useEffectdeDounceTime (
                (fun () ->
                        debounceNum.Set(num.Current * 2)),
                [ EffectTrigger.AfterInit; EffectTrigger.AfterChange num ],
                TimeSpan.FromSeconds 5
            )

            StackPanel.create [
                StackPanel.children [
                    counter num []
                    TextBlock.create [ TextBlock.text $"debounce: {debounceNum.Current}" ]
                ]
            ]
    )

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