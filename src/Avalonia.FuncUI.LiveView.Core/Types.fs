module Avalonia.FuncUI.LiveView.Core.Types

type Msg = CodeEdited of string []

type LogMessage =
    | LogDebug of string
    | LogInfo of string
    | LogError of string

type Logger = LogMessage -> unit

module FuncUiAnalyzer =
    type Post = Msg -> unit

module FuncUiLiveView =
    type Receive = unit -> Msg
