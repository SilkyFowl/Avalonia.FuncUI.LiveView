namespace Avalonia.FuncUI.LiveView

open Avalonia.FuncUI.LiveView.Types
open Avalonia.FuncUI.LiveView.Types.LiveView
open Avalonia.FuncUI.LiveView.Types.LiveView.PreviewService


[<AutoOpen>]
module Utils =
    module IDisposable =
        open System

        let create onDispose =
            { new IDisposable with
                member _.Dispose() : unit = onDispose ()
            }

        let empty =
            { new IDisposable with
                member _.Dispose() = ()
            }

        let dispose (disposable: IDisposable) = disposable.Dispose()

module PreviewService =
    open System
    open System.Threading

    module EvalState =
        let start state =
            match state with
            | Some(EvalState.success old) -> EvalState.inProgress(ValueSome old)
            | Some(EvalState.inProgress old)
            | Some(EvalState.failed(_, old)) -> EvalState.inProgress old
            | None -> EvalState.inProgress ValueNone

        let applyResult (result: EvalInteractionResult) state =
            match result, state with
            | Ok info, _ -> EvalState.success info
            | Error failed, Some(EvalState.success old) -> EvalState.failed(failed, ValueSome old)
            | Error failed, Some(EvalState.inProgress old)
            | Error failed, Some(EvalState.failed(_, old)) -> EvalState.failed(failed, old)
            | Error failed, None -> EvalState.failed(failed, ValueNone)

        let (|CurrentContent|_|) =
            function
            | EvalState.success { content = content } -> Some content
            | EvalState.inProgress(ValueSome { content = content })
            | EvalState.failed(_, ValueSome { content = content }) -> Some content
            | EvalState.inProgress ValueNone -> None
            | EvalState.failed(_, ValueNone) -> None


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


        let  receiveLiveViewAnalyzerMsg model (msg: LiveViewAnalyzerMsg) =
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
                            logMessage = LogMessage.error "eval parameter not found."
                      }
            else
                {
                    model with
                        logMessage = LogMessage.error "eval is running. Please wait a moment."
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



    let init () : (Model * Elmish.Effect<Msg> list) =
        {
            evalPendingMsgs = []
            evalStateMap = Map.empty
            evalInteractionDeferred = Waiting
            logMessage = LogMessage.info "init"
            autoUpdate = true
        },
        Elmish.Cmd.Empty

    let initWith mapper () : (Model * Elmish.Effect<Msg> list) =

        init () |> mapper

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

    let evalInteractionEffect
        syncContext
        (session: IPreviewSession)
        { path = path; content = content }
        (cts: CancellationTokenSource)
        : Elmish.Effect<Msg> =
        fun dispatch ->
            Async.StartImmediate(
                async {
                    let logInfo = LogMessage.info >> SetLogMessage >> dispatch
                    let logError = LogMessage.error >> SetLogMessage >> dispatch
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