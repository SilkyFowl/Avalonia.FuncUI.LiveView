module Avalonia.FuncUI.LiveView.Types

open System

type ReferenceSource =
    { Path: string
      ReferenceSourceTarget: string option
      FusionName: string option }

type ProjectInfo =
    { Name: string
      ProjectDirectory: string
      TargetPath: string
      TargetFramework: string
      ReferenceSources: ReferenceSource list }

type Msg =
    { FullName: string; Contents: string[] }

type LogMessage =
    | LogDebug of string
    | LogInfo of string
    | LogError of string

type Logger = LogMessage -> unit

module Protocol =
    open System.Threading.Tasks

    type IClient =
        inherit IDisposable
        abstract member PostAsync: msg: Msg -> Task<unit>
        abstract member IsConnected: bool

    type IServer =
        inherit IDisposable

        abstract member IsConnected: bool

        [<CLIEvent>]
        abstract member OnMsgReceived: IEvent<Msg>

        [<CLIEvent>]
        abstract member OnLogMessage: IEvent<LogMessage>

module Analyzer =
    type IAnalyzerService =
        abstract member Post: msg: Msg -> unit

module Watcher =
    type IWatcherService =
        inherit IDisposable
        abstract member Server: Protocol.IServer
        abstract member IsConnected: bool
        abstract member Watch: ProjectInfo -> unit
        abstract member Unwatch: unit -> unit

        [<CLIEvent>]
        abstract member OnLogMessage: IEvent<LogMessage>