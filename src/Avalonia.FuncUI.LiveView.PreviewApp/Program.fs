namespace Avalonia.FuncUI.LiveView

open Avalonia
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.Styling

open Avalonia.FuncUI.LiveView
open Avalonia.FuncUI

open FsConfig
open Avalonia.Controls
open Types.PreviewApp
open Avalonia.FuncUI.LiveView.MessagePack

type App() =
    inherit Application()

    let disposables = ResizeArray()

    let client = new State<_>(Client.init ())

    let model = PreviewService.initState ()

    let currentProj = new State<_>(None)

    let setting: IWritable<Setting> =
        match Setting.getSetting () with
        | Ok setting -> new State<_>(setting)
        | Error err ->
            match err with
            | NotFound name -> failwithf "Setting variable %s not found" name
            | BadValue(name, value) -> failwithf "Setting variable %s has invalid value %s" name value
            | NotSupported msg -> failwith msg

    /// Change Theme as follows:
    /// 1. Close all currently open windows.
    /// 1. Change Theme.
    /// 1. Display a new MainWindow that has the same size and location as current one.
    let setTheme (t: Themes) =
        let app = Application.Current

        match app.ApplicationLifetime with
        | :? IClassicDesktopStyleApplicationLifetime as lifetime ->
            let currentShutdownMode = lifetime.ShutdownMode
            // ShutdownMode is temporarily changed because all windows need to be closed.
            lifetime.ShutdownMode <- ShutdownMode.OnExplicitShutdown

            let oldWindow = lifetime.MainWindow

            List.ofSeq lifetime.Windows
            |> List.filter (fun w -> w <> oldWindow)
            |> List.iter (fun w -> w.Close())

            /// New window, not opened until changes Theme.
            let newWindow =
                MainWindow(
                    setting,
                    client,
                    model,
                    currentProj,
                    Width = oldWindow.Width,
                    Height = oldWindow.Height,
                    Position = oldWindow.Position
                )

            oldWindow.Close()

            // Close all currently opened Windows and then change Theme.
            Themes.set t app

            lifetime.MainWindow <- newWindow
            newWindow.Show()

            // Restore ShutdownMode.
            lifetime.ShutdownMode <- currentShutdownMode
        | _ -> ()


    let onExit (e: ControlledApplicationLifetimeExitEventArgs) =
        printfn $"setting on Exit : {setting.Current}"
        disposables |> Seq.iter IDisposable.dispose

    override this.Initialize() =
        Themes.init this
        Themes.set setting.Current.theme this
        this.RequestedThemeVariant <- BuildinThemeVariantCase.toThemeVariant setting.Current.buildinThemeVariant


        setting
        |> State.readMap (fun s -> s.theme)
        |> State.readUnique
        |> IReadable.subscribe setTheme
        |> disposables.Add

        setting
        |> State.readMap (fun s -> s.buildinThemeVariant)
        |> State.readUnique
        |> State.readMap BuildinThemeVariantCase.toThemeVariant
        |> IReadable.subscribe (fun tb -> this.RequestedThemeVariant <- tb)
        |> disposables.Add

    override this.OnFrameworkInitializationCompleted() =

        match this.ApplicationLifetime with
        | :? IClassicDesktopStyleApplicationLifetime as desktopLifetime ->
            desktopLifetime.MainWindow <- MainWindow(setting, client, model, currentProj)
            desktopLifetime.Exit |> Observable.add onExit
        | _ -> ()

        base.OnFrameworkInitializationCompleted()

module Program =

    [<EntryPoint>]
    let main (args: string[]) =
        MSBuildLocator.registerInstanceMaxVersion()

        AppBuilder
            .Configure<App>()
            .UsePlatformDetect()
            .UseSkia()
            .StartWithClassicDesktopLifetime(args)