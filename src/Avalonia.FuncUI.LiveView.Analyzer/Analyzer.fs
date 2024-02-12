module Avalonia.FuncUI.LiveView.Analyzer.Library

open System
open System.Diagnostics
open System.IO
open System.Net.Sockets
open System.Threading
open System.Threading.Channels

open Avalonia.Skia

open FSharp.Analyzers.SDK
open FSharp.Control

open Avalonia.FuncUI.LiveView
open Avalonia.FuncUI.LiveView.Types
open Avalonia.FuncUI.LiveView.Types.Analyzer
open Avalonia.FuncUI.LiveView.Protocol

type AnalyzerService() =
    let logExn (ex: exn) =
        let ex = ex.GetBaseException()
        let msg = $"{ex.GetType().Name}:{ex.Message}"
        Trace.TraceError(msg)

    let cts = new CancellationTokenSource()

    let channel =
        Channel.CreateUnbounded<Msg>(UnboundedChannelOptions(SingleReader = true, SingleWriter = false))

    let runner =
        task {
            while not cts.IsCancellationRequested do
                try
                    while not (File.Exists Settings.socketPath) do
                        do! Tasks.Task.Delay(1000)

                    use client = Client.create ()

                    for msg in channel.Reader.ReadAllAsync(cts.Token) do
                        do! client.PostAsync msg
                with
                | :? IOException as ex ->
                    logExn ex
                    do! Tasks.Task.Delay(1_000)
                | :? SocketException as ex ->
                    logExn ex
                    do! Tasks.Task.Delay(1_000)
        }

    let mutable isDisposed = false

    let dispose _ =
        if not isDisposed then
            isDisposed <- true

            if
                channel.Writer.TryComplete()
                && not runner.IsCompleted
                && not (runner.Wait(3_000))
            then
                cts.Cancel()

            cts.Dispose()

    do AppDomain.CurrentDomain.ProcessExit.Add(dispose)


    interface IAnalyzerService with
        member __.PostAsync ct msg =
            async {
                if File.Exists Settings.socketPath then

                    do! task { do! channel.Writer.WriteAsync(msg, ct) } |> Async.AwaitTask
            }

    interface IDisposable with
        member __.Dispose() = dispose ()

let service: IAnalyzerService = new AnalyzerService()


let private nl = Environment.NewLine

/// Analyzer can hook into IDE editing of F# code.
/// This is used to realize LiveView.
[<EditorAnalyzer "FuncUiAnalyzer">]
let funcUiAnalyzer: EditorContext -> Async<Message list> =
    SkiaPlatform.Initialize()

    fun (ctx) ->
        match ctx.CheckFileResults, ctx.TypedTree with
        | Some checkResults, Some typedTree ->
            async {
                let livePreviewFuncs = ResizeArray()
                let errorMessages = ResizeArray()
                let notSuppurtPatternMessages = ResizeArray()

                let! token = Async.CancellationToken

                typedTree.Declarations
                |> List.iter (
                    FuncUIAnalysis.visitDeclaration
                        { OnLivePreviewFunc = fun v vs -> livePreviewFuncs.Add v.FullName
                          OnInvalidLivePreviewFunc =
                            fun v vs ->
                                errorMessages.Add
                                    { Type = "FuncUi analyzer"
                                      Message = "LivePreview must be unit -> 'a"
                                      Code = "OV001"
                                      Severity = Error
                                      Range = v.DeclarationLocation
                                      Fixes = [] }
                          OnInvalidStringCall =
                            fun ex range m typeArgs argExprs ->
                                errorMessages.Add
                                    { Type = "FuncUi analyzer"
                                      Message = $"{ex.GetType().Name}:{ex.Message}"
                                      Code = "OV002"
                                      Severity = Error
                                      Range = range
                                      Fixes = [] }
                          OnNotSuppurtPattern =
                            fun ex e ->
                                notSuppurtPatternMessages.Add
                                    { Type = "FuncUi analyzer"
                                      Message = $"FuncUiAnalyzer does not support this pattern.{nl}{ex.Message}"
                                      Code = "OV000"
                                      Severity = Warning
                                      Range = e.Range
                                      Fixes = [] } }
                )

                if
                    Seq.isEmpty errorMessages
                    && Seq.isEmpty notSuppurtPatternMessages
                    && not (Seq.isEmpty livePreviewFuncs)
                then
                    let contents =
                        [| for i in 0 .. ctx.SourceText.GetLineCount() - 1 do
                               yield ctx.SourceText.GetLineString i |]

                    do! Msg.create ctx.FileName contents |> service.PostAsync token

                return List.distinct [ yield! errorMessages; yield! notSuppurtPatternMessages ]
            }

        | _, _ -> async.Return []