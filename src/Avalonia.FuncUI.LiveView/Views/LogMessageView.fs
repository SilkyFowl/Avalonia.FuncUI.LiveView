module Avalonia.FuncUI.LiveView.Views.LogMessageView

open Avalonia.Controls
open Avalonia.Layout

open Avalonia.FuncUI.DSL

open Avalonia.FuncUI.LiveView
open Avalonia.FuncUI.LiveView.Types

let create (msg: LogMessage option) attrs =
    let removeLineBreaks (str: string) = str.ReplaceLineEndings(" ")

    let text =
        match msg with
        | Some(LogInfo msg) -> $"Info: {removeLineBreaks msg}"
        | Some(LogDebug msg) -> $"Debug: {removeLineBreaks msg}"
        | Some(LogError msg) -> $"Error: {removeLineBreaks msg}"
        | None -> "None." 

    TextBlock.create [ TextBlock.text text ]

[<LivePreview>]
let preview () =
    StackPanel.create [
        StackPanel.orientation Orientation.Vertical
        StackPanel.spacing 8
        StackPanel.children [
            create (Some(LogInfo "This is a test info message.")) []
            create (Some(LogDebug "This is a test debug message.")) []
            create (Some(LogError "This is a test error message.")) []
            create None []
        ]
    ]