[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string] $Project,

    [Parameter()]
    [string[]] $Property = @(),

    [Parameter()]
    [string[]] $Source = @()
)

$projectPath = [IO.Path]::GetFullPath($Project)
if (-not (Test-Path -LiteralPath $projectPath -PathType Leaf)) {
    Write-Error "Consumer project not found: $projectPath"
    exit 2
}

$projectDirectory = Split-Path -Parent $projectPath
$msbuildProperties = @($Property | ForEach-Object { "-p:$_" })
$restoreProperties = @()
if ($Source.Count -gt 0) {
    # RestoreSources is a semicolon-delimited MSBuild property. It avoids the Windows NuGet
    # command-line edge case where a repeated --source URL can be normalized as a relative path.
    $restoreProperties += "-p:RestoreSources=$($Source -join ';')"
}
if (Test-Path -LiteralPath (Join-Path $projectDirectory 'packages.lock.json') -PathType Leaf) {
    & dotnet restore $projectPath --locked-mode @restoreProperties @msbuildProperties
} else {
    & dotnet restore $projectPath @restoreProperties @msbuildProperties
}
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$skillPath = (& dotnet msbuild $projectPath -nologo @msbuildProperties -getProperty:FluentKitAgentSkillPath | Select-Object -Last 1).Trim()
$manifestPath = (& dotnet msbuild $projectPath -nologo @msbuildProperties -getProperty:FluentKitAgentManifestPath | Select-Object -Last 1).Trim()

if ([string]::IsNullOrWhiteSpace($skillPath) -or [string]::IsNullOrWhiteSpace($manifestPath)) {
    Write-Error 'FluentKit.Blazor does not expose the agent contract. Upgrade to the first contract-bearing release.'
    exit 3
}
if (-not (Test-Path -LiteralPath $skillPath -PathType Leaf) -or -not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    Write-Error "FluentKit agent contract is incomplete after restore. Skill: $skillPath; Manifest: $manifestPath"
    exit 4
}

Write-Output "FluentKitAgentSkillPath=$skillPath"
Write-Output "FluentKitAgentManifestPath=$manifestPath"
