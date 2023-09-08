namespace Avalonia.FuncUI.LiveView.MessagePack

open Avalonia.FuncUI.LiveView.Types
open System
open System.Net

open MessagePack
open MessagePack.FSharp
open MessagePack.Resolvers

module Settings =
    val iPAddress: IPAddress
    val port: int

[<MessagePackObject>]
type MsgPack =
    { [<Key(1)>]
      FullName: string  
      [<Key(0)>]
      Contents: string[] }

module Server =
    val init: ipAddress: IPAddress -> port: int -> FuncUiAnalyzer.Server


module Client =
    val init: log: Logger -> address: IPAddress -> port: int -> onReceive: (Msg -> unit) -> IDisposable