namespace Avalonia.FuncUI.LiveView

[<AutoOpen>]
module Utils =
    type Deferred<'args, 'result> =
        | Waiting
        | HasNotStartedYet of 'args
        | InProgress
        | Resolved of 'result

    type AsyncOperationStatus<'t> =
        | StartRepuested
        | Started
        | Finished of 't

    module IDisposable =
        open System

        let create onDispose =
            { new IDisposable with
                member _.Dispose() : unit = onDispose ()
            }

module PreviewService =
    open System
    open System.Threading

    open FSharp.Compiler.Diagnostics

    open Avalonia.FuncUI.Types
    open Core.Types

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

    type EvalState =
        | InProgress of currentInfo: EvaledViewsInfo voption
        | Success of info: EvaledViewsInfo
        | Failed of failed: EvalFalled * currentInfo: EvaledViewsInfo voption

    module EvalState =
        let start state =
            match state with
            | Some(Success old) -> InProgress(ValueSome old)
            | Some(InProgress old)
            | Some(Failed(_, old)) -> InProgress old
            | None -> InProgress ValueNone

        let applyResult (result: EvalInteractionResult) state =
            match result, state with
            | Ok info, _ -> Success info
            | Error failed, Some(Success old) -> Failed(failed, ValueSome old)
            | Error failed, Some(InProgress old)
            | Error failed, Some(Failed(_, old)) -> Failed(failed, old)
            | Error failed, None -> Failed(failed, ValueNone)

        let (|CurrentContent|_|) =
            function
            | Success { content = content } -> Some content
            | InProgress(ValueSome { content = content })
            | Failed(_, ValueSome { content = content }) -> Some content
            | InProgress ValueNone -> None
            | Failed(_, ValueNone) -> None

    type Model = {
        evalPendingMsgs: list<EvalInteractionParam>
        evalStateMap: Map<string, EvalState>
        evalInteractionDeferred: Deferred<EvalInteractionParam, EvalInteractionResult>
        logMessage: LogMessage
        autoUpdate: bool
    }

    module Model =
        module EvalStateMap =
            let containEqual key content model =
                model.evalStateMap
                |> Map.exists (fun path state ->
                    (key = path)
                    && match state with
                       | EvalState.CurrentContent currentContent -> currentContent = content
                       | _ -> false)

            let tryPickContent key model =
                model.evalStateMap
                |> Map.tryPick (fun path state ->
                    match key = path, state with
                    | true, EvalState.CurrentContent content -> Some content
                    | _ -> None)

            let start model key =
                model.evalStateMap |> Map.change key (EvalState.start >> Some)

            let applyResult model key result =
                model.evalStateMap |> Map.change key (EvalState.applyResult result >> Some)

        module EvalInteractionDeferred =
            let isWaiting model =
                match model.evalInteractionDeferred with
                | Waiting -> true
                | _ -> false

        module EvalPendingMsgs =
            let add model msg =
                match List.tryLast model.evalPendingMsgs with
                | Some last when last.path = msg.Path ->
                    List.updateAt
                        (model.evalPendingMsgs.Length - 1)
                        {
                            path = msg.Path
                            content = msg.Content
                        }
                        model.evalPendingMsgs
                | _ ->
                    model.evalPendingMsgs
                    @ [
                        {
                            path = msg.Path
                            content = msg.Content
                        }
                    ]
                    |> List.distinct

            let tryPick model key =
                let rec loop acc msgs =
                    match msgs with
                    | [] -> None
                    | head :: tail ->
                        if head.path = key then
                            Some(head, acc @ tail)
                        else
                            loop (acc @ [ head ]) tail

                loop [] model.evalPendingMsgs


        let receiveLiveViewAnalyzerMsg model (msg: LiveViewAnalyzerMsg) =
            let shouldUpdate = EvalStateMap.containEqual msg.Path msg.Content model |> not

            if shouldUpdate then
                let evalStateMap' = EvalStateMap.start model msg.Path

                if model.autoUpdate && EvalInteractionDeferred.isWaiting model then
                    {
                        model with
                            evalStateMap = evalStateMap'
                            evalInteractionDeferred =
                                HasNotStartedYet {
                                    path = msg.Path
                                    content = msg.Content
                                }
                    }
                else
                    {
                        model with
                            evalStateMap = evalStateMap'
                            evalPendingMsgs = EvalPendingMsgs.add model msg
                    }
            else
                model

        let requestEvalInteraction model key =
            if EvalInteractionDeferred.isWaiting model then
                match EvalStateMap.tryPickContent key model with
                | Some content ->
                    let evalStateMap' = EvalStateMap.start model key

                    {
                        model with
                            evalStateMap = evalStateMap'
                            evalInteractionDeferred = HasNotStartedYet { path = key; content = content }
                    }
                | None ->
                    match EvalPendingMsgs.tryPick model key with
                    | Some(param, evalPendingMsgs') -> {
                        model with
                            evalInteractionDeferred = HasNotStartedYet param
                            evalPendingMsgs = evalPendingMsgs'
                      }
                    | None -> {
                        model with
                            logMessage = LogError "eval parameter not found."
                      }
            else
                {
                    model with
                        logMessage = LogError "eval is running. Please wait a moment."
                }

        let applyEvalInteractionResult model key result =
            let evalStateMap' = EvalStateMap.applyResult model key result

            match model.evalPendingMsgs with
            | head :: tail -> {
                model with
                    evalPendingMsgs = tail
                    evalInteractionDeferred = HasNotStartedYet head
                    evalStateMap = evalStateMap'
              }
            | [] -> {
                model with
                    evalInteractionDeferred = Waiting
                    evalStateMap = evalStateMap'
              }

    type Msg =
        | LiveViewAnalyzerMsg of LiveViewAnalyzerMsg
        | EvalInteraction of key: string * AsyncOperationStatus<EvalInteractionResult>
        | SetLogMessage of LogMessage
        | SetAutoUpdate of bool

    let init () =
        {
            evalPendingMsgs = []
            evalStateMap = Map.empty
            evalInteractionDeferred = Waiting
            logMessage = LogInfo "init"
            autoUpdate = true
        },
        []


    let update msg model =
        match msg with
        | LiveViewAnalyzerMsg msg -> Model.receiveLiveViewAnalyzerMsg model msg, []
        | EvalInteraction(key, StartRepuested) -> Model.requestEvalInteraction model key, []
        | EvalInteraction(_, Started) ->
            {
                model with
                    evalInteractionDeferred = Deferred.InProgress
            },
            []
        | EvalInteraction(key, (Finished result)) -> Model.applyEvalInteractionResult model key result, []
        | SetLogMessage logMessage -> { model with logMessage = logMessage }, []
        | SetAutoUpdate autoEval -> { model with autoUpdate = autoEval }, []

    type IFsSession =
        abstract addOrUpdateFsCode: path: string -> content: string[] -> unit
        abstract evalScriptNonThrowing: path: string -> content: string[] -> Result<EvaledViewsInfo, EvalFalled>


    let evalInteractionEffect
        syncContext
        (session: IFsSession)
        { path = path; content = content }
        (cts: CancellationTokenSource)
        : Elmish.Effect<Msg> =
        fun dispatch ->
            Async.StartImmediate(
                async {
                    let logInfo = LogInfo >> SetLogMessage >> dispatch
                    let logError = LogError >> SetLogMessage >> dispatch
                    let time = DateTime.Now.ToString "T"
                    EvalInteraction(path, Started) |> dispatch
                    logInfo $"{time} Eval Start..."

                    do! Async.SwitchToContext(syncContext)

                    session.addOrUpdateFsCode path content

                    let result =
                        match session.evalScriptNonThrowing path content with
                        | Ok _ as ok ->
                            logInfo $"{time} Eval Success."
                            ok
                        | Error _ as err ->
                            logError $"{time} Eval Failed."
                            err

                    EvalInteraction(path, Finished result) |> dispatch
                },
                cts.Token
            )

    let evalInteractionSubscribe session syncContext param : Elmish.Subscribe<Msg> =
        fun dispatch ->
            let cts = new CancellationTokenSource()
            evalInteractionEffect syncContext session param cts dispatch
            IDisposable.create cts.Cancel

    let subscription session syncContext (model: Model) : Elmish.Sub<Msg> = [
        match model.evalInteractionDeferred with
        | HasNotStartedYet param ->
            [ nameof evalInteractionSubscribe ], evalInteractionSubscribe session syncContext param
        | _ -> ()
    ]