module Avalonia.FuncUI.LiveView.Views.LiveView

open Avalonia.Controls
open Avalonia.FuncUI
open Avalonia.FuncUI.DSL

open Avalonia.FuncUI.LiveView
open Avalonia.FuncUI.LiveView.Types
open Avalonia.FuncUI.LiveView.Views

let main id (props: WatchingProjectInfoView.Props) attrs =
    Component.create (
        $"live-view-main-{id}",
        fun ctx ->
            ctx.attrs [ yield! attrs ]

            let watchingProject = ctx.useState Deferred.NotStartedYet

            let onOpenSelectProject (info: ProjectInfo) =
                props.Log(LogInfo "Starting watch project...")
                watchingProject.Set(Deferred.Pending)

                backgroundTask {
                    props.Watcher.Current.Watch info

                    props.Log(LogInfo "Watch project started")

                    Deferred.Resolved props.Watcher.Current.WatchingProjectInfo
                    |> watchingProject.Set
                }
                |> ignore


            match watchingProject.Current with
            | Deferred.NotStartedYet
            | Deferred.Failed _ -> SelectProjectInfoView.create props.Log onOpenSelectProject
            | Deferred.Pending -> TextBlock.create [ TextBlock.text "Loading project..." ]
            | Deferred.Resolved info -> WatchingProjectInfoView.create "watch-project" props []
    )

let footer id (logs: IReadable<LogMessage list>) attrs =
    Component.create (
        $"live-view-footer-{id}",
        fun ctx ->
            ctx.attrs [ yield! attrs ]

            let logs = ctx.usePassedRead logs

            let logMessage =
                match logs.Current with
                | [] -> None
                | msg :: _ -> Some msg

            LogMessageView.create logMessage attrs
    )

let create () =
    Component(fun ctx ->
        let server = ctx.useStateLazy (Protocol.Server.create, false)

        let watcher = ctx.useStateLazy (Watcher.createService, false)

        let logs = ctx.useStateLazy<LogMessage list> ((fun () -> []), false)

        ctx.useEffect (
            (fun _ ->
                Disposables.create [
                    server.Current.OnLogMessage
                    |> Observable.subscribe (fun x -> logs.Set(x :: logs.Current))
                    watcher.Current.OnLogMessage
                    |> Observable.subscribe (fun x -> logs.Set(x :: logs.Current))
                ]),
            [ EffectTrigger.AfterInit ]
        )

        let props: WatchingProjectInfoView.Props =
            { Watcher = watcher
              Server = server
              Log = fun msg -> logs.Set(msg :: logs.Current) }

        DockPanel.create [
            DockPanel.children [
                footer "footer" logs [ DockPanel.dock Dock.Bottom; DockPanel.margin (4, 4) ]
                main "main" props [ DockPanel.dock Dock.Top; DockPanel.margin (4, 4) ]
            ]
        ])