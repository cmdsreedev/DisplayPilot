[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version,
    [string]$InnoCompiler = $env:ISCC_PATH
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if (-not $InnoCompiler) {
    $found = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($found) { $InnoCompiler = $found.Source }
    else { $InnoCompiler = Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe' }
}
if (-not (Test-Path -LiteralPath $InnoCompiler -PathType Leaf)) { throw 'Install Inno Setup, or supply -InnoCompiler / ISCC_PATH.' }
$destination = Join-Path $root "artifacts\$Version"
# A fresh staging directory prevents stale files or personal settings entering a release.
$staging = Join-Path $root ('artifacts\build-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $destination,$staging | Out-Null
Push-Location $root
try {
    dotnet run --project tests/DisplayPilot.Tests.csproj -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Regression checks failed.' }
    dotnet publish DeviceDetector.csproj -c Release -r win-x64 --self-contained true -p:Version=$Version -o $staging --source https://api.nuget.org/v3/index.json
    if ($LASTEXITCODE -ne 0) { throw 'Release publish failed.' }
    $unexpected = Get-ChildItem $staging -Recurse -File | Where-Object { $_.Name -match '^(settings\.json|\.env)' -or $_.Extension -in '.pfx','.key' }
    if ($unexpected) { throw 'Personal settings or key material found in publish output.' }
    Copy-Item README.md $staging
    Copy-Item CHANGELOG.md $staging
    Copy-Item docs (Join-Path $staging 'docs') -Recurse
    & $InnoCompiler '/Q' "/DMyAppVersion=$Version" "/DPublishDir=$staging" "/O$destination" DisplayPilot.iss
    if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
    $archive = Join-Path $destination 'DisplayPilot-win-x64.zip'
    Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $archive -Force
    @('DisplayPilot-Setup.exe','DisplayPilot-win-x64.zip') | ForEach-Object {
        $hash = Get-FileHash (Join-Path $destination $_) -Algorithm SHA256
        '{0}  {1}' -f $hash.Hash.ToLowerInvariant(), $_
    } | Set-Content (Join-Path $destination 'SHA256SUMS.txt') -Encoding utf8
    Write-Host "Release artifacts: $destination"
} finally { Pop-Location }
