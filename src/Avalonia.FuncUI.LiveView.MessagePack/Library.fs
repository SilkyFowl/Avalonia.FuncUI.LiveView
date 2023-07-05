namespace Avalonia.FuncUI.LiveView.MessagePack

open System
open System.Net
open System.Net.Sockets
open System.Threading

open MessagePack
open MessagePack.FSharp
open MessagePack.Resolvers

open Avalonia.FuncUI.LiveView.Types
open Avalonia.FuncUI.LiveView.Types.Analyzer

module Settings =

    [<Literal>]
    let PipeNameBase = "funcui.analyzer"


[<MessagePackObject>]
type MsgPack = {
    [<Key(0)>]
    Content: string[]
    [<Key(1)>]
    Path: string
}

module internal MsgPack =

    let resolver =
        Resolvers.CompositeResolver.Create(FSharpResolver.Instance, StandardResolver.Instance)

    let options = MessagePackSerializerOptions.Standard.WithResolver(resolver)

    let serializeAsync (client: TcpClient) token value =
        MessagePackSerializer.SerializeAsync<MsgPack>(client.GetStream(), value, options, token)
        |> Async.AwaitTask

    let inline (|BrokenPipeError|_|) (e: exn) =
        match e with
        | :? MessagePackSerializationException as e ->
            match e.InnerException with
            | :? IO.IOException as ioExn -> Some ioExn
            | _ -> None
        | _ -> None

    let trySerializeAsync ct stream (msg: LiveViewAnalyzerMsg) =
        task {
            try
                let msg = {
                    Path = msg.Path
                    Content = msg.Content
                }

                do! MessagePackSerializer.SerializeAsync<MsgPack>(stream, msg, options, ct)

                return Ok()
            with BrokenPipeError exn ->
                return Error exn

        }

    let deserialize buff =
        MessagePackSerializer.Deserialize<MsgPack>(&buff, options)



module Server =
    open System.IO.Pipes

    type internal MsgPackServer(?pipeNameFooter) =
        let pipeName =
            match pipeNameFooter with
            | Some footer -> $"{Settings.PipeNameBase}.%s{footer}"
            | None -> Settings.PipeNameBase

        let server = new NamedPipeServerStream(pipeName, PipeDirection.Out)

        interface IAnalyzerServer with
            member _.IsConnected = server.IsConnected
            member _.WaitForConnectionAsync ct = server.WaitForConnectionAsync ct
            member _.TryPostAsync ct msg = MsgPack.trySerializeAsync ct server msg
            member _.Dispose() = server.Close()

    let init () : IAnalyzerServer = new MsgPackServer()
    let initWith pipeNameFooter : IAnalyzerServer = new MsgPackServer(pipeNameFooter)


module Client =
    open System.IO.Pipes

    type internal MsgPackClient(?pipeNameFooter) =
        let pipeName =
            match pipeNameFooter with
            | Some footer -> $"{Settings.PipeNameBase}.%s{footer}"
            | None -> Settings.PipeNameBase
        let onLiveViewAnalyzerMsg = new Event<LiveViewAnalyzerMsg>()

        let pipeClient = new NamedPipeClientStream(".", pipeName, PipeDirection.In)

        let cts = new CancellationTokenSource()

        let startLoop () =
            let ct = cts.Token

            backgroundTask {
                use reader = new MessagePackStreamReader(pipeClient)

                while not ct.IsCancellationRequested do
                    ct.ThrowIfCancellationRequested()
                    let! buff = reader.ReadAsync(ct)

                    if buff.HasValue then
                        let msg = MsgPack.deserialize buff.Value

                        onLiveViewAnalyzerMsg.Trigger {
                            Path = msg.Path
                            Content = msg.Content
                        }
            }
            |> ignore

        interface IAnalyzerClient with
            member _.IsConnected = pipeClient.IsConnected

            member _.ConnectAsync ct = pipeClient.ConnectAsync ct

            member _.StartReceive() = startLoop ()

            member _.OnReceivedMsg: IObservable<LiveViewAnalyzerMsg> =
                onLiveViewAnalyzerMsg.Publish

            member _.Dispose() : unit =
                cts.Cancel()
                cts.Dispose()
                pipeClient.Close()

    let init () : IAnalyzerClient = new MsgPackClient()
    let initWith pipeNameFooter : IAnalyzerClient = new MsgPackClient(pipeNameFooter)
