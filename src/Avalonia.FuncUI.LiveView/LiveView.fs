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

        let getLayoutZoneHeitht (border:Border) =
            let margin = border.Margin
            let borderThickness = border.BorderThickness
            let padding = border.Padding

            margin.Top + margin.Bottom
            + borderThickness.Top + borderThickness.Bottom
            + padding.Top + padding.Bottom

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
        let previewConterntBorderBackground = new State<IBrush>(Brushes.Transparent)
        let previewItemViewBackground = new State<IBrush>(Brushes.Transparent)

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
                let previewItemViewBackground = ctx.usePassedRead Store.previewItemViewBackground

                ctx.attrs [
                    Component.margin 8
                    Component.horizontalAlignment HorizontalAlignment.Center
                    Component.verticalAlignment VerticalAlignment.Center
                    Component.background previewItemViewBackground.Current ]

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
                    ItemsControl.borderThickness 1
                    ItemsControl.borderBrush Brushes.Gray
                    ItemsControl.viewItems [
                        for name, content in contents do
                            Grid.create [
                                Grid.rowDefinitions "Auto,Auto"
                                Grid.children [
                                    TextBlock.create [
                                        TextBlock.row 0
                                        TextBlock.fontSize 20
                                        TextBlock.margin (4, 4, 4, 8)
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

    let private gridLineBrush =
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

    let create id attrs =
        let getTopLevelBackground control =
            TopLevel.GetTopLevel control
            |> Option.ofObj
            |> Option.bind (fun tl ->
                match Option.ofObj tl.Background with
                | Some(:? SolidColorBrush as cb) -> Some cb.Color
                | _ -> None)
            |> Option.defaultValue Colors.Transparent

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
                    ctx.usePassed Store.previewConterntBorderBackground

                let previewItemViewBackground = ctx.usePassed Store.previewItemViewBackground

                ctx.useEffect (
                    (fun () ->
                        previewConterntBorderBackground.Set gridLineBrush

                        getTopLevelBackground ctx.control
                        |> ImmutableSolidColorBrush
                        |> previewItemViewBackground.Set

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
                        CheckBox.create [
                            CheckBox.row 1
                            CheckBox.column 0
                            CheckBox.margin 8
                            CheckBox.content "Auto update preview content."
                            CheckBox.isChecked model.autoUpdate
                            CheckBox.onChecked (fun _ -> SetAutoUpdate true |> dispatch)
                            CheckBox.onUnchecked (fun _ -> SetAutoUpdate false |> dispatch)

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