#
# Copyright (c) .NET Foundation and contributors. All rights reserved.
# Licensed under the MIT license. See LICENSE file in the project root for full license information.
#

param(
    [string[]]$EnvVars=@(),
    [switch]$Help)

if($Help)
{
    Write-Host "Usage: .\update-dependencies.ps1 [Options]"
    Write-Host ""
    Write-Host "Summary: Installs the .NET CLI and then compiles and invokes update-dependencies.exe."
    Write-Host ""
    Write-Host "Options:"
    Write-Host "  -EnvVars <'V1=val1','V2=val2'...>  Comma separated list of environment variable name-value pairs"
    Write-Host "  -Help                              Display this help message"
    exit 0
}

$Architecture='x64'

$RepoRoot = "$PSScriptRoot\.."
$AppPath = "$PSScriptRoot"

# Use a repo-local install directory (but not the artifacts directory because that gets cleaned a lot)
if (!$env:DOTNET_INSTALL_DIR)
{
    $env:DOTNET_INSTALL_DIR="$RepoRoot\.dotnet_stage0\Windows\$Architecture"
}

if (!(Test-Path $env:DOTNET_INSTALL_DIR))
{
    mkdir $env:DOTNET_INSTALL_DIR | Out-Null
}

if (!(Test-Path "$RepoRoot\artifacts"))
{
    mkdir "$RepoRoot\artifacts" | Out-Null
}

# Install a stage 0
$DOTNET_INSTALL_SCRIPT_URL="https://raw.githubusercontent.com/dotnet/cli/rel/1.0.0/scripts/obtain/dotnet-install.ps1"
Invoke-WebRequest $DOTNET_INSTALL_SCRIPT_URL -OutFile "$RepoRoot\artifacts\dotnet-install.ps1"

& "$RepoRoot\artifacts\dotnet-install.ps1" -Architecture $Architecture
if($LASTEXITCODE -ne 0) { throw "Failed to install stage0" }

# Put the stage0 on the path
$env:PATH = "$env:DOTNET_INSTALL_DIR;$env:PATH"

 # Restore the app 
Write-Host "Restoring app $AppPath..."
pushd "$AppPath"
dotnet restore
if($LASTEXITCODE -ne 0) { throw "Failed to restore" }
popd

# Publish the app
Write-Host "Compiling app..."
dotnet publish "$AppPath" -o "$AppPath\bin" --framework netcoreapp1.0
if($LASTEXITCODE -ne 0) { throw "Failed to compile" }

# Run the app
Write-Host "Invoking app: $AppPath\bin\update-dependencies.exe @EnvVars"
Write-Host " Configuration: $env:CONFIGURATION"
pushd $RepoRoot
& "$AppPath\bin\update-dependencies.exe" @EnvVars
if($LASTEXITCODE -ne 0) { throw "Build failed" }
popd
