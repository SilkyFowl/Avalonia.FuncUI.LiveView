module Avalonia.FuncUI.LiveView.Analyzer

open System
open System.Text.RegularExpressions
open Avalonia.FuncUI.LiveView
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
        let livePreviewFuncs = ResizeArray()
        let errorMessages = ResizeArray()

        ctx.TypedTree.Declarations
        |> List.iter (
            FuncUIAnalysis.visitDeclaration
                { OnLivePreviewFunc = fun v vs ->
                    livePreviewFuncs.Add v.FullName
                  OnInvalidLivePreviewFunc = fun v vs ->
                    errorMessages.Add
                      { Type = "FuncUi analyzer"
                        Message = "LivePreview must be unit -> IView<'t>"
                        Code = "OV001"
                        Severity = Error
                        Range = v.DeclarationLocation
                        Fixes = []}
                  OnInvalidStringCall =
                    fun range m typeArgs argExprs ->
                     errorMessages.Add 
                      { Type = "FuncUi analyzer"
                        Message = "Invalid string value, Parse failed."
                        Code = "OV002"
                        Severity = Error
                        Range = range
                        Fixes = []}
                     }
        )

        match ctx with
        | LivePreview msg -> server.Post msg
        | _ -> ()

        if errorMessages.Count <> 0 then
            Seq.toList errorMessages
        else
            List.empty