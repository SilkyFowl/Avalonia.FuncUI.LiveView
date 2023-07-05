module Avalonia.FuncUI.LiveView.Core.Tests.FuncUIAnalysisTheoryData

open Xunit

let inline createTheoryData data =
    let theoryData = (TheoryData<_>())

    for dat in data do
        theoryData.Add dat

    theoryData

let private openCodeSnippet =
    """
open System
open Avalonia.FuncUI
open Avalonia.FuncUI.DSL
open Avalonia.Controls
open Avalonia.FuncUI.LiveView
"""

let private counterViewCodeSnippet =
    """
[<RequireQualifiedAccess>]
module Store =
    let num = new State<_> 0

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

                Grid.create [
                    Grid.rowDefinitions "test,Auto"
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
"""

let createTestCode beforeViewModule afterViewModule =
    $"""
{openCodeSnippet}

{beforeViewModule}

{counterViewCodeSnippet}

{afterViewModule}
"""

let allValueCaseDUs = [
    """
type Foo = Foo of int
                """
    """
type Bar =
    | Hoge of int
    | Fuga of string
                """
]

let anyNoValueCaseDUs = [
    """
type Foo = Foo
                    """
    """
type Bar =
    | Hoge
    | Fuga of string
                    """
    """
type Bar<'t> =
    | Hoge
    | Fuga of string
    | A of {|a:int; b:string; c: bool * string|}
    | B of ('t -> unit)
                    """
]

let nestedAnyNoValueCaseDUs =
    [
        """
    type Foo = Foo"""
        """
    type Bar =
        | Hoge
        | Fuga of string"""
        """
    type Bar<'t> =
        | Hoge
        | Fuga of string
        | A of {|a:int; b:string; c: bool * string|}
        | B of ('t -> unit)"""
    ]
    |> List.map (sprintf "module DUs =%s")