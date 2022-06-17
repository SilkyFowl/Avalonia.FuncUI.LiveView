module Draft.InputData


let input =
    """
#r "nuget: Avalonia.Desktop"
#r "nuget: JaggerJo.Avalonia.FuncUI"
#r "nuget: JaggerJo.Avalonia.FuncUI.DSL"
#r "nuget: JaggerJo.Avalonia.FuncUI.Elmish"

open System
open Avalonia.FuncUI
open Avalonia.FuncUI.DSL
open Avalonia.Controls

[<AttributeUsage(AttributeTargets.Property)>]
type LivePreviewAttribute () =
    inherit Attribute()

[<RequireQualifiedAccess>]
module Store =
    let num = new State<_> 0

type Foo =
    | Hoge of int
    | Fuga

module Counter =
    open Avalonia.FuncUI
    open Avalonia.Controls
    open Avalonia.Media
    open Avalonia.FuncUI.DSL
    open Avalonia.Layout

    let view numState =

        Component.create (
            "Counter",
            fun ctx ->
                let state = ctx.usePassed numState

                DockPanel.background "Ret"

                Grid.create [
                    Grid.rowDefinitions "Auto.df"
                    Grid.columnDefinitions "Fpp"
                    Grid.children [
                        TextBlock.create [
                            TextBlock.text "Foo!!"
                        ]
                    ]
                ]
        )

    [<LivePreview>]
    let preview () =
        view Store.num

    [<LivePreview>]
    let preview2 =
        let n = 1
        view Store.num

    [<LivePreview>]
    let preview3() =
        DockPanel.create [ ]
      """