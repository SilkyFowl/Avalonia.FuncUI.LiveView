/// 参考
/// https://fsharp.github.io/fsharp-compiler-docs/fcs/interactive.html
/// https://github.com/fsprojects/Avalonia.FuncUI/issues/147
module Avalonia.FuncUI.LiveView.FsiSession

open System
open System.IO
open FSharp.Compiler.Interactive.Shell
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.IO

open Avalonia.FuncUI.LiveView.Core.Types

let create () = Core.FsiSession.create()

open Avalonia.FuncUI
open Avalonia.FuncUI.VirtualDom

/// Evalを行う。
let evalInteraction
    (fsiSession: FsiEvaluationSession)
    (log: Logger)
    (evalText: IWritable<string>)
    (evalWarings: IWritable<_>)
    (evalResult: IWritable<obj>)
    =
    let time = DateTime.Now.ToString "T"

    let res, warnings = fsiSession.EvalInteractionNonThrowing evalText.Current

    warnings
    |> Array.map (fun w -> box $"Warning {w.Message} at {w.StartLine},{w.StartColumn}")
    |> evalWarings.Set

    match res with
    | Choice1Of2 (Some value) ->

        (LogInfo >> log) $"{time} Eval Success."

        match value.ReflectionValue with
        | :? Types.IView as view ->
            fun _ -> VirtualDom.create view |> evalResult.Set
            |> Avalonia.Threading.Dispatcher.UIThread.Post
        | other -> other |> evalResult.Set
    | Choice1Of2 None -> (LogError >> log) $"{time} Null or no result."
    | Choice2Of2 (exn: exn) ->
        (LogError >> log) $"{time} Eval Failed."

        [| box $"exception %s{exn.Message}" |]
        |> evalWarings.Set