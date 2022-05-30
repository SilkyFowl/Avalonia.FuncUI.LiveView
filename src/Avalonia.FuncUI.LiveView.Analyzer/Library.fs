module Avalonia.FuncUI.LiveView.Analyzer

open System
open System.Text.RegularExpressions
open Avalonia.FuncUI.LiveView.MessagePack
open Avalonia.FuncUI.LiveView.Core.Types
open FSharp.Analyzers.SDK

let server = Server.init Settings.iPAddress Settings.port

// `fsautocomplete`が終了するときにサーバも終了する。
AppDomain.CurrentDomain.ProcessExit.Add(fun _ -> server.Dispose())


/// この文字列が含まれていたらEvalする。
[<Literal>]
let MatchText = "//#funcuianalyzer"

let (|LivePreview|_|) (ctx: Context) =
    let isEval =
        ctx.Content
        |> Array.exists (fun s ->
            String.IsNullOrEmpty s |> not
            && Regex.IsMatch(s, MatchText))

    if isEval then
        CodeEdited ctx.Content |> Some
    else
        None


/// AnalyzerでIDEによるF#コードの編集にフックできる。
/// これを利用してLiveViewを実現する。
[<Analyzer "FuncUiAnalyzer">]
let funcUiAnalyzer: Analyzer =
    fun ctx ->
        match ctx with
        | LivePreview msg -> server.Post msg
        | _ -> ()

        []