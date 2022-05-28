[fantomas]: https://github.com/fsprojects/fantomas
[f# formatting]: https://marketplace.visualstudio.com/items?itemName=asti.fantomas-vs
[Howto-Video]: https://user-images.githubusercontent.com/16532218/170818646-29ded885-bc2a-4336-909a-b17fc7242345.mp4

# Avalonia.FuncUI.LiveView

Live fs/fsx previewer for Avalonia.FuncUI.

[日本語(Javanese language)](README.ja.md)

## What is this ?

Avalonia.FuncUI.LiveView is an experimental FsAutoComplete extension that aims to provide a real-time preview of the UI of Avalonia.FuncUI.
Analyzers.SDK, it displays a real-time preview of the content of the F# file you are editing in the editor.
**No need to save the file to update the preview.**

## How to use

> **Warning**
> Avalonia.FuncUI.LiveView is incomplete. It has not been fully tested and is not intended for use in a production environment. Please use this tool at your own risk.

```sh
# Clone Repository
gh repo clone SilkyFowl/Avalonia.FuncUI.LiveView
cd Avalonia.FuncUI.LiveView

# Setup
dotnet tool restore
dotnet paket restore
dotnet fake run
dotnet fake run ./build.fsx -t SetLocalAnalyzer

# Open Editor
code .
```

[After opening the editor][Howto-Video]

- [ ] Write the necessary information.
