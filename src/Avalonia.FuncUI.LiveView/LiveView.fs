namespace Avalonia.FuncUI.LiveView

open System
open System.IO
open Avalonia
open Avalonia.Controls
open Avalonia.Media
open Avalonia.FuncUI
open Avalonia.FuncUI.Types
open Avalonia.FuncUI.DSL
open Avalonia.FuncUI.Hosts

open Avalonia.FuncUI.LiveView.MessagePack

[<AutoOpen>]
module private ViewHelper =
    open System.Collections.Generic

    let inline initList<'list, 'x when 'list: (new: unit -> 'list) and 'list: (member AddRange: IEnumerable<'x> -> unit)>
        ls
        =
        let collection = new 'list ()
        collection.AddRange ls
        collection

    module GridLineBrush =
        let create (stepInfo: list<float * Pen>) =
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
                Transform = TranslateTransform(0, 0)
            )

    type IComponentContext with

        member this.useAsync<'signal>(init: Async<'signal>) : IWritable<Deferred<_, _>> =
            let state = this.useState (Deferred.Waiting, true)

            this.useEffect (
                handler =
                    (fun _ ->
                        match state.Current with
                        | Deferred.Waiting ->
                            state.Set Deferred.InProgress

                            Async.StartImmediate(
                                async {
                                    let! result = Async.Catch init

                                    match result with
                                    | Choice1Of2 value -> state.Set(Deferred.Resolved(Ok value))
                                    | Choice2Of2 exn -> state.Set(Deferred.Resolved(Error exn))
                                }
                            )

                        | _ -> ()),
                triggers = [ EffectTrigger.AfterInit ]
            )

            state

    type PreviewConterntBorder() =
        inherit Border()

        let getLayoutZoneHeitht (border: Border) =
            let margin = border.Margin
            let borderThickness = border.BorderThickness
            let padding = border.Padding

            margin.Top
            + margin.Bottom
            + borderThickness.Top
            + borderThickness.Bottom
            + padding.Top
            + padding.Bottom

        let mutable backupDesiredSize = ValueNone

        /// When the child DesiredSize is 0, return the backup DesiredSize.
        /// To avoid the behavior that DesiredSize becomes 0 for a moment when replacing the Preview result.
        override this.MeasureOverride availableSize =
            match this.Child, backupDesiredSize with
            | null, ValueNone -> base.MeasureOverride availableSize
            | null, ValueSome oldAvailableSize -> oldAvailableSize
            | :? Component as child, ValueNone ->
                child.Measure availableSize

                if child.DesiredSize.Height <= getLayoutZoneHeitht child then
                    base.MeasureOverride availableSize

                else
                    backupDesiredSize <- ValueSome child.DesiredSize
                    child.DesiredSize
            | :? Component as child, ValueSome desiredSize ->
                child.Measure availableSize

                if child.DesiredSize.Height <= getLayoutZoneHeitht child then
                    desiredSize
                else
                    backupDesiredSize <- ValueSome child.DesiredSize
                    child.DesiredSize
            | _ -> base.MeasureOverride availableSize

module LiveView =
    open PreviewService
    open Avalonia.FuncUI.Elmish.ElmishHook
    open Avalonia.Layout
    open Avalonia.Media.Immutable

    module private Store =
        module PreviewConterntBorder =

            let background = new State<IBrush>(Brushes.Transparent)

        module PreviewItemView =
            let background = new State<IBrush>(Brushes.Transparent)
            let enableBackground = new State<bool> true

    let inline private extractObj (o: obj) : IView =
        match o with
        | :? IView as view -> view
        | :? Control as view -> ContentControl.create [ ContentControl.content view ]
        | other -> TextBlock.create [ TextBlock.text $"%A{other}" ]

    let inline private extractExn (ex: exn) : IView =
        let ex =
            match ex with
            | :? Reflection.TargetInvocationException as ex -> ex.InnerException
            | _ -> ex

        StackPanel.create [
            StackPanel.children [
                TextBox.create [ TextBox.foreground Brushes.Red; TextBox.text $"%A{ex.GetType()}" ]
                TextBox.create [ TextBox.foreground Brushes.Red; TextBox.text $"%s{ex.Message}" ]
                TextBox.create [
                    TextBox.text $"%s{ex.StackTrace}"
                    TextBox.textWrapping TextWrapping.WrapWithOverflow
                ]
            ]
        ]

    let private previewItemView viewId (asyncView: Async<IView>) =
        Component.create (
            viewId,
            fun ctx ->
                let asyncView = ctx.useAsync asyncView
                let background = ctx.usePassedRead Store.PreviewItemView.background
                let enableBackground = ctx.usePassedRead Store.PreviewItemView.enableBackground


                ctx.attrs [
                    Component.margin 8
                    Component.horizontalAlignment HorizontalAlignment.Center
                    Component.verticalAlignment VerticalAlignment.Center
                    if enableBackground.Current then
                        Component.background background.Current
                ]

                match asyncView.Current with
                | Deferred.Waiting
                | Deferred.HasNotStartedYet(_)
                | Deferred.InProgress ->
                    // Dummy View
                    Border.create [ Border.isVisible false ]
                | Deferred.Resolved(Ok view) -> view
                | Deferred.Resolved(Error ex) -> extractExn ex

        )

    let private tabItemContentView (path: string) (evalState) (previewConterntBorderBackground: IBrush) =
        // TODO: code view
        let defaultContent text =
            async { return TextBlock.create [ TextBlock.text text ] :> IView }

        let time, contents =
            match evalState with
            | InProgress(ValueSome { views = views; timestamp = timestamp })
            | Success({ views = views; timestamp = timestamp })
            | Failed(_, ValueSome { views = views; timestamp = timestamp }) ->
                timestamp,
                Map.toList views
                |> List.map ((fun (key, viewFn) -> key, viewFn extractObj extractExn))
            | InProgress ValueNone -> DateTime.Now, [ "InProgress", defaultContent "InProgress" ]
            | Failed(_, ValueNone) -> DateTime.Now, [ "Failed", defaultContent "Failed" ]

        ScrollViewer.create [
            ScrollViewer.content (
                ItemsControl.create [
                    ItemsControl.viewItems [
                        for name, content in contents do
                            Grid.create [
                                Grid.rowDefinitions "Auto,Auto"
                                Grid.children [
                                    TextBlock.create [
                                        TextBlock.row 0
                                        TextBlock.fontSize 20
                                        TextBlock.margin 8
                                        TextBlock.fontWeight FontWeight.SemiBold
                                        TextBlock.text name
                                    ]
                                    let viewKey =
                                        let timeStr = time.ToString "MM-dd-yy-H:mm:ss.fff"
                                        $"tabItemContentView-{path}-{name}-{timeStr}"

                                    View.createGeneric<PreviewConterntBorder> [
                                        Border.row 1
                                        Border.background previewConterntBorderBackground
                                        previewItemView viewKey content |> Border.child
                                    ]
                                ]
                            ]
                    ]
                ]
            )
        ]


    let private tabItemHeaderView (path: string) (evalState) =
        let fileName = Path.GetFileName path

        match evalState with
        | InProgress _ -> TextBlock.create [ TextBlock.text $"[Eval...]{fileName}" ]
        | Success _ -> TextBlock.create [ TextBlock.text $"{fileName}" ]
        | Failed _ -> TextBlock.create [ TextBlock.text $"[!!]{fileName}"; TextBlock.hasErrors true ]

    let create id attrs =
        let getTopLevelBackgroundObservable control =
            let topLevel = TopLevel.GetTopLevel control

            topLevel.GetObservable TopLevel.BackgroundProperty
            |> Observable.map (function
                | null -> Brushes.Transparent :> IBrush
                | bg -> bg)

        let fsiSession = new FsiSession.FsSession()
        let syncContext = Threading.AvaloniaSynchronizationContext()

        let mapProgram =
            Elmish.Program.withSubscription (subscription fsiSession syncContext)

        Component.create (
            id,
            fun ctx ->
                let model, dispatch = ctx.useElmish (init, update, mapProgram)
                let evalStateMap = model.evalStateMap
                let selectedEvalStateKey = ctx.useState<string option> (None, false)

                let previewConterntBorderBackground =
                    ctx.usePassed Store.PreviewConterntBorder.background

                let gridLineStepInfo =
                    ctx.useState (
                        [
                            10., Pen(Brush.Parse "#838383ff", thickness = 0.5)
                            50., Pen(Brush.Parse "#8d8d8d", thickness = 0.8, dashStyle = DashStyle.Dash)
                            100., Pen(Brush.Parse "#7e7e7e", thickness = 0.8)
                        ],
                        false
                    )

                let previewItemViewBackground = ctx.usePassed Store.PreviewItemView.background

                let enablePreviewItemViewBackground =
                    ctx.usePassed Store.PreviewItemView.enableBackground

                ctx.useEffect (
                    (fun () ->
                        GridLineBrush.create gridLineStepInfo.Current
                        |> previewConterntBorderBackground.Set),
                    [ EffectTrigger.AfterInit; EffectTrigger.AfterChange gridLineStepInfo ]
                )



                ctx.useEffect (
                    (fun () ->
                        GridLineBrush.create gridLineStepInfo.Current
                        |> previewConterntBorderBackground.Set

                        getTopLevelBackgroundObservable ctx.control
                        |> Observable.subscribe previewItemViewBackground.Set
                        |> ctx.trackDisposable

                        ctx.trackDisposable fsiSession

                        Client.init
                            (SetLogMessage >> dispatch)
                            Settings.iPAddress
                            Settings.port
                            (LiveViewAnalyzerMsg >> dispatch)),
                    [ EffectTrigger.AfterInit ]
                )

                ctx.attrs [ yield! attrs ]

                Grid.create [
                    Grid.rowDefinitions "*,Auto,Auto"
                    Grid.columnDefinitions "Auto,*,Auto"
                    Grid.children [
                        TabControl.create [
                            TabControl.row 0
                            TabControl.columnSpan 3
                            TabControl.onSelectedIndexChanged (
                                (fun idx ->
                                    Map.toList evalStateMap
                                    |> List.tryItem idx
                                    |> Option.map fst
                                    |> selectedEvalStateKey.Set),
                                OnChangeOf evalStateMap
                            )
                            TabControl.viewItems [
                                for path, evalState in Map.toList evalStateMap do
                                    TabItem.create [
                                        TabItem.header (tabItemHeaderView path evalState)
                                        TabItem.content (
                                            tabItemContentView path evalState previewConterntBorderBackground.Current
                                        )
                                    ]
                            ]
                        ]
                        StackPanel.create [
                            StackPanel.row 1
                            StackPanel.column 0
                            StackPanel.margin 8
                            StackPanel.spacing 8
                            StackPanel.orientation Orientation.Horizontal
                            StackPanel.children [
                                CheckBox.create [
                                    CheckBox.content "auto update"
                                    CheckBox.isChecked model.autoUpdate
                                    CheckBox.onChecked (fun _ -> SetAutoUpdate true |> dispatch)
                                    CheckBox.onUnchecked (fun _ -> SetAutoUpdate false |> dispatch)
                                ]
                                CheckBox.create [
                                    CheckBox.content "fill background"
                                    CheckBox.isChecked enablePreviewItemViewBackground.Current
                                    CheckBox.onChecked (fun _ -> enablePreviewItemViewBackground.Set true)
                                    CheckBox.onUnchecked (fun _ -> enablePreviewItemViewBackground.Set false)
                                ]
                            ]
                        ]
                        Button.create [
                            Button.row 1
                            Button.column 2
                            Button.margin 8
                            Button.horizontalAlignment HorizontalAlignment.Left
                            Button.content "eval manualy"
                            Button.isEnabled (
                                match model.evalInteractionDeferred, selectedEvalStateKey.Current with
                                | Waiting, Some _ -> true
                                | _ -> false
                            )
                            Button.onClick (fun _ ->
                                selectedEvalStateKey.Current
                                |> Option.iter (fun key -> EvalInteraction(key, StartRepuested) |> dispatch))
                        ]
                        TextBlock.create [
                            TextBlock.row 2
                            TextBlock.column 0
                            TextBlock.columnSpan 3
                            TextBlock.margin 8
                            TextBlock.text $"{model.logMessage}"
                        ]
                    ]
                ]
        )

type LiveViewWindow() as this =
    inherit HostWindow(Title = "LiveView", Width = 800, Height = 600)

    do
        this.Content <- Component(fun ctx -> LiveView.create "liveViewWindow-view" [])
#if DEBUG
        this.AttachDevTools()
#endif