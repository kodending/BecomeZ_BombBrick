param(
    [string]$ProjectFile = "Assembly-CSharp.csproj"
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $projectRoot $ProjectFile
$unityRoot = "C:\Program Files\Unity\Hub\Editor\2022.3.62f2\Editor\Data"
$dotnetPath = Join-Path $unityRoot "NetCoreRuntime\dotnet.exe"
$compilerPath = Join-Path $unityRoot "DotNetSdkRoslyn\csc.dll"
$isEditorProject = $ProjectFile -like "*Editor*"
$artifactName = if ($isEditorProject) { "CodexEditorValidation" } else { "CodexAssemblyValidation" }
$responsePath = Join-Path $projectRoot "Temp\$artifactName.rsp"
$outputPath = Join-Path $projectRoot "Temp\$artifactName.dll"
New-Item -ItemType Directory -Path (Split-Path -Parent $responsePath) -Force | Out-Null

[xml]$project = Get-Content -Raw -Encoding utf8 $projectPath
$namespace = New-Object System.Xml.XmlNamespaceManager($project.NameTable)
$namespace.AddNamespace("m", "http://schemas.microsoft.com/developer/msbuild/2003")

$sources = @($project.SelectNodes("//m:Compile", $namespace) | ForEach-Object {
    Join-Path $projectRoot $_.Include
})
if ($isEditorProject) {
    foreach ($testSource in Get-ChildItem (Join-Path $projectRoot "Assets\Tests\Editor") -Filter "*.cs") {
        if ($sources -notcontains $testSource.FullName) {
            $sources += $testSource.FullName
        }
    }
}
if (-not $isEditorProject) {
    $additionalRuntimeSources = @(
        "Assets\Prototype\ExpeditionSaveData.cs"
        "Assets\Prototype\CombatGrowthCards.cs"
        "Assets\Prototype\CombatGrowthCardDatabase.cs"
        "Assets\Prototype\GameMasterDatabase.cs"
    )
    foreach ($relativeSource in $additionalRuntimeSources) {
        $sourcePath = Join-Path $projectRoot $relativeSource
        if ($sources -notcontains $sourcePath) {
            $sources += $sourcePath
        }
    }
}

$references = @($project.SelectNodes("//m:Reference/m:HintPath", $namespace) | ForEach-Object {
    $_.InnerText
})
if ($isEditorProject) {
    $runtimeValidation = Join-Path $projectRoot "Temp\CodexAssemblyValidation.dll"
    $references = @($references | Where-Object {
        -not $_.EndsWith("\Library\ScriptAssemblies\Assembly-CSharp.dll", [StringComparison]::OrdinalIgnoreCase)
    })
    $references += $runtimeValidation
}
$defines = $project.SelectSingleNode(
    "//m:PropertyGroup[contains(@Condition,'Debug')]/m:DefineConstants",
    $namespace
).InnerText

$arguments = @(
    "/nologo"
    "/target:library"
    "/langversion:9.0"
    "/nostdlib+"
    "/unsafe-"
    "/define:$defines"
    "/out:`"$outputPath`""
)
$arguments += $references | ForEach-Object { "/reference:`"$_`"" }
$arguments += $sources | ForEach-Object { "`"$_`"" }

$arguments | Set-Content -Encoding utf8 $responsePath
& $dotnetPath $compilerPath "@$responsePath"
exit $LASTEXITCODE
