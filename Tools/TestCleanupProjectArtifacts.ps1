#requires -Version 7.0
$ErrorActionPreference = 'Stop'
$fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ('BecomeZArtifactCleanup-' + [Guid]::NewGuid().ToString('N'))
$now = [DateTime]::UtcNow

function Assert-True([bool]$condition, [string]$message) {
    if (-not $condition) { throw $message }
}

function Write-FixtureFile([string]$relativePath) {
    $path = Join-Path $fixtureRoot $relativePath
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($path)) | Out-Null
    [IO.File]::WriteAllText($path, $relativePath)
    return $path
}

function Set-OldTimes([string]$path) {
    foreach ($entry in @(Get-ChildItem -LiteralPath $path -Recurse -Force) + @(Get-Item -LiteralPath $path)) {
        $entry.CreationTimeUtc = $now.AddDays(-30)
        $entry.LastWriteTimeUtc = $now.AddDays(-30)
    }
}

function New-FixtureBuild([string]$name, [int]$ageDays) {
    Write-FixtureFile "Builds\$name\Game.exe" | Out-Null
    Write-FixtureFile "Builds\$name\UnityPlayer.dll" | Out-Null
    $marker = Write-FixtureFile "Builds\$name\Game_Data\data.unity3d"
    $path = Join-Path $fixtureRoot "Builds\$name"
    Set-OldTimes $path
    [IO.File]::SetLastWriteTimeUtc($marker, $now.AddDays(-$ageDays))
    return $path
}

try {
    [IO.Directory]::CreateDirectory((Join-Path $fixtureRoot 'Tools')) | Out-Null
    $cleanupScript = Join-Path $fixtureRoot 'Tools\CleanupProjectArtifacts.ps1'
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'CleanupProjectArtifacts.ps1') -Destination $cleanupScript
    $expiredLog = Write-FixtureFile 'Logs\expired\capture.png'
    $recentLog = Write-FixtureFile 'Logs\recent.png'
    $copiedLog = Write-FixtureFile 'Logs\recent-copy.png'
    $expiredTmp = Write-FixtureFile 'tmp\expired.bin'
    $recentTmp = Write-FixtureFile 'tmp\recent.bin'
    $protectedFiles = @('Assets\keep.txt', 'ArtSource\keep.txt', '.git\keep.txt') | ForEach-Object { Write-FixtureFile $_ }
    $archive = Write-FixtureFile 'Builds\old-release.zip'
    $oldBuild = New-FixtureBuild 'Z-OldBuildWithNewerFolderName' 3
    $newBuild = New-FixtureBuild 'A-NewestBuild' 1
    Set-OldTimes $fixtureRoot
    [IO.File]::SetLastWriteTimeUtc((Join-Path $oldBuild 'Game_Data\data.unity3d'), $now.AddDays(-3))
    [IO.File]::SetLastWriteTimeUtc((Join-Path $newBuild 'Game_Data\data.unity3d'), $now.AddDays(-1))
    [IO.File]::SetLastWriteTimeUtc($recentLog, $now.AddDays(-2))
    [IO.File]::SetLastWriteTimeUtc($recentTmp, $now.AddDays(-2))
    [IO.File]::SetCreationTimeUtc($copiedLog, $now)
    $protectedHashes = @($protectedFiles | Get-FileHash | ForEach-Object Hash)
    $link = Join-Path $fixtureRoot 'Logs\protected-link'
    New-Item -ItemType Junction -Path $link -Target (Join-Path $fixtureRoot 'Assets') | Out-Null

    & $cleanupScript -WhatIf | Out-Null
    Assert-True (Test-Path -LiteralPath $expiredLog) 'WhatIf deleted an expired log'
    Assert-True (Test-Path -LiteralPath $oldBuild) 'WhatIf deleted a build'

    $result = & $cleanupScript | ConvertFrom-Json
    Assert-True (-not (Test-Path -LiteralPath $expiredLog)) 'Expired log survived'
    Assert-True (-not (Test-Path -LiteralPath $expiredTmp)) 'Expired tmp file survived'
    Assert-True (Test-Path -LiteralPath $recentLog) 'Recent log was deleted'
    Assert-True (Test-Path -LiteralPath $recentTmp) 'Recent tmp file was deleted'
    Assert-True (Test-Path -LiteralPath $copiedLog) 'A newly copied file with an old timestamp was deleted'
    Assert-True (-not (Test-Path -LiteralPath $oldBuild)) 'Old build survived'
    Assert-True (-not (Test-Path -LiteralPath $archive)) 'Old archive survived'
    Assert-True ($result.LatestBuild -eq $newBuild) 'Build selection ignored the build data timestamp'
    Assert-True (Test-Path -LiteralPath $link) 'A junction was deleted'
    Assert-True (@(Compare-Object $protectedHashes @($protectedFiles | Get-FileHash | ForEach-Object Hash)).Count -eq 0) 'Protected files changed'

    $lockedBuild = New-FixtureBuild 'LockedOldBuild' 4
    $stream = [IO.File]::Open((Join-Path $lockedBuild 'Game.exe'), [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
    try {
        $lockedResult = & $cleanupScript | ConvertFrom-Json
        Assert-True ($lockedResult.Skipped.Count -eq 1) 'Locked build was not reported'
        Assert-True (Test-Path -LiteralPath (Join-Path $lockedBuild 'Game_Data\data.unity3d')) 'A locked build was partly deleted'
    } finally { $stream.Dispose() }
    & $cleanupScript | Out-Null
    Assert-True (-not (Test-Path -LiteralPath $lockedBuild)) 'An unlocked old build was not retried'

    $partialBuild = New-FixtureBuild 'PartialBuild' 1
    [IO.File]::WriteAllText((Join-Path $partialBuild 'Game.exe'), '')
    Set-OldTimes $partialBuild
    $partialResult = & $cleanupScript | ConvertFrom-Json
    Assert-True ($partialResult.LatestBuild -eq $newBuild) 'An empty executable was considered a complete build'

    $buildLink = Join-Path $fixtureRoot 'Builds\protected-link'
    New-Item -ItemType Junction -Path $buildLink -Target (Join-Path $fixtureRoot 'Assets') | Out-Null
    $linkedResult = & $cleanupScript | ConvertFrom-Json
    Assert-True ($linkedResult.BuildsDeferred -like '*contains a link*') 'A link inside Builds was ignored'
    Remove-Item -LiteralPath $buildLink -Force

    $newMarker = Join-Path $newBuild 'Game_Data\data.unity3d'
    [IO.File]::Delete($newMarker)
    Set-OldTimes $newBuild
    $unverifiedArchive = Write-FixtureFile 'Builds\unverified.zip'
    Set-OldTimes $unverifiedArchive
    $missingResult = & $cleanupScript | ConvertFrom-Json
    Assert-True ($missingResult.BuildsDeferred -like 'No complete*') 'A build with missing data was considered complete'
    Assert-True (Test-Path -LiteralPath $unverifiedArchive) 'Files were deleted without a complete build to preserve'
    [IO.File]::WriteAllText($newMarker, 'complete build data')
    Set-OldTimes $newBuild
    [IO.File]::SetLastWriteTimeUtc($newMarker, $now.AddDays(-1))

    $incomplete = Write-FixtureFile 'Builds\InProgress\progress.tmp'
    $deferredArchive = Write-FixtureFile 'Builds\keep-until-build-completes.zip'
    $deferredResult = & $cleanupScript | ConvertFrom-Json
    Assert-True ($null -ne $deferredResult.BuildsDeferred) 'Recent build activity was ignored'
    Assert-True (Test-Path -LiteralPath $deferredArchive) 'Build cleanup ran during build activity'

    [IO.File]::Delete($recentTmp)
    [IO.Directory]::Delete((Join-Path $fixtureRoot 'tmp'), $false)
    New-Item -ItemType Junction -Path (Join-Path $fixtureRoot 'tmp') -Target (Join-Path $fixtureRoot 'Assets') | Out-Null
    $guardFile = Write-FixtureFile 'Logs\guard.txt'
    [IO.File]::SetCreationTimeUtc($guardFile, $now.AddDays(-30))
    [IO.File]::SetLastWriteTimeUtc($guardFile, $now.AddDays(-30))
    $rejected = $false
    try { & $cleanupScript | Out-Null } catch { $rejected = $_.Exception.Message -like 'Cleanup root must be a real directory:*' }
    Assert-True $rejected 'A linked cleanup root was accepted'
    Assert-True (Test-Path -LiteralPath $guardFile) 'Cleanup started before all roots were checked'
    Assert-True (@(Compare-Object $protectedHashes @($protectedFiles | Get-FileHash | ForEach-Object Hash)).Count -eq 0) 'Protected files changed after all checks'
    'PASS: age retention, copied files, WhatIf, latest/incomplete builds, archives, locks, build activity, junctions, and protected folders'
} finally {
    if (Test-Path -LiteralPath $fixtureRoot) {
        $resolvedFixture = (Resolve-Path -LiteralPath $fixtureRoot).ProviderPath.TrimEnd('\')
        if ($resolvedFixture -ne $fixtureRoot -or [IO.Path]::GetDirectoryName($resolvedFixture).TrimEnd('\') -ne [IO.Path]::GetTempPath().TrimEnd('\') -or [IO.Path]::GetFileName($resolvedFixture) -notlike 'BecomeZArtifactCleanup-*') { throw 'Unsafe test cleanup path' }
        foreach ($junction in Get-ChildItem -LiteralPath $resolvedFixture -Recurse -Force -Attributes ReparsePoint) {
            Remove-Item -LiteralPath $junction.FullName -Force
        }
        Remove-Item -LiteralPath $resolvedFixture -Recurse -Force
    }
}
