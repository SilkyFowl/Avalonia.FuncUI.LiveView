namespace Avalonia.FuncUI.LiveView.Cli

open Avalonia
open Avalonia.Controls
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.Themes.Fluent

open Avalonia.FuncUI
open Avalonia.FuncUI.Hosts
open Avalonia.FuncUI.DSL

module Preview =

    let view () =
        Component(fun ctx ->

            TextBox.create [ TextBox.text "Hello World" ])

type MainWindow() as this =
    inherit HostWindow()

    do
        base.Title <- "Sample"
        base.Width <- 400.0
        base.Height <- 400.0
        this.Content <- Preview.view ()

type App() =
    inherit Application()

    override this.Initialize() =
        this.Styles.Add(FluentTheme())
        this.RequestedThemeVariant <- Styling.ThemeVariant.Dark

    override this.OnFrameworkInitializationCompleted() =
        match this.ApplicationLifetime with
        | :? IClassicDesktopStyleApplicationLifetime as desktopLifetime -> desktopLifetime.MainWindow <- MainWindow()
        | _ -> ()


// For more information see https://aka.ms/fsharp-console-apps
module Program =
    [<EntryPoint>]
    let main args =
        AppBuilder
            .Configure<App>()
            .UsePlatformDetect()
            .UseSkia()
            .StartWithClassicDesktopLifetime(args)