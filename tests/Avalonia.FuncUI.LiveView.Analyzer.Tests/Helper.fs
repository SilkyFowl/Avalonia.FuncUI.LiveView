module LiveViewCoreTests.Helper

open System
open System.IO

open FSharp.Compiler.CodeAnalysis
open FSharp.Compiler.Text
open FsUnitTyped

open Avalonia.FuncUI.LiveView

let references =
    let entryAsm = Reflection.Assembly.GetEntryAssembly()
    let asmPath = Directory.GetParent entryAsm.Location

    let getDeps asmName =

        let asm = Path.Combine(asmPath.FullName, asmName) |> Reflection.Assembly.LoadFile

        asm.GetReferencedAssemblies()
        |> Seq.map (fun asm -> $"{asm.Name}.dll")
        |> Seq.insertAt 0 asmName

    let deps =
        seq {
            "Avalonia.FuncUI.LiveView.Attribute.dll"
            "Avalonia.dll"
            "Avalonia.Desktop.dll"
            "Avalonia.Diagnostics.dll"
            "Avalonia.FuncUI.dll"
            "Avalonia.FuncUI.Elmish.dll"
        }
        |> Seq.map getDeps
        |> Seq.concat
        |> Seq.sort
        |> Seq.distinct
        |> Seq.filter (fun lib -> Path.Combine(asmPath.FullName, lib) |> File.Exists)

    deps
    |> Seq.map (fun p -> $"-r:{Path.GetFileName p}")
    |> Seq.append (Seq.singleton $"-I:{asmPath.FullName}")
    |> Seq.toArray

open FSharp.Compiler.Symbols

type FuncUIAnalysisResult =
    { livePreviewFuncs: ResizeArray<FSharpMemberOrFunctionOrValue * list<list<FSharpMemberOrFunctionOrValue>>>
      invalidLivePreviewFuncs: ResizeArray<FSharpMemberOrFunctionOrValue * list<list<FSharpMemberOrFunctionOrValue>>>
      invalidStringCalls: ResizeArray<exn * range * FSharpMemberOrFunctionOrValue * list<FSharpType> * list<FSharpExpr>>
      notSuppurtPatterns: ResizeArray<Exception * FSharpExpr> }

type FuncUIAnalysisResultCount =
    { livePreviewFuncCount: int
      invalidLivePreviewFuncCount: int
      invalidStringCallCount: int
      notSuppurtPatternCount: int }

module FuncUIAnalysisResult =
    let count (result: FuncUIAnalysisResult) =
        { livePreviewFuncCount = result.livePreviewFuncs.Count
          invalidLivePreviewFuncCount = result.invalidLivePreviewFuncs.Count
          invalidStringCallCount = result.invalidStringCalls.Count
          notSuppurtPatternCount = result.notSuppurtPatterns.Count }

type FuncUIAnalysisService() =
    let checker: FSharpChecker = FSharpChecker.Create(keepAssemblyContents = true)

    let parseAndCheckSingleFileAsync (input) =

        async {
            let file = Path.ChangeExtension(Path.GetTempFileName(), "fsx")
            do! File.WriteAllTextAsync(file, input) |> Async.AwaitTask

            let! projOptions, _errors =
                checker.GetProjectOptionsFromScript(
                    file,
                    SourceText.ofString input,
                    assumeDotNetFramework = false,
                    otherFlags = references
                )

            return! checker.ParseAndCheckProject projOptions
        }


    let getDeclarations (results: FSharpCheckProjectResults) =
        results.Diagnostics |> shouldBeEmpty

        results.AssemblyContents.ImplementationFiles[0].Declarations

    let runFuncUIAnalysisAsync sourceCode =
        async {
            let livePreviewFuncs = ResizeArray()
            let invalidLivePreviewFuncs = ResizeArray()
            let invalidStringCalls = ResizeArray()
            let notSuppurtPattern = ResizeArray()

            let! results = sourceCode |> parseAndCheckSingleFileAsync

            results
            |> getDeclarations
            |> List.iter (
                FuncUIAnalysis.visitDeclaration
                    { OnLivePreviewFunc = fun v vs -> livePreviewFuncs.Add(v, vs)
                      OnInvalidLivePreviewFunc = fun v vs -> invalidLivePreviewFuncs.Add(v, vs)
                      OnInvalidStringCall =
                        fun ex range m typeArgs argExprs -> invalidStringCalls.Add(ex, range, m, typeArgs, argExprs)
                      OnNotSuppurtPattern = fun ex e -> notSuppurtPattern.Add(ex, e) }
            )

            return
                { livePreviewFuncs = livePreviewFuncs
                  invalidLivePreviewFuncs = invalidLivePreviewFuncs
                  invalidStringCalls = invalidStringCalls
                  notSuppurtPatterns = notSuppurtPattern }
        }

    let agent =
        MailboxProcessor<string * AsyncReplyChannel<FuncUIAnalysisResult>>.Start(fun (inbox) ->
            let rec loop () =
                async {
                    let! code, rc = inbox.Receive()
                    let! result = runFuncUIAnalysisAsync code
                    rc.Reply(result)

                    return! loop ()
                }

            loop ())

    member _.FuncUIAnalysisAsync code =
        agent.PostAndAsyncReply(fun rc -> code, rc)

    interface IDisposable with
        member _.Dispose() = agent.Dispose()


let funcUIAnalysisService = lazy new FuncUIAnalysisService()

let runFuncUIAnalysisAsync sourceCode =
    funcUIAnalysisService.Value.FuncUIAnalysisAsync sourceCode