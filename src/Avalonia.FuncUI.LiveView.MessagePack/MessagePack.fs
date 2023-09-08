namespace Avalonia.FuncUI.LiveView.MessagePack

open System
open System.Net
open System.Net.Sockets
open System.Threading
open System.Threading.Tasks

open MessagePack
open MessagePack.FSharp
open MessagePack.Resolvers

open Avalonia.FuncUI.LiveView.Types

module Settings =

    let iPAddress = IPAddress.Loopback
    let port = 8080


[<MessagePackObject>]
type MsgPack =
    { [<Key(1)>]
      FullName: string
      [<Key(0)>]
      Contents: string[] }

module internal MsgPack =
    let ofMsg (msg: Msg) : MsgPack =
        { FullName = msg.FullName
          Contents = msg.Contents }

    let toMsg (msgPack: MsgPack) : Msg =
        { FullName = msgPack.FullName
          Contents = msgPack.Contents }

    let resolver =
        Resolvers.CompositeResolver.Create(FSharpResolver.Instance, StandardResolver.Instance)

    let options = MessagePackSerializerOptions.Standard.WithResolver(resolver)

    let serializeAsync (client: TcpClient) token value =
        MessagePackSerializer.SerializeAsync<MsgPack>(client.GetStream(), value, options, token)
        |> Async.AwaitTask

    let deserialize buff =
        MessagePackSerializer.Deserialize<MsgPack>(&buff, options)


module Server =

    /// Determine if exception is due to failure to send data to the client.
    let inline (|SerializedStreamError|_|) (ex: exn) =

        let msg = "Error occurred while writing the serialized data to the stream."

        match ex with
        | :? AggregateException as es ->
            es.InnerExceptions
            |> Seq.tryPick (function
                | :? MessagePackSerializationException as e when e.Message = msg -> Some e
                | _ -> None)
        | _ -> None

    /// Wrap result of `AcceptTcpClientAsync` with `Choice1Of2` and evaluate `cont`.
    let inline acceptTcpClientAsync (listener: TcpListener) cont =
        async { return! listener.AcceptTcpClientAsync() |> Choice1Of2 |> cont }


    /// Execute `serializeAsync`.
    /// If Exception `SerializedStreamError` occurs, execute `acceptTcpClientAsync` as a recovery process.
    let inline trySerializeAsync cont listener client token msg =
        async {
            try
                do! MsgPack.ofMsg msg |> MsgPack.serializeAsync client token

                return! Choice2Of2 client |> cont
            with SerializedStreamError e ->
                return! acceptTcpClientAsync listener cont
        }

    /// Initialize `TcpListener`.
    /// continue until `OperationCanceledException` occurs.
    let rec initListenerAsync ipAddress port =
        async {
            try
                let! ct = Async.CancellationToken
                ct.ThrowIfCancellationRequested()
                let listener = TcpListener(ipAddress, port)

                listener.Start()

                return listener
            with
            | :? OperationCanceledException as e -> return raise e
            | ex ->
                do! Async.Sleep 1_000
                return! initListenerAsync ipAddress port
        }

    /// Invoke `TcpListener` to listen for client connections.
    let inline body ipAddress port token (inbox: MailboxProcessor<Msg>) =
        async {
            let! listener = initListenerAsync ipAddress port

            use _ =
                { new IDisposable with
                    member _.Dispose() = listener.Stop() }

            let rec loop (state: Choice<Task<TcpClient>, TcpClient>) =
                async {
                    let! msg = inbox.Receive()

                    match state with
                    | Choice1Of2 t when not t.IsCompleted -> return! loop state
                    | Choice1Of2 t -> return! trySerializeAsync loop listener t.Result token msg
                    | Choice2Of2 client -> return! trySerializeAsync loop listener client token msg
                }

            do! acceptTcpClientAsync listener loop
        }

    let init ipAddress port =
        new FuncUiAnalyzer.Server(body ipAddress port)


open System.Net.NetworkInformation

module Client =
    let isActiveTcpListener ipAddredd port =
        IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners()
        |> Array.exists (fun ep -> ep.Address = ipAddredd && ep.Port = port)


    /// Continue `ConnectAsync` until success of connection to `FuncUiAnalyzer`.
    /// If an error other than `SocketException` occurs, abort.
    let tryConnectAsync (log: Logger) retryMilliseconds address port (client: TcpClient) =
        task {
            let mutable hasConnedted = false

            while not hasConnedted do
                try
                    do! client.ConnectAsync(address = address, port = port)
                    hasConnedted <- true
                with :? SocketException as e ->
                    (LogError >> log) $"{e.SocketErrorCode}: {e.Message}"
                    do! Tasks.Task.Delay(millisecondsDelay = retryMilliseconds)
        }


    /// Client to subscribe to data from `FuncUiAnalyzer`.
    let init (log: Logger) address port onReceive =
        let cts = new CancellationTokenSource()

        let token = cts.Token

        task {
            use client = new TcpClient()

            while isActiveTcpListener address port |> not do
                (LogInfo >> log) "FuncUiAnalyzer server is not Active..."
                do! Tasks.Task.Delay 1000

            (LogInfo >> log) "start connect..."
            do! tryConnectAsync log 1000 address port client
            (LogInfo >> log) "Connedted!!"
            use reader = new MessagePackStreamReader(client.GetStream())

            while not token.IsCancellationRequested do

                let! result = reader.ReadAsync token
                (LogInfo >> log) $"read: {result}"


                match ValueOption.ofNullable result with
                | ValueSome buff -> MsgPack.deserialize buff |> MsgPack.toMsg |> onReceive

                | ValueNone -> ()
        }
        |> ignore

        { new IDisposable with
            member _.Dispose() = cts.Cancel() }