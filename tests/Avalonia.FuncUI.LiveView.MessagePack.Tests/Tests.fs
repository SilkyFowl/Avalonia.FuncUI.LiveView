module Avalonia.FuncUI.LiveView.MessagePack.MessagePackTests

open Xunit
open FsUnit.Xunit

open Avalonia.FuncUI.LiveView.Types
open Avalonia.FuncUI.LiveView.MessagePack
open System.Threading
open System.Threading.Tasks

[<Literal>]
let PipeNameFooter = "tests"

let initServer () = Server.initWith PipeNameFooter
let initClient () = Client.initWith PipeNameFooter

type LiveViewAnalyzerMsgsData() =
    inherit TheoryData<LiveViewAnalyzerMsg list>()

    do
        base.Add [
            {
                Path = "test.txt"
                Content = [| "Hoge"; "Fuga" |]
            }
        ]

        base.Add [
            {
                Path = "test.txt"
                Content = [| "Hoge"; "Fuga" |]
            }
            {
                Path = "test2.txt"
                Content = [| "Foo"; "Bar" |]
            }
        ]


let msgOk: Result<_, System.IO.IOException> = Ok()

let serverTestWith testFn ct : Task =
    task {
        use server = initServer ()
        server.IsConnected |> should be False
        do! server.WaitForConnectionAsync ct
        server.IsConnected |> should be True

        do! testFn ct server
    }

let clientTestWith testFn ct : Task =
    task {
        use client = initClient ()
        client.IsConnected |> should be False
        do! client.ConnectAsync ct
        client.IsConnected |> should be True

        do! testFn ct client
    }


[<Theory(Timeout = 10_000)>]
[<ClassData(typeof<LiveViewAnalyzerMsgsData>)>]
let ``It can send and receive Msgs.`` (msgs: LiveViewAnalyzerMsg list) =
    let serverTask =
        serverTestWith (fun ct server ->
            task {
                for msg in msgs do
                    let! result = server.TryPostAsync ct msg
                    result |> should equal msgOk
            })

    let clientTask =
        clientTestWith (fun ct client ->
            task {
                let receivedMsg = ResizeArray()

                use _ =
                    client.OnReceivedMsg |> Observable.subscribe (fun msg -> receivedMsg.Add msg)

                client.StartReceive()

                while receivedMsg.Count < msgs.Length do
                    do! Task.Delay 5

                receivedMsg |> should equalSeq msgs
            })

    task {
        use cts = new CancellationTokenSource()

        do! Task.WhenAll [ serverTask cts.Token; clientTask cts.Token ]
    }

let errorAndRestartTest assertErrorFn =
    let msg: LiveViewAnalyzerMsg = {
        Path = "test.txt"
        Content = [| "Hoge"; "Fuga" |]
    }

    task {
        use cts = new CancellationTokenSource()
        let server = initServer ()

        let client = initClient ()
        do! Task.WhenAll [ server.WaitForConnectionAsync cts.Token; client.ConnectAsync cts.Token ]
        client.StartReceive()
        let! result = server.TryPostAsync cts.Token msg
        result |> should equal msgOk

        do! assertErrorFn server client msg cts.Token

        use server' = initServer ()
        use client' = initClient ()
        do! Task.WhenAll [ server'.WaitForConnectionAsync cts.Token; client'.ConnectAsync cts.Token ]
        client'.StartReceive()
        let! result_of_connecting_to_anotherClient_and_Posting = server'.TryPostAsync cts.Token msg
        result_of_connecting_to_anotherClient_and_Posting |> should equal msgOk
    }


[<Fact(Timeout = 10_000)>]
let ``After server is closed, client disConnected, and then the instance can be re-created and restarte.`` () =
    errorAndRestartTest (fun server client msg ct ->
        task {
            server.Dispose()
            do! Task.Delay 100
            client.IsConnected |> should be False

            client.Dispose()
        })

[<Fact(Timeout = 10_000)>]
let ``After client is closed, error occurs when server Post, and then the instance can be re-created and restarte.``
    ()
    =
    errorAndRestartTest (fun server client msg ct ->
        task {
            client.Dispose()

            let! result_of_1st_Post_after_Client_is_Closed = server.TryPostAsync ct msg
            result_of_1st_Post_after_Client_is_Closed |> should equal msgOk
            let! result_of_2nd_Post_after_Client_is_Closed = server.TryPostAsync ct msg
            result_of_2nd_Post_after_Client_is_Closed |> should not' (equal msgOk)


            server.Dispose()
        })