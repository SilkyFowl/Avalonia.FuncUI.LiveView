module Avalonia.FuncUI.LiveView.Analyzer

open System
open System.IO
open System.Text.RegularExpressions
open Avalonia.FuncUI.LiveView
open Avalonia.FuncUI.LiveView.MessagePack
open Avalonia.FuncUI.LiveView.Core.Types
open FSharp.Analyzers.SDK

let server = Server.init Settings.iPAddress Settings.port

// `fsautocomplete`が終了するときにサーバも終了する。
AppDomain.CurrentDomain.ProcessExit.Add(fun _ -> server.Dispose())

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
                { OnLivePreviewFunc = fun v vs -> livePreviewFuncs.Add v.FullName
                  OnInvalidLivePreviewFunc =
                    fun v vs ->
                        errorMessages.Add
                            { Type = "FuncUi analyzer"
                              Message = "LivePreview must be unit -> IView<'t>"
                              Code = "OV001"
                              Severity = Error
                              Range = v.DeclarationLocation
                              Fixes = [] }
                  OnInvalidStringCall =
                    fun ex range m typeArgs argExprs ->

                        errorMessages.Add
                            { Type = "FuncUi analyzer"
                              Message = $"{ex.GetType().Name}:{ex.Message}"
                              Code = "OV002"
                              Severity = Error
                              Range = range
                              Fixes = [] } }
        )

        if Seq.isEmpty errorMessages
           && livePreviewFuncs.Count > 0 then
            { Content = String.concat Environment.NewLine ctx.Content }
            |> server.Post

        Seq.toList errorMessages