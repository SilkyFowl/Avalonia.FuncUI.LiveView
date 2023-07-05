module Sample.ElmishSample.DefineDUInAnotherFile

open Avalonia.Controls
open Avalonia.Layout
open Avalonia.FuncUI
open Avalonia.FuncUI.DSL
open Avalonia.FuncUI.Elmish
open Avalonia.FuncUI.Builder
open Avalonia.FuncUI.LiveView
open Sample.ElmishModule

let view (state: State) dispatch =

    StackPanel.create [
        StackPanel.spacing 10.0
        StackPanel.children [
            TextBox.create [
                TextBox.watermark state.watermark
                TextBox.horizontalAlignment HorizontalAlignment.Stretch
            ]
            Button.create [
                Button.background "DarkBlue"
                Button.content "Set Watermark"
                Button.onClick (fun _ -> SetWatermark "test" |> dispatch)
                Button.horizontalAlignment HorizontalAlignment.Stretch
            ]

 
            Button.create [
                Button.content "Unset Watermark........"
                Button.onClick (fun _ -> SetWatermark "" |> dispatch)
                Button.horizontalAlignment HorizontalAlignment.Stretch
            ]
        ]
    ]


type Host() as this = 
    inherit Hosts.HostControl()

    do
        Elmish.Program.mkSimple (fun () -> init) update view
        |> Program.withHost this
        |> Program.runWithAvaloniaSyncDispatch ()

[<LivePreview>]
let preview () = ViewBuilder.Create<Host> []