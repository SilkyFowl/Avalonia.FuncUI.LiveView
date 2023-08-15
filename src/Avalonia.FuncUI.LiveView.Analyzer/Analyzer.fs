module Avalonia.FuncUI.LiveView.Analyzer

open System
open Avalonia.FuncUI.LiveView
open Avalonia.FuncUI.LiveView.MessagePack
open Avalonia.FuncUI.LiveView.Core.Types
open FSharp.Analyzers.SDK

let server = Server.init Settings.iPAddress Settings.port

// `fsautocomplete`が終了するときにサーバも終了する。
AppDomain.CurrentDomain.ProcessExit.Add(fun _ -> server.Dispose())

let private nl = Environment.NewLine

/// AnalyzerでIDEによるF#コードの編集にフックできる。
/// これを利用してLiveViewを実現する。
[<Analyzer "FuncUiAnalyzer">]
let funcUiAnalyzer: Analyzer =
    fun ctx ->
        let livePreviewFuncs = ResizeArray()
        let errorMessages = ResizeArray()
        let notSuppurtPatternMessages = ResizeArray()

        ctx.TypedTree.Declarations
        |> List.iter (
            FuncUIAnalysis.visitDeclaration
                { OnLivePreviewFunc = fun v vs -> livePreviewFuncs.Add v.FullName
                  OnInvalidLivePreviewFunc =
                    fun v vs ->
                        errorMessages.Add
                            { Type = "FuncUi analyzer"
                              Message = "LivePreview must be unit -> 'a"
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
                              Fixes = [] }
                  OnNotSuppurtPattern =
                    fun ex e ->
                        notSuppurtPatternMessages.Add
                            { Type = "FuncUi analyzer"
                              Message = $"FuncUiAnalyzer does not support this pattern.{nl}{ex.Message}"
                              Code = "OV000"
                              Severity = Warning
                              Range = e.Range
                              Fixes = [] } }
        )

        if livePreviewFuncs.Count > 0 then
            // FuncUIAnalyzer自体のエラーはPreview対象がある時のみカウントする。
            errorMessages.AddRange notSuppurtPatternMessages

            // エラーがなければPreviewを実行
            if Seq.isEmpty errorMessages then
                { Content = String.concat Environment.NewLine ctx.Content } |> server.Post

        Seq.distinct errorMessages |> Seq.toList