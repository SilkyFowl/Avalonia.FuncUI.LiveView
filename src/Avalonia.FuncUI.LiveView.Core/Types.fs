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
    type ErrorNumber =
        { ErrorNumber: int
          ErrorNumberPrefix: string }

        member this.ErrorNumberText = $"{this.ErrorNumberPrefix}{this.ErrorNumber}"

    type Position = { Line: int; Column: int }

    type Range =
        { FileName: string
          Start: Position
          End: Position }

    [<RequireQualifiedAccess>]
    type DiagnosticSeverity =
        | Hidden
        | Info
        | Warning
        | Error

    type Diagnostic =
        { Range: Range
          Severity: DiagnosticSeverity
          Message: string
          Subcategory: string
          ErrorNumber: ErrorNumber }

    type PreviewFuncInfo =
        { msg: Msg
          previewFuncs: List<string * (unit -> obj)>
          warnings: Diagnostic list }

    type EvalErrorInfo =
        { msg: Msg
          error: exn
          warnings: Diagnostic list }

    type EvalResult = Result<PreviewFuncInfo, EvalErrorInfo>

    type IWatcherService =
        inherit IDisposable
        abstract member WatchingProjectInfo: option<ProjectInfo>
        abstract member Watch: ProjectInfo -> unit
        abstract member UnWatch: unit -> unit
        abstract member RequestEval: msg: Msg -> unit

        [<CLIEvent>]
        abstract member OnEvalResult: IEvent<EvalResult>

        [<CLIEvent>]
        abstract member OnLogMessage: IEvent<LogMessage>