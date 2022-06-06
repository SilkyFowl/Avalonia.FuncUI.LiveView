module Avalonia.FuncUI.LiveView.Core.Types

open System

type LivePreviewFuncs = LivePreviewFuncs of string []

module LivePreviewFuncs =
    let toPreviewEvalText (LivePreviewFuncs funcs) =
        let children =
            funcs
            |> Array.mapi (fun i func ->
                $"""
        Grid.create [
            Grid.row %d{i}
            Grid.rowDefinitions "Auto,*"
            Grid.children [
                TextBlock.create [
                    TextBlock.row 0
                    TextBlock.text "%s{func}"
                ]
                Border.create [
                    Border.row 1
                    %s{func}()
                    |> Border.child
                ]
            ]
        ]
            """)
            |> String.concat ""

        let rowDefinitions =
            [ for _ = 1 to Array.length funcs do
                  "Auto" ]
            |> String.concat ","

        $"""
open Avalonia.Controls
open Avalonia.Media
open Avalonia.Layout
open Avalonia.FuncUI
open Avalonia.FuncUI.DSL

Grid.create [
    Grid.rowDefinitions "{rowDefinitions}"
    Grid.children [
{children}
    ]
]
        """

        

type Msg =
    { LivePreviewFuncs: string
      Content: string }

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

    type Post = Msg -> unit

    type Server(body) =
        let cts = new CancellationTokenSource()
        let actor = MailboxProcessor<Msg>.Start (body cts.Token, cts.Token)
        member _.Post = actor.Post
        member _.Dispose() = cts.Dispose()

        interface IDisposable with
            member this.Dispose() = this.Dispose()

module FuncUiLiveView =
    type Receive = unit -> Msg