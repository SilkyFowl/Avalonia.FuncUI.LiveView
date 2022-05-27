$packPath = 'dist'
$analyzersPath = 'analyzers'

Remove-Item $packPath/* -Recurse
Remove-Item $analyzersPath/* -Recurse

$proj = 'Avalonia.FuncUI.LiveView.Analyzer'
dotnet pack src/$proj --configuration Release --output $packPath
Expand-Archive "$packPath/$proj*.nupkg" -DestinationPath $packPath/$proj
dotnet publish src/$proj --configuration Release --framework net6.0

$publishPath = "src/$proj/bin/Release/net6.0/publish/*"
$distPath= "dist/$proj/lib/net6.0"
Copy-Item $publishPath $distPath -Recurse

Move-Item $packPath/$proj $analyzersPath