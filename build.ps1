# Builds the single-file app and its installer into .\publish
#   TrainerDeck.exe        portable: runs from any folder, keeps its data next to it
#   TrainerDeck-Setup.exe  the same program; named "Setup" it installs itself into Program Files
param([string]$Out = "publish")

$ErrorActionPreference = "Stop"
dotnet publish "$PSScriptRoot\src\TrainerDeck" -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:PublishReadyToRun=true -p:DebugType=None -o $Out
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Copy-Item (Join-Path $Out "TrainerDeck.exe") (Join-Path $Out "TrainerDeck-Setup.exe") -Force
Get-ChildItem $Out -Filter *.exe | Select-Object Name, @{ n = "MB"; e = { [math]::Round($_.Length / 1MB, 1) } }
