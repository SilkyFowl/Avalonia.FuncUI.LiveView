namespace Avalonia.FuncUI.LiveView.MessagePack

open Avalonia.FuncUI.LiveView.Core.Types
open System
open System.Net

module Settings =
    val iPAddress : IPAddress
    val port : int

module Server =
    val init: ipAddress: IPAddress -> port: int -> FuncUiAnalyzer.Server


module Client =
    val init: log: Logger -> address: IPAddress -> port: int -> setEvalText: (string -> unit) -> IDisposable