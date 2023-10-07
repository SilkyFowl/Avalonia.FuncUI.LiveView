module MSBuildTests

open Xunit
open FsUnit.Xunit
open FsUnitTyped

open Avalonia.FuncUI.LiveView

module MSBuildLocatorTests =
    [<Fact>]
    let ``register should make Microsoft.Build types accessible without throwing an exception`` () =

        // Act
        MSBuildLocator.registerIfNotRegistered ()

        // Assert
        MSBuildLocator.isRegistered() |> should be True

module ProjectInfoTests =
    open FsUnit.CustomMatchers
    open Avalonia.FuncUI.LiveView.Types
    [<Fact>]
    let ``create should return a project with the correct path`` () =

        // Arrange
        MSBuildLocator.registerIfNotRegistered ()
        let path = __SOURCE_DIRECTORY__ + "/../../src/Avalonia.FuncUI.LiveView/Avalonia.FuncUI.LiveView.fsproj"
        // Act
        let project = ProjectInfo.loadFromProjFile path

        // Assert
        project |> should be (ofCase <@ Result<ProjectInfo,string>.Ok @>)


    [<Fact>]
    let ``It should be able to load sln file`` () =
            // Arrange
            MSBuildLocator.registerIfNotRegistered ()
            let path = __SOURCE_DIRECTORY__ + "/../../Avalonia.FuncUI.LiveView.sln"
            // Act
            let project = ProjectInfo.loadFromSlnFile path

            // Assert
            project |> shouldContain

