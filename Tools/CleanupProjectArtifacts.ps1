#requires -Version 7.0
[CmdletBinding(SupportsShouldProcess)]
param([ValidateRange(1, 3650)][int]$RetentionDays = 14)

$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot)).TrimEnd('\')
$cleanupRoots = @('Logs', 'tmp', 'Builds') | ForEach-Object { Join-Path $projectRoot $_ }
$now = [DateTime]::UtcNow
$cutoff = $now.AddDays(-$RetentionDays)
$cleanupCmdlet = $PSCmdlet
$removed = @{ Files = 0; Bytes = [long]0 }
$skipped = [Collections.Generic.List[object]]::new()

foreach ($root in @($projectRoot) + @($cleanupRoots)) {
    if (-not (Test-Path -LiteralPath $root)) { continue }
    if ((Resolve-Path -LiteralPath $root).ProviderPath.TrimEnd('\') -ne $root -or
        ([IO.File]::GetAttributes($root) -band [IO.FileAttributes]::ReparsePoint)) {
        throw "Cleanup root must be a real directory: $root"
    }
}

function Remove-Artifact($entry) {
    $path = [IO.Path]::GetFullPath($entry.FullName)
    $scope = @($cleanupRoots | Where-Object { $path.StartsWith($_ + '\', [StringComparison]::OrdinalIgnoreCase) })
    if ($scope.Count -ne 1) { throw "Refusing deletion outside Logs, tmp, or Builds: $path" }
    for ($ancestor = $path; $ancestor -ne $projectRoot; $ancestor = [IO.Path]::GetDirectoryName($ancestor)) {
        if ([IO.File]::GetAttributes($ancestor) -band [IO.FileAttributes]::ReparsePoint) {
            throw "Refusing deletion through a link: $ancestor"
        }
    }
    if ($cleanupCmdlet.ShouldProcess($path, 'Delete generated artifact')) {
        try {
            if ($entry.PSIsContainer) { [IO.Directory]::Delete($path, $false) }
            else {
                [IO.File]::Delete($path)
                $removed.Files++
                $removed.Bytes += $entry.Length
            }
        } catch { $skipped.Add([pscustomobject]@{ Path = $path; Reason = $_.Exception.Message }) }
    }
}

foreach ($root in $cleanupRoots[0..1]) {
    if (-not (Test-Path -LiteralPath $root)) { continue }
    $entries = @(Get-ChildItem -LiteralPath $root -Recurse -Force)
    $oldEntries = @($entries | Where-Object {
        -not ($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -and
        $_.LastWriteTimeUtc -lt $cutoff -and $_.CreationTimeUtc -lt $cutoff
    })
    foreach ($file in $oldEntries | Where-Object { -not $_.PSIsContainer }) { Remove-Artifact $file }
    foreach ($directory in $oldEntries | Where-Object PSIsContainer | Sort-Object { $_.FullName.Length } -Descending) {
        if (@(Get-ChildItem -LiteralPath $directory.FullName -Force).Count -eq 0) { Remove-Artifact $directory }
    }
}

$buildsRoot = $cleanupRoots[2]
$latestBuild = $null
$buildsDeferred = $null
if (Test-Path -LiteralPath $buildsRoot) {
    $buildEntries = @(Get-ChildItem -LiteralPath $buildsRoot -Force)
    $allBuildEntries = @(Get-ChildItem -LiteralPath $buildsRoot -Recurse -Force)
    if (@($allBuildEntries | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }).Count) {
        $buildsDeferred = 'Builds contains a link; no build artifacts were deleted.'
    } elseif (@($allBuildEntries | Where-Object {
        $_.LastWriteTimeUtc -gt $now.AddHours(-1) -or $_.CreationTimeUtc -gt $now.AddHours(-1)
    }).Count) {
        $buildsDeferred = 'Build artifacts changed within the last hour; retry next run.'
    } else {
        $candidates = @(foreach ($directory in $buildEntries | Where-Object PSIsContainer) {
            $player = Get-Item -LiteralPath (Join-Path $directory.FullName 'UnityPlayer.dll') -ErrorAction SilentlyContinue
            if (-not $player -or $player.PSIsContainer -or $player.Length -eq 0) { continue }
            foreach ($exe in Get-ChildItem -LiteralPath $directory.FullName -File -Filter '*.exe' | Where-Object { $_.Name -notlike 'UnityCrashHandler*' -and $_.Length -gt 0 }) {
                $dataRoot = Join-Path $directory.FullName ($exe.BaseName + '_Data')
                $markers = @('data.unity3d', 'globalgamemanagers') | ForEach-Object { Join-Path $dataRoot $_ }
                $marker = Get-Item -LiteralPath $markers -ErrorAction SilentlyContinue | Where-Object { -not $_.PSIsContainer -and $_.Length -gt 0 } |
                    Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
                if ($marker) { [pscustomobject]@{ Directory = $directory; BuiltAt = $marker.LastWriteTimeUtc; Name = $directory.Name } }
            }
        })
        $latestBuild = $candidates | Sort-Object BuiltAt, Name -Descending | Select-Object -First 1
        if (-not $latestBuild -and $buildEntries.Count) {
            $buildsDeferred = 'No complete Windows Unity build found; no build artifacts were deleted.'
        }
        if ($latestBuild) {
            foreach ($entry in $buildEntries | Where-Object { $_.FullName -ne $latestBuild.Directory.FullName }) {
                $entries = if ($entry.PSIsContainer) { @(Get-ChildItem -LiteralPath $entry.FullName -Recurse -Force) + @($entry) } else { @($entry) }
                $files = @($entries | Where-Object { -not $_.PSIsContainer })
                try {
                    foreach ($file in $files) {
                        if ($file.IsReadOnly) { throw "Read-only build file: $($file.FullName)" }
                        $stream = [IO.File]::Open($file.FullName, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::None)
                        $stream.Dispose()
                    }
                } catch {
                    $skipped.Add([pscustomobject]@{ Path = $entry.FullName; Reason = $_.Exception.Message })
                    continue
                }
                foreach ($file in $files) { Remove-Artifact $file }
                foreach ($directory in $entries | Where-Object PSIsContainer | Sort-Object { $_.FullName.Length } -Descending) {
                    if (@(Get-ChildItem -LiteralPath $directory.FullName -Force).Count -eq 0) { Remove-Artifact $directory }
                }
            }
        }
    }
}

[pscustomobject]@{
    RetentionDays = $RetentionDays
    RemovedFiles = $removed.Files
    RemovedBytes = $removed.Bytes
    LatestBuild = if ($latestBuild) { $latestBuild.Directory.FullName } else { $null }
    BuildsDeferred = $buildsDeferred
    Skipped = @($skipped)
    WhatIf = [bool]$WhatIfPreference
} | ConvertTo-Json -Depth 4 -Compress
