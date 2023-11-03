namespace Avalonia.FuncUI.LiveView

open System
open System.IO
open System.Text.RegularExpressions
open System.Reflection
open System.Runtime.Loader
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
    open System.Collections.Concurrent

    let inline tryDispose (x: #IDisposable option) =
        match x with
        | Some x -> x.Dispose()
        | None -> ()

    type FullPath = string

    /// watch project assembly and load ot reload assembly dynamically.
    /// To avaid build project, read assembly byte from byte[], not from file directly.
    ///
    /// > [TODO]:
    /// > implement `Unload` method to unload assembly manually after [dotnetfsharp/#15669](https://github.com/dotnet/fsharp/issues/15669) fixed.
    /// > `Unload` is not workint in FSI-generated AssemblyLoadContext. So, can't unload assembly manually by create collectible  AssemblyLoadContext.
    type PrijectAssemblyWatcher(info: ProjectInfo, onPrijectAssemblyUpdated: FullPath -> unit) =

        /// target assembly path to load assembly dynamically.
        let watchPaths =
            [ info.TargetPath
              yield!
                  info.ReferenceSources
                  |> List.filter (fun x -> x.ReferenceSourceTarget = "ProjectReference")
                  |> List.map (fun x -> x.Path) ]

        /// loaded assembly cache.
        let loadedAssemblyDictionary =
            let concurrencyLevel = Environment.ProcessorCount * 2
            let capacity = List.length watchPaths
            ConcurrentDictionary<string, Assembly>(concurrencyLevel, capacity)

        let tryRemove key =
            match loadedAssemblyDictionary.TryRemove(key = key) with
            | result, _ -> result

        /// if watching assembly updated, remove from cache.
        /// Updated assembly will be loaded again when assembly is requested after removed from cache.
        let assemblyFileWatchers =
            watchPaths
            |> List.groupBy (fun path -> Path.GetDirectoryName path)
            |> List.map (fun (dir, paths) ->
                let watcher = new FileSystemWatcher(dir)
                paths |> List.iter (Path.GetFileName >> watcher.Filters.Add)
                watcher.EnableRaisingEvents <- true
                watcher.NotifyFilter <- NotifyFilters.LastWrite

                watcher.Changed.Add(fun e ->
                    while loadedAssemblyDictionary.ContainsKey e.FullPath && not (tryRemove e.FullPath) do
                        ()

                    onPrijectAssemblyUpdated e.FullPath)

                watcher)

        /// if assembly not found in cache, load assembly from file bytes.
        ///
        /// > [NOTE]:
        /// > AssemblyLoadContext.Resolving event will continue to fire if assembly is not loaded into that AssemblyLoadContext, such as return assembly from another AssemblyLoadContext.
        /// > This behaiar is used to replace loaded project assembly.
        let resolvingHandler =
            Func<AssemblyLoadContext, AssemblyName, Assembly>(fun ctx name ->
                watchPaths
                |> List.tryFind (fun path ->
                    AssemblyName.ReferenceMatchesDefinition(name, AssemblyName.GetAssemblyName path))
                |> Option.map (fun path -> loadedAssemblyDictionary.GetOrAdd(path, File.ReadAllBytes >> Assembly.Load))
                |> Option.defaultValue null)

        do AssemblyLoadContext.Default.add_Resolving resolvingHandler

        let mutable isDisposed = false

        interface IDisposable with
            member _.Dispose() =
                if not isDisposed then
                    isDisposed <- true
                    AssemblyLoadContext.Default.remove_Resolving resolvingHandler
                    assemblyFileWatchers |> List.iter (fun x -> x.Dispose())

    type private WatcherMsg =
        | EvalScript of msg: Msg
        | ProjectAssemblyUpdated of fullPath: string

    /// watch project and eval script.
    type internal Service() =

        let cts = new CancellationTokenSource()

        /// channel for communication to runner.
        let msgChannel = Channel.CreateUnbounded<WatcherMsg>()

        let writeMsg msg =
            while not (msgChannel.Writer.TryWrite msg) do
                ()

        /// event for eval result.
        let evalResultEvent = new Event<EvalResult>()

        /// event for log message.
        let logMessageEvent = new Event<LogMessage>()


        let logInfo, logErr, logDebug =

            let logger case msg =
                let timestamp = DateTimeOffset.Now.ToString "T"
                logMessageEvent.Trigger(case $"{timestamp} %s{msg}")

            logger LogInfo, logger LogError, logger LogDebug

        /// project info to watch.
        let mutable projectInfo: ProjectInfo option = None

        /// watcher for project assembly.
        let mutable prijectAssemblyWatcher: PrijectAssemblyWatcher option = None

        /// fsi session for eval script.
        let mutable fsi: FsiEvaluationSession option = None

        /// add or update fs code to fsi session.
        let addOrUpdateFsCode path content =
            content
            // Fsi newline code for any OS is LF(\n).
            |> Array.map (fun s -> Regex.Replace(s, "\r?\n?$", "\n"))
            |> String.concat ""
            |> LivePreviewFileSystem.addOrUpdateFsCode path


        let watch (newProjectInfo: ProjectInfo) =
            match projectInfo with
            | Some current when current = newProjectInfo -> ()
            | _ ->
                tryDispose prijectAssemblyWatcher
                tryDispose fsi
                projectInfo <- Some newProjectInfo

                prijectAssemblyWatcher <-
                    Some(new PrijectAssemblyWatcher(newProjectInfo, ProjectAssemblyUpdated >> writeMsg))

                fsi <- Some(FsiSession.ofProjectInfo newProjectInfo)

        let unWatch () =
            tryDispose fsi
            tryDispose prijectAssemblyWatcher
            projectInfo <- None
            fsi <- None
            prijectAssemblyWatcher <- None

        do LivePreviewFileSystem.setLivePreviewFileSystem ()


        /// runner for eval script. if want to communicate to runner, write to msgChannel.
        let runner =
            task {
                while not cts.IsCancellationRequested do

                    // loop for read all messages from channel.
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
                            | Some projectInfo, Some fsi, ProjectAssemblyUpdated fullPath ->
                                logDebug $"project assembly updated: {fullPath}"

                                unWatch ()

                                watch projectInfo

                                logDebug $"fsi reset succeeded"

                        with ex ->
                            logErr $"{typeof<Service>} failed: {ex}"
            }

        let mutable isDisposed = false

        interface IWatcherService with
            member _.WatchingProjectInfo = projectInfo

            member _.Watch(newProjectInfo: ProjectInfo) = watch newProjectInfo

            member _.UnWatch() = unWatch ()

            member _.RequestEval msg = EvalScript msg |> writeMsg

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