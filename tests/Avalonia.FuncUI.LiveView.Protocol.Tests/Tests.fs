module ProtocolTests

open Xunit
open Avalonia.FuncUI.LiveView.Types
open Avalonia.FuncUI.LiveView.Protocol
open FsUnit.Xunit
open System.IO
open System.Threading
open System.Threading.Tasks
open FSharp.Control
open System
open System.Text
open System.Net.Sockets


/// Dummy Msg content
let msgContent =
    """
module Sample.ElmishSample.DefineDUInFile

open Avalonia.Controls
open Avalonia.Layout
open Avalonia.FuncUI
open Avalonia.FuncUI.DSL
open Avalonia.FuncUI.Elmish
open Avalonia.FuncUI.Builder
open Avalonia.FuncUI.LiveView

type State = { watermark: string }

let init = { watermark = "" }

type Msg =
    | SetWatermark of string
    | UnSetWatermark


let update msg state =
    match msg with
    | SetWatermark test -> { state with watermark = test }
    | UnSetWatermark -> { state with watermark = "" }

let view state dispatch =
    StackPanel.create [
        StackPanel.spacing 10
        StackPanel.children [
            TextBox.create [
                TextBox.watermark state.watermark
                TextBox.horizontalAlignment HorizontalAlignment.Stretch
            ]
            Button.create [
                Button.content "Set Watermark"
                Button.background "Blue"
                Button.onClick (fun _ -> dispatch (SetWatermark "test"))
                Button.horizontalAlignment HorizontalAlignment.Stretch
            ]

            Button.create [
                Button.content "Unset Watermark"
                Button.onClick (fun _ -> dispatch (UnSetWatermark))
                Button.horizontalAlignment HorizontalAlignment.Stretch
            ]
        ]
    ]

type Host() as this =
    inherit Hosts.HostControl()

    do
        Elmish.Program.mkSimple (fun () -> init) update view
        |> Program.withHost this
        |> Elmish.Program.run

[<LivePreview>]
let preview () = ViewBuilder.Create<Host> []
        """
        .Split('\n')


[<Fact>]
let ``SerializeAsync should serialize and write the message to the stream`` () =
    task {
        // Arrange
        let msg = Msg.create "Test" [| "Hello"; "World" |]

        use stream = new MemoryStream()
        let token = new CancellationToken()

        // Act
        do! Msg.serializeAsync stream token msg

        // Assert
        stream.Position <- 0L
        let reader = new StreamReader(stream)
        let serializedMsg = reader.ReadToEnd()

        let expected = Json.JsonSerializer.Serialize msg

        serializedMsg |> should equal expected
    }


[<Fact>]
let ``Serialize Success`` () =
    task {
        let ct = new CancellationToken()
        use stream = new MemoryStream()

        let value = Msg.create "Test" msgContent

        do! Msg.serializeAsync stream ct value
        stream.Position <- 0L
        let! actual = Msg.deserializeAsync stream ct

        actual |> should equal value
    }


[<Fact>]
let ``SerializeAsyncEnumerable should serialize and write the message sequence to the stream`` () =
    task {
        // Arrange
        let value = Msg.create "Test" [| "Hello"; "World" |]

        use stream = new MemoryStream()
        let token = new CancellationToken()

        // Act
        do! Encoding.UTF8.GetBytes "[" |> stream.WriteAsync
        do! Msg.serializeAsync stream token value
        do! Encoding.UTF8.GetBytes "]" |> stream.WriteAsync

        // Assert
        stream.Position <- 0L
        use reader = new StreamReader(stream)
        let serializedMsgSeq = reader.ReadToEnd()

        let expected = Json.JsonSerializer.Serialize [| value |]
        serializedMsgSeq |> should equal expected

    }


[<Fact>]
let ``DeserializeAsyncEnumerable should deserialize and read the message sequence from the stream`` () =
    task {
        // Arrange
        let msgs = List.init 10 (fun i -> Msg.create $"Test{i}" msgContent)
        let msgSeq = TaskSeq.ofList msgs

        use stream = new MemoryStream()
        let token = new CancellationToken()

        let jsonArrayStart = Encoding.UTF8.GetBytes "["
        let jsonArrayEnd = Encoding.UTF8.GetBytes "]"
        let jsonSeparator = Encoding.UTF8.GetBytes ","

        // Act
        do! stream.WriteAsync jsonArrayStart

        for msg in msgSeq do
            do! Msg.serializeAsync stream token msg
            do! stream.WriteAsync jsonSeparator

        do! stream.WriteAsync jsonArrayEnd

        stream.Position <- 0L

        do!
            Msg.deserializeAsyncEnumerable stream token
            |> TaskSeq.zip msgSeq
            |> TaskSeq.iter (fun (actual, expected) ->
                // Assert
                actual |> should equal expected)

    }


let (|TaskCancelled|_|) (ex: exn) =
    match ex with
    | :? AggregateException as e ->
        match List.ofSeq e.InnerExceptions with
        | [ :? TaskCanceledException ] -> Some(TaskCancelled e)
        | _ -> None
    | _ -> None

[<Fact>]
let ``Async Client Server Communication Success`` () =
    task {
        let cts = new CancellationTokenSource()
        // Arrange
        let msgs = List.init 10 (fun i -> Msg.create $"Test{i}" msgContent)

        let socketNameSuffix = Guid.NewGuid().ToString("N")

        let assertTask =
            task {
                use server = Server.createWithSuffix socketNameSuffix

                let received = new ResizeArray<Msg>()

                let onErr (logMSg: LogMessage) =
                    match logMSg with
                    | LogInfo msg
                    | LogDebug msg -> printfn $"Server: {msg[0..64]}..."
                    | LogError msg -> printfn $"Error: {msg}"

                server.OnLogMessage |> Event.add onErr

                server.OnMsgReceived |> Event.add received.Add

                while received.Count < List.length msgs do
                    cts.Token.ThrowIfCancellationRequested()
                    do! Tasks.Task.Delay(10)

                // Assert
                received |> should equalSeq msgs
            }

        let actTask =
            task {
                let client = Client.createWithSuffix socketNameSuffix

                while not client.IsConnected do
                    cts.Token.ThrowIfCancellationRequested()
                    do! Tasks.Task.Delay(10)

                for msg in msgs do
                    // Act
                    do! client.PostAsync msg

                client.Dispose()

                do! assertTask
            }

        cts.CancelAfter(10_000)
        do! actTask
    }


[<Fact>]
let ``Async Client Server Communication Failure`` () =
    task {
        let cts = new CancellationTokenSource()
        let timeout = 10_000
        cts.CancelAfter(timeout)

        let failIfTimeout (msg: string) whilePred =
            task {
                while not (whilePred ()) do
                    if cts.IsCancellationRequested then
                        Assert.Fail $"Test execution timed out after {timeout} milliseconds\n    {msg}"

                    do! Tasks.Task.Delay(10)
            }

        // Arrange
        let msgs = List.init 10 (fun i -> Msg.create $"Test{i}" msgContent)

        let socketNameSuffix = Guid.NewGuid().ToString("N")

        let actTask =
            task {
                let server = Server.createWithSuffix socketNameSuffix
                use client = Client.createWithSuffix socketNameSuffix

                let received = new ResizeArray<Msg>()
                server.OnMsgReceived |> Event.add received.Add


                do! failIfTimeout "client.IsConnected" (fun () -> client.IsConnected)

                for msg in msgs do
                    // Act
                    do! client.PostAsync msg
                    do! Tasks.Task.Delay(10)

                server.Dispose()
                do! Tasks.Task.Delay(10)

                try
                    while not cts.IsCancellationRequested do
                        // Act
                        do! client.PostAsync msgs[0]
                        do! Tasks.Task.Delay(10)

                    Assert.Fail "Client should throw Exception"
                with
                | :? IOException as e ->
                    e.Message |> should startWith "Unable to write data to the transport connection"
                | :? SocketException as e -> enum<SocketError> e.ErrorCode |> should equal SocketError.ConnectionReset
            }

        do! actTask
    }