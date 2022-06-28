namespace Sample

open Avalonia.Controls
open Avalonia.Layout
open Avalonia.FuncUI
open Avalonia.FuncUI.DSL
open Avalonia.FuncUI.Elmish
open Avalonia.FuncUI.Builder
open Avalonia.FuncUI.LiveView.Core.Types
open System.Runtime.CompilerServices
open Avalonia
open Avalonia.Data
open Avalonia.Controls
open Avalonia.Controls.Presenters
open Avalonia.Controls.Shapes
open Avalonia.Controls.Primitives
open Avalonia.Controls.Templates
open Avalonia.Media
open Avalonia.Layout
open Avalonia.FuncUI
open Avalonia.FuncUI.Types
open Avalonia.FuncUI.VirtualDom
open Avalonia.FuncUI.DSL
open Avalonia.Controls.Templates
open Avalonia.Themes.Fluent

module Template =

    type AvaloniaObject with

        member inline this.InitalIndexer
            with set keysAndValues =
                for k: IndexerDescriptor, v: AvaloniaObject in keysAndValues do
                    this[k] <- v[k]

    let button =
        FuncControlTemplate<Button> (fun parent scope ->
            let (~~) = AvaloniaProperty.op_OnesComplement

            ContentPresenter(
                Name = "PART_ContentPresenter",
                InitalIndexer =
                    [ ~~ContentPresenter.BackgroundProperty, parent
                      ~~ContentPresenter.BorderBrushProperty, parent
                      ~~ContentPresenter.BorderThicknessProperty, parent
                      ~~ContentPresenter.ContentProperty, parent
                      ~~ContentPresenter.ContentTemplateProperty, parent
                      ~~ContentPresenter.CornerRadiusProperty, parent
                      ~~ContentPresenter.PaddingProperty, parent ],
                RecognizesAccessKey = true
            )
                .RegisterInNameScope scope)

    type ContentPresenter with
        static member content<'t when 't :> ContentPresenter>(obj: obj) : IAttr<'t> =
            AttrBuilder<'t>
                .CreateProperty<obj>(ContentPresenter.ContentProperty, obj, ValueNone)

    module Disposable =
        open System
        open System.Reactive.Disposables

        let composite (disposables: _ seq) : IDisposable = new CompositeDisposable(disposables)

    type IComponentContext with
        member inline this.useObservable init observable =
            let state = this.useState init
            this.useEffect ((fun _ -> observable |> Observable.subscribe state.Set), [ EffectTrigger.AfterInit ])
            state

    let buttonWithComponent =
        FuncControlTemplate<Button> (fun parent scope ->
            Component (fun ctx ->
                let setDynamicBrush (writable: IWritable<IBrush>) key =
                    match ResourceNodeExtensions.FindResource(parent, key) with
                    | :? IBrush as brush -> writable.Set brush
                    | _ -> ()

                let presenter = ctx.useState (Unchecked.defaultof<ContentPresenter>)

                let foreground = ctx.useState parent.Foreground
                let background = ctx.useState parent.Background
                let borderBrush = ctx.useState parent.BorderBrush

                ctx.attrs [
                    Component.onPointerEnter (fun _ ->
                        setDynamicBrush foreground "ButtonForegroundPointerOver"
                        setDynamicBrush borderBrush "ButtonBorderBrushPointerOver"
                        setDynamicBrush background "ButtonBackgroundPointerOver")
                    Component.onPointerLeave (fun _ ->
                        setDynamicBrush foreground "ButtonForeground"
                        setDynamicBrush borderBrush "ButtonBorderBrush"
                        setDynamicBrush background "ButtonBackground")
                    Component.onPointerPressed (fun _ ->
                        setDynamicBrush foreground "ButtonForegroundPressed"
                        setDynamicBrush borderBrush "ButtonBorderBrushPressed"
                        setDynamicBrush background "ButtonBackgroundPressed")
                    Component.onPointerReleased (fun _ ->
                        setDynamicBrush foreground "ButtonForegroundPointerOver"
                        setDynamicBrush borderBrush "ButtonBorderBrushPointerOver"
                        setDynamicBrush background "ButtonBackgroundPointerOver")
                ]

                let setDynamicBrushWithIsEnabled (control: IControl) =
                    if control.IsEnabled then
                        setDynamicBrush background "ButtonBackground"
                        setDynamicBrush foreground "ButtonForeground"
                        setDynamicBrush borderBrush "ButtonBorderBrush"
                    else
                        setDynamicBrush foreground "ButtonForegroundDisabled"
                        setDynamicBrush background "ButtonBackgroundDisabled"
                        setDynamicBrush borderBrush "ButtonBorderBrushDisabled"

                ctx.useEffect (
                    (fun _ ->
                        presenter.Current.RecognizesAccessKey <- true

                        presenter.Current.RegisterInNameScope scope
                        |> ignore

                        let (~~) = AvaloniaProperty.op_OnesComplement

                        [ ~~ContentPresenter.BackgroundProperty
                          ~~ContentPresenter.BorderBrushProperty
                          ~~ContentPresenter.BorderThicknessProperty
                          ~~ContentPresenter.CornerRadiusProperty
                          ~~ContentPresenter.ContentProperty
                          ~~ContentPresenter.ContentTemplateProperty
                          ~~ContentPresenter.PaddingProperty
                          ~~ContentPresenter.HorizontalAlignmentProperty
                          ~~ContentPresenter.VerticalAlignmentProperty
                          ~~TextBlock.ForegroundProperty ]
                        |> List.iter (fun prop -> presenter.Current[ prop ] <- parent[prop])

                        presenter.Current.ApplyTemplate()

                        Disposable.composite [
                            presenter.Current.Bind(TextBlock.ForegroundProperty, foreground.Observable)
                            Application.Current.Styles.CollectionChanged
                            |> Observable.subscribe (fun _ -> setDynamicBrushWithIsEnabled parent)
                            parent.GetObservable ContentPresenter.IsEnabledProperty
                            |> Observable.subscribe (fun _ -> setDynamicBrushWithIsEnabled parent)
                        ]),
                    [ EffectTrigger.AfterInit ]
                )

                View.createWithOutlet
                    presenter.Set
                    ContentPresenter.create
                    [ ContentPresenter.name "PART_ContentPresenter"
                      ContentPresenter.borderBrush borderBrush.Current
                      ContentPresenter.background background.Current ]))



module Preview =
    open System
    open Avalonia.Animation
    open Avalonia.Animation.Easings
    open Avalonia.Media
    open Avalonia.Styling



    [<LivePreview>]
    let templatedButtonWithComponent () =
        let getInputBrushe () =
            match List.ofSeq Application.Current.Styles with
            | (:? FluentTheme as fluent) :: _ ->
                if fluent.Mode = FluentThemeMode.Light then
                    Brushes.LightBlue
                elif fluent.Mode = FluentThemeMode.Dark then
                    Brushes.DarkBlue
                else
                    Brushes.Transparent
            | _ -> Brushes.Transparent

        Component.create (
            "templatedButtonWithComponent",
            fun ctx ->
                let input = ctx.useState ""

                let inputBrush = getInputBrushe () |> ctx.useState

                ctx.useEffect (
                    (fun _ ->
                        Application.Current.Styles.CollectionChanged
                        |> Observable.subscribe (fun _ -> getInputBrushe () |> inputBrush.Set)),
                    [ EffectTrigger.AfterInit ]
                )


                let transitions =
                    let t = Transitions()
                    DoubleTransition(Property=Button.OpacityProperty,Duration = TimeSpan.FromSeconds 3)
                    |> t.Add

                    t

                let opacity = ctx.useState 0.2

                DockPanel.create [

                    DockPanel.children [
                        Button.create [
                            Button.dock Dock.Left
                            Button.margin 8
                            Button.opacity opacity.Current
                            Button.onPointerEnter (
                                fun _ -> opacity.Set 1.0
                            )
                            Button.onPointerLeave (
                                fun _ -> opacity.Set 0.2
                            )
                            Button.transitions transitions
                            Button.content "Dark"
                            Button.onClick (fun _ ->
                                Application.Current.Styles[ 0 ] <-
                                    FluentTheme(baseUri = null, Mode = FluentThemeMode.Dark))
                        ]
                        Button.create [
                            Button.dock Dock.Left
                            Button.margin 8
                            Button.content "Light"
                            Button.onClick (fun _ ->
                                Application.Current.Styles[ 0 ] <-
                                    FluentTheme(baseUri = null, Mode = FluentThemeMode.Light))
                        ]
                        Button.create [
                            Button.dock Dock.Top
                            Button.content "Not isEnabled"
                            Button.isEnabled false
                            Button.template Template.buttonWithComponent
                        ]
                        StackPanel.create [
                            StackPanel.orientation Orientation.Horizontal
                            StackPanel.children [
                                TextBox.create [
                                    Button.dock Dock.Top
                                    TextBox.text input.Current
                                    TextBox.onTextChanged input.Set
                                ]
                                Button.create [
                                    Button.template Template.buttonWithComponent
                                    Button.content (
                                        StackPanel.create [
                                            StackPanel.orientation Orientation.Horizontal
                                            StackPanel.children [
                                                TextBlock.create [
                                                    TextBlock.background inputBrush.Current
                                                    TextBlock.margin (0, 0, 8, 0)
                                                    TextBlock.text $"input: {input.Current}"
                                                ]
                                                TextBlock.create [
                                                    TextBlock.text "Bar"
                                                ]
                                            ]
                                        ]
                                    )
                                ]
                            ]
                        ]
                    ]
                ]
        )

    [<LivePreview>]
    let templatedButton () =
        Component.create (
            "templatedButton",
            fun ctx ->
                let input = ctx.useState ""

                StackPanel.create [
                    StackPanel.children [
                        TextBox.create [
                            TextBox.text input.Current
                            TextBox.onTextChanged input.Set
                        ]
                        Button.create [
                            Button.content "Default"
                        ]
                        Button.create [
                            Button.template Template.button
                            Button.content (
                                StackPanel.create [
                                    StackPanel.orientation Orientation.Horizontal
                                    StackPanel.children [
                                        TextBlock.create [
                                            TextBlock.text input.Current
                                        ]
                                        TextBlock.create [
                                            TextBlock.text "Bar"
                                        ]
                                    ]
                                ]
                            )
                        ]
                    ]
                ]
        )