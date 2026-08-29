[CmdletBinding()]
param()

$repository = 'https://github.com/VibeNoobNotFound/FluentKit/releases/latest/download'
$temporaryDirectory = Join-Path ([IO.Path]::GetTempPath()) ('fluentkit-skill-' + [Guid]::NewGuid().ToString('N'))
$archivePath = Join-Path $temporaryDirectory 'fluentkit-api.zip'
$checksumPath = Join-Path $temporaryDirectory 'fluentkit-api.zip.sha256'
$extractDirectory = Join-Path $temporaryDirectory 'extract'

try {
    New-Item -ItemType Directory -Path $temporaryDirectory, $extractDirectory -Force | Out-Null
    Invoke-WebRequest -Uri "$repository/fluentkit-api.zip" -OutFile $archivePath
    Invoke-WebRequest -Uri "$repository/fluentkit-api.zip.sha256" -OutFile $checksumPath

    $expectedHash = (Get-Content -LiteralPath $checksumPath | Where-Object { $_.Trim() } | Select-Object -First 1).Split()[0]
    $actualHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $archivePath).Hash.ToLowerInvariant()
    if ([string]::IsNullOrWhiteSpace($expectedHash) -or $expectedHash.ToLowerInvariant() -ne $actualHash) {
        throw 'FluentKit skill checksum verification failed.'
    }

    Expand-Archive -LiteralPath $archivePath -DestinationPath $extractDirectory -Force
    $sourceDirectory = Join-Path $extractDirectory 'fluentkit-api'
    $requiredFiles = @(
        (Join-Path $sourceDirectory 'SKILL.md'),
        (Join-Path $sourceDirectory 'agents/openai.yaml'),
        (Join-Path $sourceDirectory 'scripts/resolve-fluentkit.sh'),
        (Join-Path $sourceDirectory 'scripts/resolve-fluentkit.ps1')
    )
    if ($requiredFiles | Where-Object { -not (Test-Path -LiteralPath $_ -PathType Leaf) }) {
        throw 'The downloaded FluentKit skill archive is invalid.'
    }

    $codexRoot = if ($env:CODEX_HOME) { $env:CODEX_HOME } else { Join-Path $HOME '.codex' }
    $skillsDirectory = Join-Path $codexRoot 'skills'
    $destination = Join-Path $skillsDirectory 'fluentkit-api'
    New-Item -ItemType Directory -Path $skillsDirectory -Force | Out-Null
    if (Test-Path -LiteralPath $destination) {
        $backup = "$destination.backup.$(Get-Date -Format yyyyMMddHHmmss).$([Guid]::NewGuid().ToString('N'))"
        Move-Item -LiteralPath $destination -Destination $backup
        Write-Output "Backed up the existing skill to $backup"
    }
    Move-Item -LiteralPath $sourceDirectory -Destination $destination
    Write-Output "Installed FluentKit API bootstrap at $destination"
}
finally {
    if (Test-Path -LiteralPath $temporaryDirectory) {
        Remove-Item -LiteralPath $temporaryDirectory -Recurse -Force
    }
}
