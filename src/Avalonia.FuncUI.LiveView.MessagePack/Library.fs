﻿namespace Avalonia.FuncUI.LiveView.MessagePack

open System
open System.Net
open System.Net.Sockets
open System.Threading
open System.Threading.Tasks

open MessagePack
open MessagePack.FSharp
open MessagePack.Resolvers

open Avalonia.FuncUI.LiveView.Core.Types

module Settings =

    let iPAddress = IPAddress.Loopback
    let port = 8080


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

    let deserialize buff =
        MessagePackSerializer.Deserialize<MsgPack>(&buff, options)


module Server =

    /// クライアントへのデータ送信の失敗による例外なのかを判定するアクティブパターン。
    let inline (|SerializedStreamError|_|) (ex: exn) =

        let msg = "Error occurred while writing the serialized data to the stream."

        match ex with
        | :? AggregateException as es ->
            es.InnerExceptions
            |> Seq.tryPick (function
                | :? MessagePackSerializationException as e when e.Message = msg -> Some e
                | _ -> None)
        | _ -> None

    /// `AcceptTcpClientAsync`の結果を`Choice1Of2`でラップして`cont`を評価する。
    let inline acceptTcpClientAsync (listener: TcpListener) cont =
        async { return! listener.AcceptTcpClientAsync() |> Choice1Of2 |> cont }


    /// `serializeAsync`を実行する。
    /// `SerializedStreamError`に該当する例外した場合、復旧処理として`acceptTcpClientAsync`を実行する。
    let inline trySerializeAsync cont listener client token (msg: LiveViewAnalyzerMsg) =
        async {
            try
                do!
                    MsgPack.serializeAsync client token {
                        Content = msg.Content
                        Path = msg.Path
                    }

                return! Choice2Of2 client |> cont
            with SerializedStreamError e ->
                return! acceptTcpClientAsync listener cont
        }

    /// `TcpListener`を起動して、クライアントの接続を待ち受ける。
    let inline body ipAddress port token (inbox: MailboxProcessor<LiveViewAnalyzerMsg>) =
        async {
            let listener = TcpListener(ipAddress, port)

            listener.Start()

            use _ =
                { new IDisposable with
                    member _.Dispose() = listener.Stop()
                }

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


    /// `FuncUiAnalyzer`への接続が成功するまで`ConnectAsync`を行う。
    /// `SocketException`以外のエラーになった場合は中断。
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


    /// `FuncUiAnalyzer`からデータを購読するためのクライアント。
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
                | ValueSome buff ->
                    let msgPack = MsgPack.deserialize buff

                    onReceive {
                        LiveViewAnalyzerMsg.Content = msgPack.Content
                        Path = msgPack.Path
                    }

                | ValueNone -> ()
        }
        |> ignore

        { new IDisposable with
            member _.Dispose() = cts.Cancel()
        }