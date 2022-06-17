namespace Sample

open System
open Avalonia.FuncUI
open Avalonia.FuncUI.Elmish


[<RequireQualifiedAccess>]
module Store =
    let num = new State<_> 0


module ElmishModule =
    type State = { watermark: string }
    let init = { watermark = "" }

    type Msg =
        | SetWatermark of string

    let update msg state =
        match msg with
        | SetWatermark test -> { state with watermark = test }