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
$temporaryConfig = $null
$restoreProperties = @()
if ($Source.Count -gt 0) {
    # A temporary NuGet.Config avoids a Windows NuGet command-line edge case where a repeated
    # --source URL can be normalized as a relative path. It also preserves paths containing spaces.
    $temporaryConfig = Join-Path ([IO.Path]::GetTempPath()) ('fluentkit-resolver-' + [Guid]::NewGuid().ToString('N') + '.NuGet.Config')
    $configuration = New-Object System.Xml.XmlDocument
    $configuration.LoadXml('<configuration><packageSources><clear /></packageSources></configuration>')
    $packageSources = $configuration.SelectSingleNode('/configuration/packageSources')
    for ($index = 0; $index -lt $Source.Count; $index++) {
        $sourceElement = $configuration.CreateElement('add')
        $sourceElement.SetAttribute('key', "fluentkit-source-$index")
        $sourceElement.SetAttribute('value', $Source[$index])
        [void]$packageSources.AppendChild($sourceElement)
    }
    $configuration.Save($temporaryConfig)
    $restoreProperties += @('--configfile', $temporaryConfig)
}
try {
    if (Test-Path -LiteralPath (Join-Path $projectDirectory 'packages.lock.json') -PathType Leaf) {
        & dotnet restore $projectPath --locked-mode @restoreProperties @msbuildProperties
    } else {
        & dotnet restore $projectPath @restoreProperties @msbuildProperties
    }
    $restoreExitCode = $LASTEXITCODE
} finally {
    if ($temporaryConfig -and (Test-Path -LiteralPath $temporaryConfig)) {
        Remove-Item -LiteralPath $temporaryConfig -Force
    }
}
if ($restoreExitCode -ne 0) { exit $restoreExitCode }

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
