namespace Avalonia.FuncUI.LiveView

open System
module Types =
    type LiveViewAnalyzerMsg = { Content: string[]; Path: string }

    [<RequireQualifiedAccess>]
    type LogMessage =
        | debug of string
        | info of string
        | error of string

    type Logger = LogMessage -> unit


    module Analyzer =
        open System.Threading
        open System.Threading.Tasks
        open System.IO

        type IAnalyzerServer =
            inherit IDisposable
            abstract IsConnected: bool
            abstract WaitForConnectionAsync: CancellationToken -> Task
            abstract TryPostAsync:   ct : CancellationToken ->   msg: LiveViewAnalyzerMsg     ->  Task<Result<unit,IOException>>
            
        type IAnalyzerClient =
            inherit IDisposable
            abstract IsConnected: bool
            abstract ConnectAsync: ct: CancellationToken -> Task
            abstract StartReceive : unit -> unit
            abstract OnReceivedMsg : IObservable<LiveViewAnalyzerMsg>
            

    module LiveView =
        type Deferred<'args, 'result> =
            | Waiting
            | HasNotStartedYet of 'args
            | InProgress
            | Resolved of 'result

        type AsyncOperationStatus<'t> =
            | StartRepuested
            | Started
            | Finished of 't

        module PreviewService =
            open Avalonia.FuncUI.Types
            open FSharp.Compiler.Diagnostics

            type EvalInteractionParam = { path: string; content: string[] }
            type ExtractFunc = (obj -> IView) -> (exn -> IView) -> Async<IView>

            type EvaledViewsInfo = {
                views: Map<string, ExtractFunc>
                diagnostic: FSharpDiagnostic[]
                content: string[]
                timestamp: DateTime
            }

            type EvalFalled = {
                exn: exn
                diagnostic: FSharpDiagnostic[]
                content: string[]
                timestamp: DateTime
            }

            type EvalInteractionResult = Result<EvaledViewsInfo, EvalFalled>

            [<RequireQualifiedAccess>]
            type EvalState =
                | inProgress of currentInfo: EvaledViewsInfo voption
                | success of info: EvaledViewsInfo
                | failed of failed: EvalFalled * currentInfo: EvaledViewsInfo voption

            type IPreviewSession =
                abstract addOrUpdateFsCode: path: string -> content: string[] -> unit
                abstract evalScriptNonThrowing: path: string -> content: string[] -> EvalInteractionResult


        open PreviewService
        type Model = {
            evalPendingMsgs: list<EvalInteractionParam>
            evalStateMap: Map<string, EvalState>
            evalInteractionDeferred: Deferred<EvalInteractionParam, EvalInteractionResult>
            logMessage: LogMessage
            autoUpdate: bool
        }

        type Msg =
            | LiveViewAnalyzerMsg of LiveViewAnalyzerMsg
            | EvalInteraction of key: string * AsyncOperationStatus<EvalInteractionResult>
            | SetLogMessage of LogMessage
            | SetAutoUpdate of bool

    module PreviewApp =
        type ProjArgsInfo = {
            Name: string
            ProjectDirectory: string
            TargetPath: string
            TargetFramework: string
            DotnetHostPath: string
            DotnetFscCompilerPath: string
            Args: string[]
        }

        type Themes =
            | Fluent
            | Simple

        type BuildinThemeVariantCase =
            | Default
            | Dark
            | Light

        type Setting = {
            autoUpdate: bool
            autoUpdateDebounceTime: TimeSpan
            enablePreviewItemViewBackground: bool
            theme: Themes
            buildinThemeVariant: BuildinThemeVariantCase
        }