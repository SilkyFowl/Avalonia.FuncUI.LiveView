namespace Avalonia.FuncUI.LiveView

open System
open System.Text.RegularExpressions
open System.Threading
open System.Threading.Channels

open FSharp.Control
open FSharp.Compiler.Interactive.Shell

open Avalonia.FuncUI.LiveView.Types

module Watcher =
    let inline tryDispose (x: #IDisposable option) =
        match x with
        | Some x -> x.Dispose()
        | None -> ()

    type private WatcherMsg = EvalScript of path: string * constent: string[]

    type Service() =

        let cts = new CancellationTokenSource()
        let msgChannel = Channel.CreateUnbounded<WatcherMsg>()
        let evalResultEvent = new Event<_>()
        let logMsgEvent = new Event<LogMessage>()

        let logInfo, logErr, logDebug =

            let logger case msg =
                let timestamp = DateTimeOffset.Now.ToString "T"
                logMsgEvent.Trigger(case $"{timestamp} %s{msg}")

            logger LogInfo, logger LogError, logger LogDebug

        let mutable projectInfo: ProjectInfo option = None
        let mutable fsi: FsiEvaluationSession option = None


        let addOrUpdateFsCode path content =
            content
            // Fsi newline code for any OS is LF(\n).
            |> Array.map (fun s -> Regex.Replace(s, "\r?\n?$", "\n"))
            |> String.concat ""
            |> LivePreviewFileSystem.addOrUpdateFsCode path

        do LivePreviewFileSystem.setLivePreviewFileSystem ()


        let runner =
            task {
                while not cts.IsCancellationRequested do
                    for msg in msgChannel.Reader.ReadAllAsync(cts.Token) do
                        try
                            match projectInfo, fsi, msg with
                            | None, _, _
                            | _, None, _ -> ()
                            | Some projectInfo, Some fsi, EvalScript(path, content) ->
                                logDebug $"eval {path}"
                                addOrUpdateFsCode path content

                                let res, warnings = fsi.EvalScriptNonThrowing path

                                match res with
                                | Choice1Of2() ->
                                    let control = fsi.DynamicAssemblies |> Array.last |> FsiSession.getLivePreviews
                                    logDebug $"eval {path} succeeded"
                                    Ok(path, control, warnings) |> evalResultEvent.Trigger
                                | Choice2Of2 ex ->
                                    logErr $"{typeof<Service>} failed: {ex}"
                                    Error(ex, warnings) |> evalResultEvent.Trigger

                        with ex ->
                            logErr $"{typeof<Service>} failed: {ex}"
            }

        let mutable isDisposed = false

        member _.WatchingProjectInfo = projectInfo

        member _.Watch(newProjectInfo: ProjectInfo) =
            match projectInfo with
            | Some current when current = newProjectInfo -> ()
            | _ ->

                tryDispose fsi
                projectInfo <- Some newProjectInfo
                fsi <- Some(FsiSession.ofProjectInfo newProjectInfo)

        member _.UnWatch() =
            tryDispose fsi
            projectInfo <- None
            fsi <- None


        member _.RequestEval(path, content) =
            let msg = EvalScript(path, content)

            while not (msgChannel.Writer.TryWrite msg) do
                ()

        [<CLIEvent>]
        member _.OnEvalResult = evalResultEvent.Publish

        [<CLIEvent>]
        member _.OnLogMsg = logMsgEvent.Publish

        interface IDisposable with
            member this.Dispose() =
                if not isDisposed then
                    isDisposed <- true

                    if
                        msgChannel.Writer.TryComplete()
                        && not runner.IsCompleted
                        && not (runner.Wait(1_000))
                    then
                        cts.Cancel()

                    cts.Dispose()
                    this.UnWatch()
                    LivePreviewFileSystem.resetFileSystem ()