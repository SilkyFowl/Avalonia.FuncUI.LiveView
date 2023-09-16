namespace LiveViewCoreTests

open Xunit
open TestCodeSnippets
open FsUnitTyped
open Helper


module ``No contain DU`` =

    [<Fact>]
    let ``a lidLivePreviewFunc`` () =
        task {
            let! results = Simple.livePreviewFunc |> runFuncUIAnalysisAsync

            FuncUIAnalysisResult.count results
            |> shouldEqual
                { livePreviewFuncCount = 1
                  invalidLivePreviewFuncCount = 0
                  invalidStringCallCount = 0
                  notSuppurtPatternCount = 0 }

        }

    [<Fact>]
    let ``a invalidLivePreviewFunc`` () =
        task {
            let! results = Simple.invalidLivePreviewFunc |> runFuncUIAnalysisAsync

            FuncUIAnalysisResult.count results
            |> shouldEqual
                { livePreviewFuncCount = 0
                  invalidLivePreviewFuncCount = 1
                  invalidStringCallCount = 0
                  notSuppurtPatternCount = 0 }
        }

    [<Fact>]
    let ``a invalidStringCall`` () =
        task {
            let! results = Simple.invalidStringCall |> runFuncUIAnalysisAsync

            FuncUIAnalysisResult.count results
            |> shouldEqual
                { livePreviewFuncCount = 0
                  invalidLivePreviewFuncCount = 0
                  invalidStringCallCount = 1
                  notSuppurtPatternCount = 0 }
        }



    [<Fact>]
    let `` a lidLivePreviewFunc and a invalidStringCall`` () =
        task {
            let! results = Simple.lidLivePreviewFuncAndInvalidStringCall |> runFuncUIAnalysisAsync

            FuncUIAnalysisResult.count results
            |> shouldEqual
                { livePreviewFuncCount = 1
                  invalidLivePreviewFuncCount = 0
                  invalidStringCallCount = 1
                  notSuppurtPatternCount = 0 }
        }


module DU_is_in_top_of_code =
    let private snipNoLivePreviewFunc du =
        $"""
module Foo

{du}

{snipCounter "Auto,Auto"}
        """
        |> String.trimStart

    let private snipWithLivePreviewFunc du =
        $"""
module Foo

{du}

{snipCounter "Auto,Auto"}

    [<LivePreview>]
    let preview () = view Store.num
        """

    [<Fact>]
    let ``All value case DU singleCase`` () =
        task {
            let! results = AllValueCaseDU.singleCase |> snipNoLivePreviewFunc |> runFuncUIAnalysisAsync

            FuncUIAnalysisResult.count results
            |> shouldEqual
                { livePreviewFuncCount = 0
                  invalidLivePreviewFuncCount = 0
                  invalidStringCallCount = 0
                  notSuppurtPatternCount = 0 }
        }

    [<Fact>]
    let ``All value case DU singleCase with livePreviewFunc`` () =
        task {
            let! results = AllValueCaseDU.singleCase |> snipWithLivePreviewFunc |> runFuncUIAnalysisAsync

            FuncUIAnalysisResult.count results
            |> shouldEqual
                { livePreviewFuncCount = 1
                  invalidLivePreviewFuncCount = 0
                  invalidStringCallCount = 0
                  notSuppurtPatternCount = 0 }
        }

    [<Fact>]
    let ``All value case DU multiCase`` () =
        task {
            let! results = AllValueCaseDU.multiCase |> snipNoLivePreviewFunc |> runFuncUIAnalysisAsync

            FuncUIAnalysisResult.count results
            |> shouldEqual
                { livePreviewFuncCount = 0
                  invalidLivePreviewFuncCount = 0
                  invalidStringCallCount = 0
                  notSuppurtPatternCount = 0 }
        }

    [<Fact>]
    let ``All value case DU multiCase with livePreviewFunc`` () =
        task {
            let! results = AllValueCaseDU.multiCase |> snipWithLivePreviewFunc |> runFuncUIAnalysisAsync

            FuncUIAnalysisResult.count results
            |> shouldEqual
                { livePreviewFuncCount = 1
                  invalidLivePreviewFuncCount = 0
                  invalidStringCallCount = 0
                  notSuppurtPatternCount = 0 }
        }

    [<Fact>]
    let ``All value case DU namedValueCase`` () =
        task {
            let! results = AllValueCaseDU.namedValueCase |> snipNoLivePreviewFunc |> runFuncUIAnalysisAsync

            FuncUIAnalysisResult.count results
            |> shouldEqual
                { livePreviewFuncCount = 0
                  invalidLivePreviewFuncCount = 0
                  invalidStringCallCount = 0
                  notSuppurtPatternCount = 0 }
        }

    [<Fact>]
    let ``All value case DU namedValueCase with livePreviewFunc`` () =
        task {
            let! results =
                AllValueCaseDU.namedValueCase
                |> snipWithLivePreviewFunc
                |> runFuncUIAnalysisAsync

            FuncUIAnalysisResult.count results
            |> shouldEqual
                { livePreviewFuncCount = 1
                  invalidLivePreviewFuncCount = 0
                  invalidStringCallCount = 0
                  notSuppurtPatternCount = 0 }
        }

    [<Fact>]
    let ``All value case DU valueCaseInParenthesis`` () =
        task {
            let! results =
                AllValueCaseDU.valueCaseInParenthesis
                |> snipNoLivePreviewFunc
                |> runFuncUIAnalysisAsync

            FuncUIAnalysisResult.count results
            |> shouldEqual
                { livePreviewFuncCount = 0
                  invalidLivePreviewFuncCount = 0
                  invalidStringCallCount = 0
                  notSuppurtPatternCount = 0 }
        }

    [<Fact>]
    let ``All value case DU valueCaseInParenthesis with livePreviewFunc`` () =
        task {
            let! results =
                AllValueCaseDU.valueCaseInParenthesis
                |> snipWithLivePreviewFunc
                |> runFuncUIAnalysisAsync

            FuncUIAnalysisResult.count results
            |> shouldEqual
                { livePreviewFuncCount = 1
                  invalidLivePreviewFuncCount = 0
                  invalidStringCallCount = 0
                  notSuppurtPatternCount = 0 }
        }

    [<Fact>]
    let ``All value case DU functionValueCase`` () =
        task {
            let! results =
                AllValueCaseDU.functionValueCase
                |> snipNoLivePreviewFunc
                |> runFuncUIAnalysisAsync

            FuncUIAnalysisResult.count results
            |> shouldEqual
                { livePreviewFuncCount = 0
                  invalidLivePreviewFuncCount = 0
                  invalidStringCallCount = 0
                  notSuppurtPatternCount = 0 }
        }

    [<Fact>]
    let ``All value case DU functionValueCase with livePreviewFunc`` () =
        task {
            let! results =
                AllValueCaseDU.functionValueCase
                |> snipWithLivePreviewFunc
                |> runFuncUIAnalysisAsync

            FuncUIAnalysisResult.count results
            |> shouldEqual
                { livePreviewFuncCount = 1
                  invalidLivePreviewFuncCount = 0
                  invalidStringCallCount = 0
                  notSuppurtPatternCount = 0 }
        }


    [<Fact>]
    let ``All value case DU generic singleCase`` () =
        task {
            let! results =
                AllValueCaseDU.genericSingleCase
                |> snipNoLivePreviewFunc
                |> runFuncUIAnalysisAsync

            FuncUIAnalysisResult.count results
            |> shouldEqual
                { livePreviewFuncCount = 0
                  invalidLivePreviewFuncCount = 0
                  invalidStringCallCount = 0
                  notSuppurtPatternCount = 0 }
        }

    [<Fact>]
    let ``All value case DU generic singleCase with livePreviewFunc`` () =
        task {
            let! results =
                AllValueCaseDU.genericSingleCase
                |> snipWithLivePreviewFunc
                |> runFuncUIAnalysisAsync

            FuncUIAnalysisResult.count results
            |> shouldEqual
                { livePreviewFuncCount = 1
                  invalidLivePreviewFuncCount = 0
                  invalidStringCallCount = 0
                  notSuppurtPatternCount = 0 }
        }

    [<Fact>]
    let ``All value case DU generic multiCase`` () =
        task {
            let! results =
                AllValueCaseDU.genericMultiCase
                |> snipNoLivePreviewFunc
                |> runFuncUIAnalysisAsync

            FuncUIAnalysisResult.count results
            |> shouldEqual
                { livePreviewFuncCount = 0
                  invalidLivePreviewFuncCount = 0
                  invalidStringCallCount = 0
                  notSuppurtPatternCount = 0 }
        }

    [<Fact>]
    let ``All value case DU generic multiCase with livePreviewFunc`` () =
        task {
            let! results =
                AllValueCaseDU.genericMultiCase
                |> snipWithLivePreviewFunc
                |> runFuncUIAnalysisAsync

            FuncUIAnalysisResult.count results
            |> shouldEqual
                { livePreviewFuncCount = 1
                  invalidLivePreviewFuncCount = 0
                  invalidStringCallCount = 0
                  notSuppurtPatternCount = 0 }
        }

    [<Fact>]
    let ``Any no value case DU singleCase`` () =
        task {
            let! results = AnyNoValueCaseDU.singleCase |> snipNoLivePreviewFunc |> runFuncUIAnalysisAsync

            FuncUIAnalysisResult.count results
            |> shouldEqual
                { livePreviewFuncCount = 0
                  invalidLivePreviewFuncCount = 0
                  invalidStringCallCount = 0
                  notSuppurtPatternCount = 0 }
        }

    [<Fact>]
    let ``Any no value case DU singleCase with livePreviewFunc`` () =
        task {
            let! results = AnyNoValueCaseDU.singleCase |> snipWithLivePreviewFunc |> runFuncUIAnalysisAsync

            FuncUIAnalysisResult.count results
            |> shouldEqual
                { livePreviewFuncCount = 1
                  invalidLivePreviewFuncCount = 0
                  invalidStringCallCount = 0
                  notSuppurtPatternCount = 0 }
        }

    [<Fact>]
    let ``Any no value case DU multiCase`` () =
        task {
            let! results = AnyNoValueCaseDU.multiCase |> snipNoLivePreviewFunc |> runFuncUIAnalysisAsync

            FuncUIAnalysisResult.count results
            |> shouldEqual
                { livePreviewFuncCount = 0
                  invalidLivePreviewFuncCount = 0
                  invalidStringCallCount = 0
                  notSuppurtPatternCount = 0 }
        }

    [<Fact>]
    let ``Any no value case DU multiCase with livePreviewFunc`` () =
        task {
            let! results = AnyNoValueCaseDU.multiCase |> snipWithLivePreviewFunc |> runFuncUIAnalysisAsync

            FuncUIAnalysisResult.count results
            |> shouldEqual
                { livePreviewFuncCount = 1
                  invalidLivePreviewFuncCount = 0
                  invalidStringCallCount = 0
                  notSuppurtPatternCount = 0 }
        }

    [<Fact>]
    let ``Any no value case DU genericCase`` () =
        task {
            let! results = AnyNoValueCaseDU.genericCase |> snipNoLivePreviewFunc |> runFuncUIAnalysisAsync

            FuncUIAnalysisResult.count results
            |> shouldEqual
                { livePreviewFuncCount = 0
                  invalidLivePreviewFuncCount = 0
                  invalidStringCallCount = 0
                  notSuppurtPatternCount = 0 }
        }

    [<Fact>]
    let ``Any no value case DU genericCase with livePreviewFunc`` () =
        task {
            let! results =
                AnyNoValueCaseDU.genericCase
                |> snipWithLivePreviewFunc
                |> runFuncUIAnalysisAsync

            FuncUIAnalysisResult.count results
            |> shouldEqual
                { livePreviewFuncCount = 1
                  invalidLivePreviewFuncCount = 0
                  invalidStringCallCount = 0
                  notSuppurtPatternCount = 0 }
        }

module DU_is_in_end_of_code =
    let private snipNoLivePreviewFunc du =
        $"""
module Foo

{snipCounter "Auto,Auto"}

{du}
        """
        |> String.trimStart

    let private snipWithLivePreviewFunc du =
        $"""
module Foo

{snipCounter "Auto,Auto"}

    [<LivePreview>]
    let preview () = view Store.num

{du}
        """

    [<Fact>]
    let ``All value case DU singleCase`` () =
        task {
            let! results = AllValueCaseDU.singleCase |> snipNoLivePreviewFunc |> runFuncUIAnalysisAsync

            FuncUIAnalysisResult.count results
            |> shouldEqual
                { livePreviewFuncCount = 0
                  invalidLivePreviewFuncCount = 0
                  invalidStringCallCount = 0
                  notSuppurtPatternCount = 0 }
        }

    [<Fact>]
    let ``All value case DU singleCase with livePreviewFunc`` () =
        task {
            let! results = AllValueCaseDU.singleCase |> snipWithLivePreviewFunc |> runFuncUIAnalysisAsync

            FuncUIAnalysisResult.count results
            |> shouldEqual
                { livePreviewFuncCount = 1
                  invalidLivePreviewFuncCount = 0
                  invalidStringCallCount = 0
                  notSuppurtPatternCount = 0 }
        }

    [<Fact>]
    let ``All value case DU multiCase`` () =
        task {
            let! results = AllValueCaseDU.multiCase |> snipNoLivePreviewFunc |> runFuncUIAnalysisAsync

            FuncUIAnalysisResult.count results
            |> shouldEqual
                { livePreviewFuncCount = 0
                  invalidLivePreviewFuncCount = 0
                  invalidStringCallCount = 0
                  notSuppurtPatternCount = 0 }
        }

    [<Fact>]
    let ``All value case DU multiCase with livePreviewFunc`` () =
        task {
            let! results = AllValueCaseDU.multiCase |> snipWithLivePreviewFunc |> runFuncUIAnalysisAsync

            FuncUIAnalysisResult.count results
            |> shouldEqual
                { livePreviewFuncCount = 1
                  invalidLivePreviewFuncCount = 0
                  invalidStringCallCount = 0
                  notSuppurtPatternCount = 0 }
        }


    [<Fact>]
    let ``All value case DU generic singleCase`` () =
        task {
            let! results =
                AllValueCaseDU.genericSingleCase
                |> snipNoLivePreviewFunc
                |> runFuncUIAnalysisAsync

            FuncUIAnalysisResult.count results
            |> shouldEqual
                { livePreviewFuncCount = 0
                  invalidLivePreviewFuncCount = 0
                  invalidStringCallCount = 0
                  notSuppurtPatternCount = 0 }
        }

    [<Fact>]
    let ``All value case DU generic singleCase with livePreviewFunc`` () =
        task {
            let! results =
                AllValueCaseDU.genericSingleCase
                |> snipWithLivePreviewFunc
                |> runFuncUIAnalysisAsync

            FuncUIAnalysisResult.count results
            |> shouldEqual
                { livePreviewFuncCount = 1
                  invalidLivePreviewFuncCount = 0
                  invalidStringCallCount = 0
                  notSuppurtPatternCount = 0 }
        }

    [<Fact>]
    let ``All value case DU generic multiCase`` () =
        task {
            let! results =
                AllValueCaseDU.genericMultiCase
                |> snipNoLivePreviewFunc
                |> runFuncUIAnalysisAsync

            FuncUIAnalysisResult.count results
            |> shouldEqual
                { livePreviewFuncCount = 0
                  invalidLivePreviewFuncCount = 0
                  invalidStringCallCount = 0
                  notSuppurtPatternCount = 0 }
        }

    [<Fact>]
    let ``All value case DU generic multiCase with livePreviewFunc`` () =
        task {
            let! results =
                AllValueCaseDU.genericMultiCase
                |> snipWithLivePreviewFunc
                |> runFuncUIAnalysisAsync

            FuncUIAnalysisResult.count results
            |> shouldEqual
                { livePreviewFuncCount = 1
                  invalidLivePreviewFuncCount = 0
                  invalidStringCallCount = 0
                  notSuppurtPatternCount = 0 }
        }


    [<Fact>]
    let ``Any no value case DU singleCase`` () =
        task {
            let! results = AnyNoValueCaseDU.singleCase |> snipNoLivePreviewFunc |> runFuncUIAnalysisAsync

            FuncUIAnalysisResult.count results
            |> shouldEqual
                { livePreviewFuncCount = 0
                  invalidLivePreviewFuncCount = 0
                  invalidStringCallCount = 0
                  notSuppurtPatternCount = 0 }
        }

    [<Fact>]
    let ``Any no value case DU singleCase with livePreviewFunc`` () =
        task {
            let! results = AnyNoValueCaseDU.singleCase |> snipWithLivePreviewFunc |> runFuncUIAnalysisAsync

            FuncUIAnalysisResult.count results
            |> shouldEqual
                { livePreviewFuncCount = 1
                  invalidLivePreviewFuncCount = 0
                  invalidStringCallCount = 0
                  notSuppurtPatternCount = 0 }
        }

    [<Fact>]
    let ``Any no value case DU multiCase`` () =
        task {
            let! results = AnyNoValueCaseDU.multiCase |> snipNoLivePreviewFunc |> runFuncUIAnalysisAsync

            FuncUIAnalysisResult.count results
            |> shouldEqual
                { livePreviewFuncCount = 0
                  invalidLivePreviewFuncCount = 0
                  invalidStringCallCount = 0
                  notSuppurtPatternCount = 0 }
        }

    [<Fact>]
    let ``Any no value case DU multiCase with livePreviewFunc`` () =
        task {
            let! results = AnyNoValueCaseDU.multiCase |> snipWithLivePreviewFunc |> runFuncUIAnalysisAsync

            FuncUIAnalysisResult.count results
            |> shouldEqual
                { livePreviewFuncCount = 1
                  invalidLivePreviewFuncCount = 0
                  invalidStringCallCount = 0
                  notSuppurtPatternCount = 0 }
        }

    [<Fact>]
    let ``Any no value case DU genericCase`` () =
        task {
            let! results = AnyNoValueCaseDU.genericCase |> snipNoLivePreviewFunc |> runFuncUIAnalysisAsync

            FuncUIAnalysisResult.count results
            |> shouldEqual
                { livePreviewFuncCount = 0
                  invalidLivePreviewFuncCount = 0
                  invalidStringCallCount = 0
                  notSuppurtPatternCount = 0 }
        }

    [<Fact>]
    let ``Any no value case DU genericCase with livePreviewFunc`` () =
        task {
            let! results =
                AnyNoValueCaseDU.genericCase
                |> snipWithLivePreviewFunc
                |> runFuncUIAnalysisAsync

            FuncUIAnalysisResult.count results
            |> shouldEqual
                { livePreviewFuncCount = 1
                  invalidLivePreviewFuncCount = 0
                  invalidStringCallCount = 0
                  notSuppurtPatternCount = 0 }
        }