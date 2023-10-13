module Avalonia.FuncUI.LiveView.Types

open System

type ReferenceSource =
    { Path: string
      ReferenceSourceTarget: string
      FusionName: string }

type ProjectInfo =
    { Name: string
      ProjectDirectory: string
      TargetPath: string
      TargetFramework: string
      ReferenceSources: ReferenceSource list }

type Msg =
    { FullName: string
      Contents: string[]
      Timestamp: DateTimeOffset }

module Msg =
    let create (fullName: string) (contents: string[]) =
        { FullName = fullName
          Contents = contents
          Timestamp = DateTimeOffset.Now }

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
        abstract member WatchingProjectInfo: option<ProjectInfo>
        abstract member Watch: ProjectInfo -> unit
        abstract member Unwatch: unit -> unit
        abstract member RequestEval: path: string -> content: string array -> unit

        [<CLIEvent>]
        abstract member OnLogMessage: IEvent<LogMessage>