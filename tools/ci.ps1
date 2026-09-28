<#
.SYNOPSIS
    The local CI gate for HyperQ OSS: builds the solution and runs every test. Run it before every push.

.DESCRIPTION
    There is no hosted CI. This script is the gate instead: it builds HyperQ.OSS.sln (the libraries, the
    samples and the WinForms visualizers) and runs every test project in it. Every stage runs, a summary
    follows, and the exit code is non-zero when any stage failed. Test results are written as TRX files
    under TestResults/ci.

    Off Windows the visualizers still compile (EnableWindowsTargeting); they only run on Windows.

    To run it before every push:  git config core.hooksPath tools/hooks

.PARAMETER Configuration
    Build configuration, Release by default.

.PARAMETER Filter
    Passed to dotnet test --filter, for example "FullyQualifiedName~Checkpoint".

.EXAMPLE
    ./tools/ci.ps1

.EXAMPLE
    ./tools/ci.ps1 -Configuration Debug -Filter "FullyQualifiedName~LayeredHyperQ"
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [string]$Filter
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $root 'HyperQ.OSS.sln'
$results = Join-Path $root 'TestResults/ci'
$onWindows = [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::Windows)
$buildProperties = @()
if (-not $onWindows) {
    $buildProperties += '-p:EnableWindowsTargeting=true'
}

$stages = New-Object System.Collections.Generic.List[object]

# Runs one dotnet command as a named stage and records whether it passed and how long it took.
function Invoke-Stage([string]$Name, [string[]]$Arguments) {
    Write-Host ''
    Write-Host "==> $Name" -ForegroundColor Cyan
    Write-Host "    dotnet $($Arguments -join ' ')"
    $watch = [System.Diagnostics.Stopwatch]::StartNew()
    # Out-Host keeps dotnet's output out of this function's return value.
    & dotnet @Arguments | Out-Host
    $passed = ($LASTEXITCODE -eq 0)
    $stages.Add([pscustomobject]@{
        Stage   = $Name
        Result  = $(if ($passed) { 'passed' } else { 'FAILED' })
        Seconds = [math]::Round($watch.Elapsed.TotalSeconds)
    })
    return $passed
}

if (Test-Path $results) {
    Remove-Item -Recurse -Force $results
}

$built = Invoke-Stage 'OSS build' (@('build', $solution, '-c', $Configuration, '--nologo', '-v', 'minimal') + $buildProperties)
if ($built) {
    $testArguments = @('test', $solution, '-c', $Configuration, '--no-build', '--nologo',
        '--results-directory', $results, '--logger', 'trx', '--logger', 'console;verbosity=minimal')
    if ($Filter) {
        $testArguments += @('--filter', $Filter)
    }
    [void](Invoke-Stage 'OSS tests' $testArguments)
}
else {
    $stages.Add([pscustomobject]@{ Stage = 'OSS tests'; Result = 'skipped (build failed)'; Seconds = 0 })
}

Write-Host ''
Write-Host '==> Summary' -ForegroundColor Cyan
$stages | Format-Table -AutoSize | Out-String | Write-Host
$failed = @($stages | Where-Object { $_.Result -ne 'passed' }).Count
if ($failed -gt 0) {
    Write-Host "CI FAILED: $failed stage(s) did not pass." -ForegroundColor Red
    exit 1
}
Write-Host 'CI passed.' -ForegroundColor Green
exit 0
