module Avalonia.FuncUI.LiveView.Core.Tests.FuncUIAnalysisTests

open Xunit
open FsUnitTyped

open FuncUIAnalysisTheoryData

[<Fact>]
let ``should work if no contain DU`` () =
    let results = createTestCode "" "" |> Helper.runFuncUIAnalysis

    results.livePreviewFuncs.Count |> shouldEqual 1

    results.invalidLivePreviewFuncs.Count
    |> shouldEqual 1

    results.invalidStringCalls.Count |> shouldEqual 1

let allValueCaseDUsData = createTheoryData allValueCaseDUs

[<Theory>]
[<MemberData(nameof allValueCaseDUsData)>]
let ``wont work if all case have value DU`` allValueCaseDU =
    let results =
        createTestCode "" allValueCaseDU
        |> Helper.runFuncUIAnalysis

    results.notSuppurtPattern.Count
    |> shouldBeGreaterThan 1

    results.livePreviewFuncs.Count |> shouldEqual 1

    results.invalidLivePreviewFuncs.Count
    |> shouldEqual 1

    results.invalidStringCalls.Count |> shouldEqual 1

let anyNoValueCaseDUsData = createTheoryData anyNoValueCaseDUs

[<Theory>]
[<MemberData(nameof anyNoValueCaseDUsData)>]
let ``should work If at least one no value case DU is the end of the code`` anyNoValueCase =
    let results =
        createTestCode "" anyNoValueCase
        |> Helper.runFuncUIAnalysis

    results.livePreviewFuncs.Count |> shouldEqual 1

    results.invalidLivePreviewFuncs.Count
    |> shouldEqual 1

    results.invalidStringCalls.Count |> shouldEqual 1
    results.notSuppurtPattern.Count |> shouldEqual 0

let module_with_some_value_after_DU =
    anyNoValueCaseDUs
    |> List.map (fun s -> createTestCode s "")
    |> createTheoryData

let module_after_DU_contain_module =
    nestedAnyNoValueCaseDUs
    |> List.map (fun s -> createTestCode s "")
    |> createTheoryData

[<Theory>]
[<MemberData(nameof module_with_some_value_after_DU)>]
[<MemberData(nameof module_after_DU_contain_module)>]
let ``wont work If module with some value after DU`` code =

    let ex =
        Assert.Throws<Sdk.EmptyException> (fun _ ->
            createTestCode "" code
            |> Helper.runFuncUIAnalysis
            |> ignore)

    ex.Message
    |> shouldContainText "typecheck error Duplicate definition of type, exception or module 'Counter'"