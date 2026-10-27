$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
[xml]$project = Get-Content 'src/NovaLand/NovaLand.csproj'
$version = [string]$project.Project.PropertyGroup.Version
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Version must be X.Y.Z.' }
$publish = Join-Path $PWD 'artifacts/publish'
$stage = Join-Path $PWD 'artifacts/package/NovaLand'
# Generated artifacts only; never installed app or user data.
if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
dotnet publish src/NovaLand/NovaLand.csproj -c Release -r win-x64 -p:SelfContained=false -p:PublishSelfContained=false -p:PublishSingleFile=false -p:DebugType=None -o $publish --nologo
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
dotnet run --project tests/UpdateChecks.csproj -- $publish
if ($LASTEXITCODE -ne 0) { throw 'Update checks failed.' }
dotnet run --project tests/DimmerChecks/DimmerChecks.csproj -c Release
if ($LASTEXITCODE -ne 0) { throw 'Native dimmer checks failed.' }
New-Item $stage -ItemType Directory -Force | Out-Null
Copy-Item "$publish/*" $stage -Recurse
Get-ChildItem src/NovaLand -File | Where-Object { $_.Extension -in '.txt','.md','.cmd' } | Copy-Item -Destination $stage
Copy-Item src/NovaLand/BrowserAudioExtension "$stage/Tarayici_Ses_Eklentisi" -Recurse
Copy-Item src/NovaLand/Assets "$stage/Logo" -Recurse
# Source is already versioned on GitHub; app updates need only runtime files.
$zip = Join-Path $PWD "artifacts/NovaLand_v${version}_win-x64.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path $stage -DestinationPath $zip -CompressionLevel Optimal
Write-Output "Release package: $zip"
