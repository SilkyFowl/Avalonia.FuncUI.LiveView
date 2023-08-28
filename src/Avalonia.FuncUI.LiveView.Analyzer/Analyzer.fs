module Avalonia.FuncUI.LiveView.Analyzer.Library

open System
open System.Reflection
open Avalonia.FuncUI.LiveView
open Avalonia.FuncUI.LiveView.MessagePack
open Avalonia.FuncUI.LiveView.Core.Types
open FSharp.Analyzers.SDK


let service =
    lazy
        let server = Server.init Settings.iPAddress Settings.port
        // When `fsautocomplete` terminates, the server also terminates.
        AppDomain.CurrentDomain.ProcessExit.Add(fun _ -> server.Dispose())
        server


let private nl = Environment.NewLine

/// Analyzer can hook into IDE editing of F# code.
/// This is used to realize LiveView.
[<Analyzer "FuncUiAnalyzer">]
let funcUiAnalyzer: Analyzer =
    fun ctx ->
        let service = service.Value
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

        if Seq.isEmpty errorMessages then
            // Errors in FuncUIAnalyzer itself are counted only when there is a Preview target.
            errorMessages.AddRange notSuppurtPatternMessages

            // Post if no errors.
            if not (Seq.isEmpty livePreviewFuncs) then
                service.Post { Content = String.concat nl ctx.Content }

        Seq.distinct errorMessages |> Seq.toList