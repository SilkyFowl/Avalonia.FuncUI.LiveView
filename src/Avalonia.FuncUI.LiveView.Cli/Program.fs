namespace Avalonia.FuncUI.LiveView.Cli

open Avalonia
open Avalonia.Controls
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.Themes.Fluent
open Avalonia.Themes.Simple

open Avalonia.FuncUI
open Avalonia.FuncUI.Hosts
open Avalonia.FuncUI.DSL

open Avalonia.FuncUI.LiveView.Protocol

module Preview =
    open Avalonia.Layout
    open Avalonia.Controls.Primitives

    let counter id =
        Component.create (
            id,
            fun ctx ->
                let num = ctx.useState 0

                DockPanel.create [
                    DockPanel.verticalAlignment VerticalAlignment.Center
                    DockPanel.horizontalAlignment HorizontalAlignment.Center
                    DockPanel.children [
                        NumericUpDown.create [
                            NumericUpDown.value num.Current
                            NumericUpDown.onValueChanged (fun v ->
                                if v.HasValue then
                                    int v.Value |> num.Set)
                            NumericUpDown.dock Dock.Bottom
                        ]
                        Button.create [
                            Button.width 64

                            Button.horizontalAlignment HorizontalAlignment.Center
                            Button.horizontalContentAlignment HorizontalAlignment.Center
                            Button.content "Reset"
                            Button.onClick (fun _ -> num.Set 0)
                            Button.dock Dock.Bottom
                        ]
                        Button.create [
                            Button.width 64
                            Button.horizontalAlignment HorizontalAlignment.Center
                            Button.horizontalContentAlignment HorizontalAlignment.Center
                            Button.content "-"
                            Button.onClick (fun _ -> num.Current - 10 |> num.Set)
                            Button.dock Dock.Bottom
                        ]
                        Button.create [
                            Button.width 64
                            Button.horizontalAlignment HorizontalAlignment.Center
                            Button.horizontalContentAlignment HorizontalAlignment.Center
                            Button.content "+"
                            Button.onClick (fun _ -> num.Current + 1 |> num.Set)
                            Button.dock Dock.Bottom
                        ]
                        TextBlock.create [
                            TextBlock.dock Dock.Top
                            TextBlock.fontSize 48.0
                            TextBlock.horizontalAlignment HorizontalAlignment.Center
                            TextBlock.text (string num.Current)
                        ]
                    ]
                ]
        )

    let simpleCmp id =
        Component.create (id, (fun ctx -> counter $"counter-{id}"))

    let fluentCmp id =
        Component.create (
            id,
            fun ctx ->
                ctx.control.Styles.Add(FluentTheme())

                counter $"counter-{id}"
        )

    type Props =
        { logs: IWritable<list<string>>
          msgs: IWritable<list<LiveView.Types.Msg>> }

    let client id { logs = logs; msgs = msgs } =

        Component.create (
            id,
            fun ctx ->
                let logs = ctx.usePassedRead logs
                let msgs = ctx.usePassedRead msgs

                let msgsView =
                    ListBox.create [
                        ListBox.viewItems [
                            for { FullName = fullName
                                  Contents = contents } in msgs.Current do
                                Expander.create [
                                    Expander.header (fullName)
                                    Expander.content (String.concat "\n" contents)
                                ]
                        ]
                    ]

                let lastestLogView =
                    TextBlock.create [
                        TextBlock.dock Dock.Bottom
                        TextBlock.maxLines 4
                        TextBlock.multiline false
                        TextBlock.horizontalScrollBarVisibility ScrollBarVisibility.Disabled
                        TextBlock.text (
                            match logs.Current with
                            | [] -> ""
                            | x :: _ -> $"{x}"
                        )
                    ]

                DockPanel.create [ DockPanel.children [ lastestLogView; msgsView ] ]
        )

    let view props =
        Component(fun ctx ->
            DockPanel.create [
                DockPanel.children [ simpleCmp "simple"; fluentCmp "fluent"; client "client" props ]
            ])

type MainWindow() as this =
    inherit HostWindow()

    let server = Server.create()

    let logs = new State<_>([]) :> IWritable<_>

    let addLog msg =
        let timeStamp = System.DateTime.Now.ToString("HH:mm:ss.fff")
        logs.Set($"{timeStamp}:{msg}" :: logs.Current)

    let msgs = new State<_>([]) :> IWritable<_>
    let addMsg msg = msgs.Set(msg :: msgs.Current)

    do
        server.OnLogMessage |> Event.add (fun msg -> addLog msg)
        server.OnMsgReceived |> Event.add (fun msg -> addMsg msg)

        this.Closed
        |> Event.add (fun _ ->
            logs.Dispose()
            msgs.Dispose()
            server.Dispose())

        base.Title <- "Sample"
        base.Width <- 400.0
        base.Height <- 400.0
        this.Content <- Preview.view { logs = logs; msgs = msgs }


type App() =
    inherit Application()

    override this.Initialize() =
        this.Styles.Add(SimpleTheme())
        this.RequestedThemeVariant <- Styling.ThemeVariant.Dark

    override this.OnFrameworkInitializationCompleted() =
        match this.ApplicationLifetime with
        | :? IClassicDesktopStyleApplicationLifetime as desktopLifetime ->
            desktopLifetime.MainWindow <- MainWindow()
            this.AttachDevTools()
        | _ -> ()


// For more information see https://aka.ms/fsharp-console-apps
module Program =
    [<EntryPoint>]
    let main args =
        AppBuilder
            .Configure<App>()
            .UsePlatformDetect()
            .UseSkia()
            .StartWithClassicDesktopLifetime(args)