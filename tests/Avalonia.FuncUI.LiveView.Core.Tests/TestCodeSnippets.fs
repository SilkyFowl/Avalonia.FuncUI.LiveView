module LiveViewCoreTests.TestCodeSnippets


module String =
    let trimStart (s: string) = s.TrimStart(' ', '\t', '\n', '\r')

    let indent level (lines: string) =
        lines.Split('\n')
        |> Array.map (fun x -> String.replicate level " " + x)
        |> String.concat "\n"


let snipCounter rowDefinitions =
    $"""
[<RequireQualifiedAccess>]
module Store =
    open Avalonia.FuncUI
    let num = new State<_> 0

module Counter =
    open Avalonia.FuncUI
    open Avalonia.Controls
    open Avalonia.FuncUI.DSL
    open Avalonia.FuncUI.LiveView

    let view numState =

        Component.create (
            "Counter",
            fun ctx ->
                let state = ctx.usePassed numState

                Grid.create [
                    Grid.rowDefinitions "{rowDefinitions}"
                    Grid.children [ TextBlock.create [ TextBlock.text "Foo!!" ] ]
                ]
        )
        """
    |> String.trimStart

module Simple =
    let private snip rowDefinitions livePreviewPart =
        $"""
{snipCounter rowDefinitions}

{livePreviewPart}
        """

    let livePreviewFunc =
        snip
            "Auto,Auto"
            """
    [<LivePreview>]
    let preview () = view Store.num
    """

    let invalidLivePreviewFunc =
        snip
            "Auto,Auto"
            """
    [<LivePreview>]
    let preview2 =
        let n = 1
        view Store.num
    """

    let invalidStringCall = snip "test,Aut" ""

    let lidLivePreviewFuncAndInvalidStringCall =
        snip
            "Auto,test"
            """
    [<LivePreview>]
    let preview () = view Store.num
    """

module AllValueCaseDU =
    let singleCase =
        """
type SingleCase = SingleCase of int
        """
        |> String.trimStart

    let multiCase =
        """
type MultiCase =
    | Case1 of int
    | Case2 of string
    | Case3 of bool
        """
        |> String.trimStart

    let namedValueCase =
        """
type NamedValueCase = NamedValueCase of a:int
        """
        |> String.trimStart

    let valueCaseInParenthesis =
        """
type ValueCaseInParenthesis = ValueCaseInParenthesis of (int)
        """
        |> String.trimStart

    let functionValueCase =
        """
type FunctionValueCase = FunctionValueCase of (int -> string)
        """
        |> String.trimStart


    let genericSingleCase =
        """
type GenericSingleCase<'t> = GenericSingleCase of 't
        """
        |> String.trimStart

    let genericMultiCase =
        """
type GenericMultiCase<'t> =
    | Case1 of 't
    | Case2 of 't * int
    | Case3 of string
        """
        |> String.trimStart

module AnyNoValueCaseDU =
    let singleCase =
        """
type SingleCase = SingleCase
        """
        |> String.trimStart

    let multiCase =
        """
type MultiCase =
    | Case1
    | Case2
    | Case3 of bool
        """
        |> String.trimStart

    let genericCase =
        """
type GenericCase<'t> =
    | Hoge
    | Fuga of string
    | A of {|a:int; b:string; c: bool * string|}
    | B of ('t -> unit)
        """
        |> String.trimStart