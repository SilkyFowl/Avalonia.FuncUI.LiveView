namespace Avalonia.FuncUI.LiveView.Cli

open Avalonia
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.Themes.Simple

open Avalonia.FuncUI.LiveView

type App() =
    inherit Application()

    override this.Initialize() =
        this.Styles.Add(SimpleTheme())
        this.RequestedThemeVariant <- Styling.ThemeVariant.Dark

    override this.OnFrameworkInitializationCompleted() =
        match this.ApplicationLifetime with
        | :? IClassicDesktopStyleApplicationLifetime as desktopLifetime ->
            desktopLifetime.MainWindow <-
                MSBuildLocator.registerIfNotRegistered()
                LiveViewWindow()
            this.AttachDevTools()
        | _ -> ()

module Program =
    [<EntryPoint>]
    let main args =
        AppBuilder
            .Configure<App>()
            .UsePlatformDetect()
            .UseSkia()
            .StartWithClassicDesktopLifetime(args)