module Avalonia.FuncUI.LiveView.Analyzer.Program

open Argu
open System
open System.IO
open System.Reflection
open System.Diagnostics
open System.Runtime.InteropServices

type AnalyzerPathArgs =
    | [<Unique; AltCommandLine("-code")>] Vsccode
    | [<Unique; AltCommandLine("-r")>] RelativeTo of string

    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Vsccode -> "display analyzer path for vscode."
            | RelativeTo _ -> "display analyzer path relative to the given path."

type Arguments =
    | [<CliPrefix(CliPrefix.None); Unique>] AnalyzerPath of ParseResults<AnalyzerPathArgs>
    | [<Unique>] Version

    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | AnalyzerPath _ -> "display analyzer path."
            | Version -> "display version."

let parser =
    ArgumentParser.Create<Arguments>(
        programName = "funcui-analyser",
        errorHandler =
            ProcessExiter(
                colorizer =
                    function
                    | ErrorCode.HelpText -> None
                    | _ -> Some ConsoleColor.Red
            )
    )

let printProductVersion () =
    let assembly = Assembly.GetExecutingAssembly()
    let info = FileVersionInfo.GetVersionInfo(assembly.Location)
    info.ProductVersion

module AnalyzerPath =
    let analyserDir =
        Directory.GetParent((typeof<Arguments>).Assembly.Location).FullName

    let print () = analyserDir.Replace("\\", "/")


    let printForVscode () =
        let homeDir =
            if RuntimeInformation.IsOSPlatform(OSPlatform.Windows) then
                Environment.GetEnvironmentVariable("USERPROFILE")
            else
                Environment.GetEnvironmentVariable("HOME")

        let result =
            if analyserDir.StartsWith(homeDir) then
                Path.GetRelativePath(homeDir, analyserDir).Replace("\\", "/")
            else
                analyserDir.Replace("\\", "/")
        $"${{userHome}}/{result}"

    let printRelative relativeTo =
        Path.GetRelativePath(relativeTo, analyserDir).Replace("\\", "/")


[<EntryPoint>]
let main (args: string[]) =
    try
        let results = parser.ParseCommandLine(inputs = args, raiseOnUsage = true)

        match results.GetAllResults() with
        | [ AnalyzerPath(nested) ] ->
            match nested.GetAllResults() with
            | [ RelativeTo path ] -> AnalyzerPath.printRelative path
            | [ Vsccode ] -> AnalyzerPath.printForVscode ()
            | _ -> AnalyzerPath.print ()
        | [ Version ] -> printProductVersion ()
        | _ -> parser.PrintUsage()

        |> printfn "%s"

        0
    with ex ->
        printfn "%s" ex.Message
        1