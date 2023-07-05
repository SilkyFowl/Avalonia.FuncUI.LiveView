module Avalonia.FuncUI.LiveView.Analyzer

open System
open Avalonia.FuncUI.LiveView
open Avalonia.FuncUI.LiveView.MessagePack
open Avalonia.FuncUI.LiveView.Types
open FSharp.Analyzers.SDK
open System.Threading

module AnalyzerServer =
    let private initServer () =
        let initServer = Server.init ()
        let cts = new CancellationTokenSource()
        initServer.WaitForConnectionAsync cts.Token |> ignore
        initServer

    let private onMsgEvent = new Event<LiveViewAnalyzerMsg>()

    let triggerMsg = onMsgEvent.Trigger
    let private onMsg = onMsgEvent.Publish

    let mutable private server = initServer ()

    let private sub =
        onMsg
        |> Observable.filter (fun _ -> server.IsConnected)
        |> Observable.subscribe (fun msg ->
            task {
                let! ct = Async.CancellationToken
                let! result = server.TryPostAsync ct msg
                result |> Result.defaultWith (fun ex ->
                    server.Dispose()
                    server <- initServer ())
            }
            |> ignore)

    // When `fsautocomplete` terminates, server also terminates.
    AppDomain.CurrentDomain.ProcessExit.Add(fun _ ->
        sub.Dispose()
        server.Dispose())

let private nl = Environment.NewLine

/// Analyzer can hook into F# code editing by the IDE.
/// LiveView can be realized using this.
[<Analyzer "FuncUiAnalyzer">]
let funcUiAnalyzer: Analyzer =
    fun ctx ->
        /// Count of functions with `LivePreviewAttribute` in code
        let livePreviewFuncs = ResizeArray()
        let errorMessages = ResizeArray()
        let notSuppurtPatternMessages = ResizeArray()

        ctx.TypedTree.Declarations
        |> List.iter (
            FuncUIAnalysis.visitDeclaration {
                OnLivePreviewFunc = fun v vs -> livePreviewFuncs.Add v.FullName
                OnInvalidLivePreviewFunc =
                    fun v vs ->
                        errorMessages.Add {
                            Type = "FuncUi analyzer"
                            Message = "LivePreview must be unit -> 'a"
                            Code = "OV001"
                            Severity = Error
                            Range = v.DeclarationLocation
                            Fixes = []
                        }
                OnInvalidStringCall =
                    fun ex range m typeArgs argExprs ->
                        errorMessages.Add {
                            Type = "FuncUi analyzer"
                            Message = $"{ex.GetType().Name}:{ex.Message}"
                            Code = "OV002"
                            Severity = Error
                            Range = range
                            Fixes = []
                        }
                OnNotSuppurtPattern =
                    fun ex e ->
                        notSuppurtPatternMessages.Add {
                            Type = "FuncUi analyzer"
                            Message = $"FuncUiAnalyzer does not support this pattern.{nl}{ex.Message}"
                            Code = "OV000"
                            Severity = Warning
                            Range = e.Range
                            Fixes = []
                        }
            }
        )

        let errors =
            List.distinct [ yield! errorMessages; yield! notSuppurtPatternMessages ]


        if livePreviewFuncs.Count > 0 && errors.IsEmpty then
            AnalyzerServer.triggerMsg {
                Content = ctx.Content
                Path = ctx.FileName
            }

        errors