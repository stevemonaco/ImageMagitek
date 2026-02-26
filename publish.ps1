### Required startup parameters

Param(
    [Parameter(Mandatory=$true)]
    [string]$Version,

    [Parameter(Mandatory=$true)]
    [string]$CliVersion,
    
    [Parameter(Mandatory=$true)]
    [ValidateSet("win-x64", "osx-arm64", "osx-x64", "linux-x64")]
    [string]$Rid,
    
    [string]$ReadyToRun = $false
)

### Configuration

$configuration = "Release";

$solution = Join-Path "." "ImageMagitek.slnx"
$tileshopProject = Join-Path "." "TileShop.UI" "TileShop.UI.csproj"
$tileshopCliProject = Join-Path "." "TileShop.CLI" "TileShop.CLI.csproj"
$testProjects = @(
    Join-Path "." "ImageMagitek.UnitTests" "ImageMagitek.UnitTests.csproj"
)

$publishPath = Join-Path "." "publish" $Rid;
$tileshopPublishPath = Join-Path $publishPath "TileShop"
$tileshopCliPublishPath = Join-Path $publishPath "TileShopCLI"

$tileshopZipName = "TileShop-$Rid.v$Version.zip"
$tileshopCliZipName = "TileShopCLI-$Rid.v$CliVersion.zip"

if ($CliVersion -eq $null) {
    $CliVersion = $Version
}

### Clean

dotnet clean $solution -c $configuration

### Test

dotnet build $solution -c $configuration

foreach ($testProject in $testProjects) {
    dotnet test $testProject --configuration $configuration
}

### Build TileShop.UI

dotnet build $tileshopProject -c $configuration --runtime $Rid --self-contained true
dotnet publish $tileshopProject `
    -c $configuration `
    --runtime $Rid `
    --self-contained true `
    --no-build `
    --no-restore `
    -p:PublishSingleFile=true `
    -o $tileshopPublishPath
    
Compress-Archive -Path $tileshopPublishPath -DestinationPath (Join-Path $publishPath $tileshopZipName)

### Build TileShop.CLI

dotnet build $tileshopCliProject -c $configuration --runtime $Rid --self-contained true
dotnet publish $tileshopCliProject `
    -c $configuration `
    --runtime $Rid `
    --self-contained true `
    --no-build `
    --no-restore `
    -p:PublishSingleFile=true `
    -o $tileshopCliPublishPath

Compress-Archive -Path $tileshopCliPublishPath -DestinationPath (Join-Path $publishPath $tileshopCliZipName)