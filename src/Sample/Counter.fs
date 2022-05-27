namespace Sample



module Sample =
    open Avalonia.FuncUI
    open Avalonia.FuncUI.DSL
    open Avalonia.Controls

    let view =
        Component(fun ctx ->
            let n = ctx.usePassed Store.mun
            TextBox.create [
                TextBox.text $"Text{n.Current}"
            ]
        )
