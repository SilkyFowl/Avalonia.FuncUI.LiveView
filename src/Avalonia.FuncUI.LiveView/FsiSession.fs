/// 参考
/// https://fsharp.github.io/fsharp-compiler-docs/fcs/interactive.html
/// https://github.com/fsprojects/Avalonia.FuncUI/issues/147
module Avalonia.FuncUI.LiveView.FsiSession

open System
open System.IO
open type System.Reflection.BindingFlags

open FSharp.Compiler.Interactive.Shell
open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.IO

open Avalonia.FuncUI.LiveView.Core.Types

let create () = Core.FsiSession.create ()

open Avalonia.FuncUI
open Avalonia.FuncUI.VirtualDom

let evalText striptPath value =
    $"""
#load "{striptPath}"

{value}    
    """

/// Evalを行う。
let evalInteraction
    (fsiSession: FsiEvaluationSession)
    (log: Logger)
    (tempFile: FileInfo)
    ({ Content = content
       LivePreviewFuncs = funcs })
    (evalWarings: IWritable<_>)
    (evalResult: IWritable<obj>)
    =
    let time = DateTime.Now.ToString "T"
    File.WriteAllText(tempFile.FullName, content)

    let res, warnings =
        evalText tempFile.FullName funcs
        |> fsiSession.EvalInteractionNonThrowing

    match res with
    | Choice1Of2 (Some value) ->
        warnings
        |> Array.map (fun w -> box $"Warning {w.Message} at {w.StartLine},{w.StartColumn}")
        |> evalWarings.Set

        (LogInfo >> log) $"{time} Eval Success."

        match value.ReflectionValue with
        | :? Types.IView as view ->
            fun _ -> VirtualDom.create view |> evalResult.Set
            |> Avalonia.Threading.Dispatcher.UIThread.Post
        | other -> other |> evalResult.Set
    | Choice1Of2 None -> (LogError >> log) $"{time} Null or no result."
    | Choice2Of2 (exn: exn) ->
        (LogError >> log) $"{time} Eval Failed."

        [| yield!
               warnings
               |> Array.map (fun w -> box $"Warning {w.Message} at {w.StartLine},{w.StartColumn}")
           box $"exception %s{exn.Message}" |]
        |> evalWarings.Set