module Avalonia.FuncUI.LiveView.Analyzer

open System
open System.Threading
open Avalonia.FuncUI.LiveView.Core.Types
open Avalonia.FuncUI.LiveView.MessagePack
open FSharp.Analyzers.SDK

let cts = new CancellationTokenSource()
let server = Server(Settings.iPAddress,Settings.port,cts)

// `fsautocomplete`が終了するときにサーバも終了する。
AppDomain.CurrentDomain.ProcessExit.Add(fun _ -> cts.Cancel())


/// AnalyzerでIDEによるF#コードの編集にフックできる。
/// これを利用してLiveViewを実現する。
[<Analyzer "FuncUiAnalyzer">]
let funcUiAnalyzer: Analyzer =
    fun ctx ->
        CodeEdited ctx.Content
        |> server.Post
        []
