﻿namespace Sample

open System
open Avalonia
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.Controls
open Avalonia.Themes.Fluent
open Avalonia.FuncUI.Hosts
open Avalonia.FuncUI.LiveView

type MainWindow() as this =
    inherit HostWindow()
    do
        base.Title <- "Sample"
        base.Width <- 400.0
        base.Height <- 400.0
        this.Content <- Sample.cmp

        //this.VisualRoot.VisualRoot.Renderer.DrawFps <- true
        //this.VisualRoot.VisualRoot.Renderer.DrawDirtyRects <- true
#if DEBUG
        this.AttachDevTools()
#endif

type App() =
    inherit Application()

    [<Literal>]
    let FUNCUI_LIVEPREVIEW = "FUNCUI_LIVEPREVIEW"

    let livePreviewEnabled =
        match Environment.GetEnvironmentVariable FUNCUI_LIVEPREVIEW with
        | null -> false
        | "1" -> true
        | _ -> false

    override this.Initialize() =
        this.Styles.Add (FluentTheme(baseUri = null, Mode = FluentThemeMode.Dark))

    override this.OnFrameworkInitializationCompleted() =
        match this.ApplicationLifetime with
        | :? IClassicDesktopStyleApplicationLifetime as desktopLifetime ->
            desktopLifetime.MainWindow <-
            if livePreviewEnabled then
                LiveViewWindow() :> Window
            else
                MainWindow()
        | _ -> ()

module Program =

    [<EntryPoint>]
    let main(args: string[]) =
        AppBuilder
            .Configure<App>()
            .UsePlatformDetect()
            .UseSkia()
            .StartWithClassicDesktopLifetime(args)