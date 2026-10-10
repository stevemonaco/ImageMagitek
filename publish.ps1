#Requires -Version 7.3

### Required startup parameters

Param(
    [Parameter(Mandatory=$true)]
    [ValidateSet("win-x64", "osx-arm64", "osx-x64", "linux-x64")]
    [string]$Rid,

    [switch]$ReadyToRun
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true

### Configuration

$configuration = "Release";
$readyToRunArg = "-p:PublishReadyToRun=$($ReadyToRun.IsPresent)"

$solution = Join-Path "." "ImageMagitek.slnx"
$tileshopProject = Join-Path "." "TileShop.UI" "TileShop.UI.csproj"
$tileshopCliProject = Join-Path "." "TileShop.CLI" "TileShop.CLI.csproj"
$testProjects = @(
    Join-Path "." "ImageMagitek.UnitTests" "ImageMagitek.UnitTests.csproj"
)

$version = dotnet msbuild $tileshopProject -getProperty:Version

$publishPath = Join-Path "." "publish" $Rid;
$tileshopPublishPath = Join-Path $publishPath "TileShop"
$tileshopCliPublishPath = Join-Path $publishPath "TileShopCLI"

$tileshopZipName = "TileShop-$Rid-v$version.zip"
$tileshopCliZipName = "TileShopCLI-$Rid-v$version.zip"

### Clean

Remove-Item -Recurse -Force $publishPath -ErrorAction Ignore

dotnet clean $solution -c $configuration

### Test

dotnet build $solution -c $configuration

foreach ($testProject in $testProjects) {
    dotnet test $testProject --configuration $configuration
}

### Build TileShop.UI

dotnet build $tileshopProject -c $configuration --runtime $Rid --self-contained true $readyToRunArg
dotnet publish $tileshopProject `
    -c $configuration `
    --runtime $Rid `
    --self-contained true `
    --no-build `
    --no-restore `
    -p:PublishSingleFile=true `
    $readyToRunArg `
    -o $tileshopPublishPath

### Build TileShop.CLI

dotnet build $tileshopCliProject -c $configuration --runtime $Rid --self-contained true $readyToRunArg
dotnet publish $tileshopCliProject `
    -c $configuration `
    --runtime $Rid `
    --self-contained true `
    --no-build `
    --no-restore `
    -p:PublishSingleFile=true `
    $readyToRunArg `
    -o $tileshopCliPublishPath

### Archive

$tileshopZipPath = Join-Path $publishPath $tileshopZipName
$tileshopCliZipPath = Join-Path $publishPath $tileshopCliZipName

try {
    Compress-Archive -Path $tileshopPublishPath -DestinationPath $tileshopZipPath
    Compress-Archive -Path $tileshopCliPublishPath -DestinationPath $tileshopCliZipPath
}
catch {
    Remove-Item -Force $tileshopZipPath, $tileshopCliZipPath -ErrorAction Ignore
    throw
}
