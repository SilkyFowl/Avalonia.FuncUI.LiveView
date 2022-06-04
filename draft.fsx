// #r "nuget: Avalonia.Desktop"
// #r "nuget: FSharp.Compiler.Service"
#I "tests/Avalonia.FuncUI.LiveView.Core.Tests/bin/Debug/net6.0"
#r "Avalonia.Desktop"
#r "Avalonia.Controls"
#r "Avalonia.Visuals"
#r "Avalonia.FuncUI"
#r "Avalonia.FuncUI.DSL"
#r "FSharp.Compiler.Service"
#r "Avalonia.Styling.dll"

#load "draft-init.fsx"
#load "src/Avalonia.FuncUI.LiveView.Core/FuncUIAnalysis.fs"

open Avalonia.FuncUI.LiveView

#load "draft-input.fsx"
let t = ResizeArray()
open Draft
AnalysisDraft.references
AnalysisDraft.checkProjectResults
// let input = "DockPanel.background \"Fpp\""
// (AnalysisDraft.parseAndCheckSingleFile InputData.input).AssemblyContents.ImplementationFiles[1].Declarations
AnalysisDraft.declarations
|> List.iter (
    FuncUIAnalysis.visitDeclaration
        { OnLivePreviewFunc =
            fun v vs ->
                printfn $"{nameof v}:{v}"
                printfn $"{nameof vs}:{vs}"
                t.Add v
          OnInvalidLivePreviewFunc =
            fun v vs ->
                printfn $"{nameof v}:{v}"
                printfn $"{nameof vs}:{vs}"

          OnInvalidStringCall =
              fun range m typeArgs argExprs ->
                  [ $"{nameof range}: %A{range}"
                    $"{nameof m}: {m}"
                    $"{nameof m}: {m.FullName}"
                    $"{nameof m}: {m.ReturnParameter}"
                    $"{nameof typeArgs}: {typeArgs}"
                    $"{nameof argExprs}: {argExprs}" ]
                  |> List.iter (printfn "%s") }
)
t[0].DisplayName

let session = AnalysisDraft.createSession()
session.DynamicAssemblies.Length
session.EvalExpression "10"
let r,w  = session.EvalScriptNonThrowing "src/Sample/draft.fsx"
let r',w'  = session.EvalInteractionNonThrowing "Draft.Counter.view"
r'
session.DynamicAssemblies[0]
session.CurrentPartialAssemblySignature.Entities
session.DynamicAssemblies[0].GetTypes()
|> Seq.iter (fun t ->
  t.FullName |> printfn "%s")
System
    .Reflection
    .Assembly
    .Load("Avalonia.FuncUI")
    .GetReferencedAssemblies()
|> Seq.iter (fun asm -> printfn $"{asm.Name}")
$"{t[0]}"
