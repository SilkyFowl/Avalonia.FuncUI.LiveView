namespace Avalonia.FuncUI.LiveView

module Disposable =
    open System
    let inline dispose (x: #IDisposable) = x.Dispose()

    let inline tryDispose (x: #IDisposable option) =
        match x with
        | Some x -> x.Dispose()
        | None -> ()

module Disposables =
    open System

    let inline create (xs: #IDisposable seq) =
        { new IDisposable with
            member _.Dispose() =
                for x in xs do
                    x.Dispose() }

module IO =
    open System.IO
    open System.Text.RegularExpressions

    let inline (|FilePathString|_|) (str: string) =
        if File.Exists str then Some(Path.GetFullPath str) else None

    let inline (|DirectoryPathString|_|) (str: string) =
        if Directory.Exists str then
            Some(Path.GetFullPath str)
        else
            None

    let inline (|MatchExtension|_|) (patten: string) (path: string) =
        if Regex.IsMatch(Path.GetExtension path, patten) then
            Some()
        else
            None