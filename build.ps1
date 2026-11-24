# Builds Trainer Deck into .\publish. Both are the same single-file program:
#   TrainerDeck-Setup.exe         named "Setup", it installs itself into Program Files
#   Trainer Deck\TrainerDeck.exe  the portable copy: runs from its folder and keeps its data there
#   TrainerDeck-Portable.zip      that folder, zipped for sharing
#
# With Visual Studio's UWP tools installed (in Build Tools for Visual Studio 2022: the WinUI workload with its
# optional Universal Windows Platform tools), it also builds the
# Xbox Game Bar widget, signs it with a self-signed "CN=Trainer Deck" certificate (created once in .signing\,
# which stays out of git) and embeds both in the installer. -NoWidget skips that.
param([string]$Out = "publish", [switch]$NoWidget)

$ErrorActionPreference = "Stop"
if (-not [IO.Path]::IsPathRooted($Out)) { $Out = Join-Path $PSScriptRoot $Out }
$Out = [IO.Path]::GetFullPath($Out)
$publishArgs = @()

function Find-MSBuild {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
    if (-not (Test-Path $vswhere)) { return $null }
    # Any Visual Studio or Build Tools install that has the UWP XAML build targets. Newer installers put them under
    # the WinUI workload's optional Universal Windows Platform tools, so look for the files rather than a workload name.
    $installs = & $vswhere -all -prerelease -products * -sort -property installationPath
    foreach ($install in $installs) {
        $uwp = Get-ChildItem (Join-Path $install "MSBuild\Microsoft\WindowsXaml") -Recurse -Filter "Microsoft.Windows.UI.Xaml.CSharp.targets" -ErrorAction SilentlyContinue
        $path = Get-ChildItem (Join-Path $install "MSBuild") -Recurse -Filter MSBuild.exe -ErrorAction SilentlyContinue |
            Where-Object { $_.Directory.Name -eq "Bin" } | Select-Object -First 1
        if ($uwp -and $path) { return $path.FullName }
    }
    return $null
}

function Get-SigningCertificate {
    $dir = Join-Path $PSScriptRoot ".signing"
    $pfx = Join-Path $dir "TrainerDeck-Widget.pfx"
    $cer = Join-Path $dir "TrainerDeck-Widget.cer"
    $pwFile = Join-Path $dir "password.txt"
    if (-not (Test-Path $pfx)) {
        Write-Host "Creating the widget signing certificate (CN=Trainer Deck) in .signing\"
        New-Item -ItemType Directory -Force $dir | Out-Null
        $password = [Convert]::ToBase64String((1..24 | ForEach-Object { Get-Random -Maximum 256 }) -as [byte[]])
        $cert = New-SelfSignedCertificate -Type Custom -Subject "CN=Trainer Deck" -FriendlyName "Trainer Deck widget signing" `
            -KeyUsage DigitalSignature -KeyAlgorithm RSA -KeyLength 2048 -NotAfter (Get-Date).AddYears(10) `
            -CertStoreLocation "Cert:\CurrentUser\My" -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")
        Export-PfxCertificate -Cert $cert -FilePath $pfx -Password (ConvertTo-SecureString $password -AsPlainText -Force) | Out-Null
        Export-Certificate -Cert $cert -FilePath $cer | Out-Null
        Set-Content $pwFile $password -NoNewline
        Remove-Item "Cert:\CurrentUser\My\$($cert.Thumbprint)"
    }
    return @{ Pfx = $pfx; Cer = $cer; Password = (Get-Content $pwFile -Raw) }
}

if (-not $NoWidget) {
    $msbuild = Find-MSBuild
    if (-not $msbuild) {
        Write-Warning "Visual Studio with the Universal Windows Platform workload wasn't found, so the installer won't include the Game Bar widget."
    }
    else {
        $cert = Get-SigningCertificate
        $widgetOut = Join-Path $Out "widget-build"
        $widgetPackage = Join-Path $Out "widget"
        Remove-Item $widgetOut, $widgetPackage -Recurse -Force -ErrorAction SilentlyContinue

        Write-Host "Building the Game Bar widget"
        & $msbuild "$PSScriptRoot\src\TrainerDeck.Widget\TrainerDeck.Widget.csproj" /restore /nologo /v:minimal `
            /p:Configuration=Release /p:Platform=x64 /p:AppxBundle=Never /p:UapAppxPackageBuildMode=SideloadOnly `
            /p:GenerateAppxPackageOnBuild=true "/p:AppxPackageDir=$widgetOut\" `
            "/p:PackageCertificateKeyFile=$($cert.Pfx)" "/p:PackageCertificatePassword=$($cert.Password)"
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

        $msix = Get-ChildItem $widgetOut -Recurse -Filter "TrainerDeck.Widget_*.msix" | Select-Object -First 1
        if (-not $msix) { throw "The widget build didn't produce an .msix package." }
        New-Item -ItemType Directory -Force $widgetPackage | Out-Null
        Copy-Item $msix.FullName (Join-Path $widgetPackage "TrainerDeck.GameBarWidget.msix")
        Copy-Item $cert.Cer (Join-Path $widgetPackage "TrainerDeck.GameBarWidget.cer")

        # WinUI 2 and the .NET Native runtime ship as framework packages next to the widget.
        $deps = Get-ChildItem $widgetOut -Recurse -Directory -Filter "x64" | Where-Object { $_.Parent.Name -eq "Dependencies" } | Select-Object -First 1
        if ($deps) {
            Get-ChildItem $deps.FullName -Include *.appx, *.msix -Recurse | ForEach-Object {
                Copy-Item $_.FullName (Join-Path $widgetPackage ("Dependency." + $_.Name))
            }
        }

        $publishArgs += "-p:WidgetPackageDir=$widgetPackage"
    }
}

# Rider's background build or a virus scan can briefly hold the compiled DLL open; one retry gets past that.
for ($attempt = 1; $attempt -le 2; $attempt++) {
    dotnet publish "$PSScriptRoot\src\TrainerDeck" -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=true -p:PublishReadyToRun=true -p:DebugType=None -o $Out @publishArgs
    if ($LASTEXITCODE -eq 0) { break }
    if ($attempt -eq 2) { exit $LASTEXITCODE }
    Write-Warning "The build failed; trying once more in case a file was briefly in use."
    Start-Sleep -Seconds 5
}

$portableDir = Join-Path $Out "Trainer Deck"
$portableZip = Join-Path $Out "TrainerDeck-Portable.zip"
Remove-Item $portableDir, $portableZip, (Join-Path $Out "TrainerDeck-Portable.exe") -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $portableDir | Out-Null
Move-Item (Join-Path $Out "TrainerDeck.exe") (Join-Path $portableDir "TrainerDeck.exe") -Force
Copy-Item (Join-Path $portableDir "TrainerDeck.exe") (Join-Path $Out "TrainerDeck-Setup.exe") -Force
# Entries are written by hand so the zip uses standard "/" paths (Windows PowerShell's zip helpers write "\").
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::Open($portableZip, [IO.Compression.ZipArchiveMode]::Create)
try {
    Get-ChildItem $portableDir -File -Recurse | ForEach-Object {
        $entry = "Trainer Deck/" + $_.FullName.Substring($portableDir.Length + 1).Replace("\", "/")
        [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $_.FullName, $entry, [IO.Compression.CompressionLevel]::Optimal)
    }
}
finally {
    $zip.Dispose()
}

Get-ChildItem $Out -File -Include *.exe, *.zip -Recurse -Depth 1 |
    Select-Object @{ n = "File"; e = { $_.FullName.Substring($Out.Length + 1) } }, @{ n = "MB"; e = { [math]::Round($_.Length / 1MB, 1) } }
