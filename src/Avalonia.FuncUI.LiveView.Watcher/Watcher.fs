namespace Avalonia.FuncUI.LiveView

open System
open System.Text.RegularExpressions
open System.Threading
open System.Threading.Channels

open FSharp.Control
open FSharp.Compiler.Interactive.Shell

open Avalonia.FuncUI.LiveView.Types
open Avalonia.FuncUI.LiveView.Types.Watcher


module internal DiagnosticSeverity =
    open FSharp.Compiler.Diagnostics

    let ofFsharpDiagnosticSererity (severity: FSharpDiagnosticSeverity) : Watcher.DiagnosticSeverity =
        match severity with
        | FSharpDiagnosticSeverity.Error -> DiagnosticSeverity.Error
        | FSharpDiagnosticSeverity.Warning -> DiagnosticSeverity.Warning
        | FSharpDiagnosticSeverity.Info -> DiagnosticSeverity.Info
        | FSharpDiagnosticSeverity.Hidden -> DiagnosticSeverity.Hidden

    let toFsharpDiagnosticSererity (severity: Watcher.DiagnosticSeverity) : FSharpDiagnosticSeverity =
        match severity with
        | DiagnosticSeverity.Error -> FSharpDiagnosticSeverity.Error
        | DiagnosticSeverity.Warning -> FSharpDiagnosticSeverity.Warning
        | DiagnosticSeverity.Info -> FSharpDiagnosticSeverity.Info
        | DiagnosticSeverity.Hidden -> FSharpDiagnosticSeverity.Hidden

module internal Diagnostic =
    open FSharp.Compiler.Diagnostics
    open FSharp.Compiler.Text

    let ofFsharpDiagnostic (diagnostic: FSharpDiagnostic) : Watcher.Diagnostic =
        { Range =
            { FileName = diagnostic.FileName
              Start =
                { Line = diagnostic.StartLine
                  Column = diagnostic.StartColumn }
              End =
                { Line = diagnostic.EndLine
                  Column = diagnostic.EndColumn } }
          Severity = DiagnosticSeverity.ofFsharpDiagnosticSererity diagnostic.Severity
          Message = diagnostic.Message
          Subcategory = diagnostic.Subcategory
          ErrorNumber =
            { ErrorNumber = diagnostic.ErrorNumber
              ErrorNumberPrefix = diagnostic.ErrorNumberPrefix } }


    let toFsharpDiagnostic (diagnostic: Watcher.Diagnostic) : FSharpDiagnostic =
        let severity = DiagnosticSeverity.toFsharpDiagnosticSererity diagnostic.Severity

        let range =
            let { Range = { FileName = fileName
                            Start = { Line = startLine
                                      Column = startColumn }
                            End = { Line = endLine; Column = endColumn } } } =
                diagnostic

            let startPos = Position.mkPos startLine startColumn
            let endPos = Position.mkPos endLine endColumn

            Range.mkRange fileName startPos endPos

        FSharpDiagnostic.Create(
            severity,
            diagnostic.Message,
            diagnostic.ErrorNumber.ErrorNumber,
            range,
            diagnostic.Subcategory,
            diagnostic.ErrorNumber.ErrorNumberPrefix
        )

module Watcher =
    let inline tryDispose (x: #IDisposable option) =
        match x with
        | Some x -> x.Dispose()
        | None -> ()

    type private WatcherMsg = EvalScript of msg: Msg

    type internal Service() =

        let cts = new CancellationTokenSource()
        let msgChannel = Channel.CreateUnbounded<WatcherMsg>()
        let evalResultEvent = new Event<EvalResult>()
        let logMessageEvent = new Event<LogMessage>()


        let logInfo, logErr, logDebug =

            let logger case msg =
                let timestamp = DateTimeOffset.Now.ToString "T"
                logMessageEvent.Trigger(case $"{timestamp} %s{msg}")

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
                            | Some projectInfo, Some fsi, EvalScript msg ->
                                logDebug $"eval {msg.FullName}"
                                addOrUpdateFsCode msg.FullName msg.Contents

                                let res, warnings = fsi.EvalScriptNonThrowing msg.FullName

                                let warnings = warnings |> Array.map Diagnostic.ofFsharpDiagnostic |> Array.toList

                                match res with
                                | Choice1Of2() ->
                                    let previewFuncs = fsi.DynamicAssemblies |> Array.last |> FsiSession.getLivePreviews

                                    logDebug $"eval {msg.FullName} succeeded"

                                    Ok
                                        { msg = msg
                                          previewFuncs = previewFuncs
                                          warnings = warnings }
                                    |> evalResultEvent.Trigger
                                | Choice2Of2 ex ->
                                    logErr $"{typeof<Service>} failed: {ex}"

                                    Error
                                        { msg = msg
                                          error = ex
                                          warnings = warnings }
                                    |> evalResultEvent.Trigger

                        with ex ->
                            logErr $"{typeof<Service>} failed: {ex}"
            }

        let mutable isDisposed = false

        interface IWatcherService with
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


            member _.RequestEval msg =
                let msg = EvalScript msg

                while not (msgChannel.Writer.TryWrite msg) do
                    ()

            [<CLIEvent>]
            member _.OnEvalResult = evalResultEvent.Publish

            [<CLIEvent>]
            member _.OnLogMessage = logMessageEvent.Publish

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
                    (this :> IWatcherService).UnWatch()
                    LivePreviewFileSystem.resetFileSystem ()

    let createService () : IWatcherService = new Service()