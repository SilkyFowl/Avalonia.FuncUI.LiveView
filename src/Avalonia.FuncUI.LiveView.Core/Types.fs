module Avalonia.FuncUI.LiveView.Core.Types

open System

type LiveViewAnalyzerMsg = { Content: string []; Path: string }

type LogMessage =
    | LogDebug of string
    | LogInfo of string
    | LogError of string

type Logger = LogMessage -> unit

[<AttributeUsage(AttributeTargets.Property)>]
type LivePreviewAttribute() =
    inherit Attribute()

module FuncUiAnalyzer =
    open System
    open System.Threading

    type Post = LiveViewAnalyzerMsg -> unit

    type Server(body) =
        let cts = new CancellationTokenSource()
        let actor = MailboxProcessor<LiveViewAnalyzerMsg>.Start(body cts.Token, cts.Token)
        member _.Post = actor.Post
        member _.Dispose() = cts.Dispose()

        interface IDisposable with
            member this.Dispose() = this.Dispose()

module FuncUiLiveView =
    type Receive = unit -> LiveViewAnalyzerMsg