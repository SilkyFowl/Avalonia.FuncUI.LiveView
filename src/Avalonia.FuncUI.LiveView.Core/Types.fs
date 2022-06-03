module Avalonia.FuncUI.LiveView.Core.Types

type Msg = CodeEdited of string []

type LogMessage =
    | LogDebug of string
    | LogInfo of string
    | LogError of string

type Logger = LogMessage -> unit

module FuncUiAnalyzer =
    open System
    open System.Threading

    type Post = Msg -> unit

    type Server(body) =
        let cts = new CancellationTokenSource()
        let actor = MailboxProcessor<Msg>.Start (body cts.Token, cts.Token)
        member _.Post = actor.Post
        member _.Dispose() = cts.Dispose()

        interface IDisposable with
            member this.Dispose() =
                this.Dispose()

module FuncUiLiveView =
    type Receive = unit -> Msg