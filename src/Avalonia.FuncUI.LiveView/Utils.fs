namespace Avalonia.FuncUI.LiveView

module Disposable =
    open System
    let inline dispose (x: #IDisposable) = x.Dispose()

    let inline tryDispose (x: #IDisposable option) =
        match x with
        | Some x -> x.Dispose()
        | None -> ()