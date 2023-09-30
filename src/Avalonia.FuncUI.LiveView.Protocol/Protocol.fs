namespace Avalonia.FuncUI.LiveView.Protocol

open System
open System.Net.Sockets
open System.Text
open System.Threading
open System.Threading.Channels

open FSharp.Control

open Avalonia.FuncUI.LiveView.Types

module Settings =
    open System.IO

    [<Literal>]
    let socketName = "avalonia-funcui-liveview.sock"

    let socketPath = Path.Combine(Path.GetTempPath(), socketName)



module Msg =
    open System.Text.Json
    open System.Collections.Generic

    let enumerableBufferSize = 1024


    let serializeAsync stream token value =
        JsonSerializer.SerializeAsync<Msg>(stream, value, cancellationToken = token)

    let deserializeAsync stream token =
        JsonSerializer.DeserializeAsync<Msg>(stream, cancellationToken = token)

    let serializeAsyncEnumerable stream token msgSeq =
        let options = JsonSerializerOptions(DefaultBufferSize = enumerableBufferSize)

        JsonSerializer.SerializeAsync<IAsyncEnumerable<Msg>>(stream, msgSeq, options, token)

    let deserializeAsyncEnumerable stream token =
        let options =
            JsonSerializerOptions(DefaultBufferSize = enumerableBufferSize, AllowTrailingCommas = true)

        JsonSerializer.DeserializeAsyncEnumerable<Msg>(stream, options, cancellationToken = token)

module Socket =
    let checkConnection (socket: Socket) =
        let originalBlockingMode = socket.Blocking

        try
            socket.Blocking <- false

            try
                let buffer = Array.create 1 0uy
                let success = socket.Send(buffer, 0, buffer.Length, SocketFlags.None) > 0
                success
            with :? SocketException as ex when
                ex.SocketErrorCode = SocketError.WouldBlock
                || ex.SocketErrorCode = SocketError.InProgress ->
                true
        finally
            socket.Blocking <- originalBlockingMode

type Client(?socketNameSuffix) =
    let cts = new CancellationTokenSource()

    let socketPath =
        match socketNameSuffix with
        | Some suffix -> Settings.socketPath.Replace("(.sock)", $"-{suffix}$1")
        | None -> Settings.socketPath

    let jsonArrayStart = Encoding.UTF8.GetBytes "["
    let jsonArrayEnd = Encoding.UTF8.GetBytes "]"
    let jsonSeparator = Encoding.UTF8.GetBytes ","

    let channel =
        Channel.CreateUnbounded<Msg>(UnboundedChannelOptions(SingleReader = true, SingleWriter = false))

    let client =
        new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified)


    let clientStreamTask =
        task {
            do! client.ConnectAsync(UnixDomainSocketEndPoint(socketPath), cts.Token)
            let stream = new NetworkStream(client)
            do! stream.WriteAsync(jsonArrayStart, cts.Token)

            return stream
        }

    interface Protocol.IClient with
        member _.IsConnected: bool = client.Connected

        member _.PostAsync(msg: Msg) =
            task {
                let! clientStream = clientStreamTask

                do! Msg.serializeAsync clientStream cts.Token msg

                do! client.SendAsync(jsonSeparator, SocketFlags.None, cts.Token) |> ValueTask.ignore

                do! clientStream.FlushAsync(cts.Token)
            }

        member _.Dispose() : unit =
            if clientStreamTask.IsCompletedSuccessfully && client.Connected then
                let clientStream = clientStreamTask.Result
                clientStream.Write jsonArrayEnd
                clientStream.Dispose()

            cts.Cancel()

            try
                if client.Connected then
                    client.Shutdown(SocketShutdown.Both)
            with _ ->
                ()

            client.Close()


type Server(?socketNameSuffix) =

    let socketPath =
        match socketNameSuffix with
        | Some suffix -> Settings.socketPath.Replace("(.sock)", $"-{suffix}$1")
        | None -> Settings.socketPath

    do
        if IO.File.Exists socketPath then
            IO.File.Delete socketPath

    let cts = new CancellationTokenSource()

    let msgEvent = new Event<Msg>()

    let logMsgEvent = new Event<LogMessage>()

    let logInfo = LogInfo >> logMsgEvent.Trigger
    let logError = LogError >> logMsgEvent.Trigger

    let listener =
        let socket =
            new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified)

        socket.Bind(UnixDomainSocketEndPoint(socketPath))
        socket.Listen(10)
        socket

    let _runner =
        task {
            while not cts.IsCancellationRequested do
                try

                    logInfo "Waiting for client connection..."
                    use! client = listener.AcceptAsync(cts.Token)

                    use _ =
                        { new IDisposable with
                            member _.Dispose() = client.Shutdown(SocketShutdown.Both) }

                    logInfo "Client connected."

                    use clientStream = new NetworkStream(client)

                    for msg in Msg.deserializeAsyncEnumerable clientStream cts.Token do
                        logInfo $"read: {msg}"
                        msg |> msgEvent.Trigger
                with
                | :? OperationCanceledException as e -> raise e
                | ex ->

                    logError $"{ex.GetType().Name}: {ex.Message}"
                    do! Tasks.Task.Delay(1_000)
        }

    interface Protocol.IServer with
        member _.IsConnected: bool = listener.Connected

        [<CLIEvent>]
        member _.OnMsgReceived: IEvent<Msg> = msgEvent.Publish

        [<CLIEvent>]
        member _.OnLogMessage: IEvent<LogMessage> = logMsgEvent.Publish

        member _.Dispose() : unit =
            try
                if listener.Connected then
                    listener.Shutdown(SocketShutdown.Both)
            with _ ->
                ()

            listener.Close()

            cts.Cancel()
            cts.Dispose()

open Protocol
module Client =

    let create () : IClient = new Client()
    let createWithSuffix (suffix: string) : IClient = new Client(suffix)

module Server =
    let create () : IServer = new Server()
    let createWithSuffix (suffix: string) : IServer = new Server(suffix)