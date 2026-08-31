param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [string]$ExpectedVersion,

    [ValidateRange(60, 1800)]
    [int]$TimeoutSeconds = 600
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$modProject = Join-Path $repoRoot "AbyssMod/AbyssMod.csproj"
$dependencyRoot = Join-Path $repoRoot "dependencies"
$interopDirectory = Join-Path $dependencyRoot "interop/assemblies"
$melonLoaderDirectory = Join-Path $dependencyRoot "melonloader/net6"
$fontBundle = Join-Path $dependencyRoot "font/ttcuyuanj"
$utilityAssembly = Join-Path $dependencyRoot "managed/Utility.dll"
$dotnet = (Get-Command dotnet -ErrorAction Stop).Source

function Assert-RequiredPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [ValidateSet("Leaf", "Container")]
        [string]$PathType,

        [Parameter(Mandatory = $true)]
        [string]$Label
    )

    if (-not (Test-Path -LiteralPath $Path -PathType $PathType)) {
        throw "$Label is missing: $Path"
    }
}

function Invoke-CheckedProcess {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,

        [Parameter(Mandatory = $true)]
        [string[]]$ArgumentList,

        [Parameter(Mandatory = $true)]
        [string]$WorkingDirectory,

        [Parameter(Mandatory = $true)]
        [int]$TimeoutSeconds
    )

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $FilePath
    $startInfo.WorkingDirectory = $WorkingDirectory
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    foreach ($argument in $ArgumentList) {
        [void]$startInfo.ArgumentList.Add($argument)
    }

    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    try {
        if (-not $process.Start()) {
            throw "Failed to start process: $FilePath"
        }

        $stdoutTask = $process.StandardOutput.ReadToEndAsync()
        $stderrTask = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            $process.Kill($true)
            throw "Process timed out after $TimeoutSeconds seconds: $FilePath"
        }

        $stdout = $stdoutTask.GetAwaiter().GetResult()
        $stderr = $stderrTask.GetAwaiter().GetResult()
        if (-not [string]::IsNullOrWhiteSpace($stdout)) {
            Write-Host $stdout.TrimEnd()
        }
        if (-not [string]::IsNullOrWhiteSpace($stderr)) {
            Write-Host $stderr.TrimEnd() -ForegroundColor DarkYellow
        }

        if ($process.ExitCode -ne 0) {
            throw "Process failed with exit code $($process.ExitCode): $FilePath"
        }
    } finally {
        if (-not $process.HasExited) {
            $process.Kill($true)
        }
        $process.Dispose()
    }
}

function Get-StreamSha256 {
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.Stream]$Stream
    )

    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        return [System.Convert]::ToHexString($sha256.ComputeHash($Stream)).ToLowerInvariant()
    } finally {
        $sha256.Dispose()
    }
}

Assert-RequiredPath -Path $modProject -PathType Leaf -Label "Mod project"
Assert-RequiredPath -Path $utilityAssembly -PathType Leaf -Label "Utility assembly"
Assert-RequiredPath -Path $interopDirectory -PathType Container -Label "Game Interop directory"
Assert-RequiredPath -Path $melonLoaderDirectory -PathType Container -Label "MelonLoader reference directory"
Assert-RequiredPath -Path $fontBundle -PathType Leaf -Label "Font AssetBundle"
Assert-RequiredPath -Path (Join-Path $interopDirectory "Il2CppProject.dll") -PathType Leaf -Label "Il2CppProject Interop reference"
Assert-RequiredPath -Path (Join-Path $interopDirectory "Il2Cppmscorlib.dll") -PathType Leaf -Label "Il2Cpp mscorlib reference"
Assert-RequiredPath -Path (Join-Path $melonLoaderDirectory "MelonLoader.dll") -PathType Leaf -Label "MelonLoader reference"

[xml]$projectXml = Get-Content -LiteralPath $modProject -Raw
$version = @($projectXml.Project.PropertyGroup.Version | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })[0]
if ($version -notmatch '^[0-9]+\.[0-9]+\.[0-9]+(?:[-.][0-9A-Za-z.-]+)?$') {
    throw "The project version is missing or invalid: $version"
}
$releaseVersion = "v$version"
if (-not [string]::IsNullOrWhiteSpace($ExpectedVersion)) {
    $normalizedExpectedVersion = $ExpectedVersion.Trim().TrimStart('v')
    if ($normalizedExpectedVersion -cne $version) {
        throw "Expected version '$ExpectedVersion' does not match project version '$version'."
    }
}

$modBuildParameters = @{
    FilePath = $dotnet
    WorkingDirectory = $repoRoot
    TimeoutSeconds = $TimeoutSeconds
    ArgumentList = @(
        "build",
        $modProject,
        "-c", $Configuration,
        "--nologo",
        "--no-incremental",
        "-p:UtilityAssemblyPath=$utilityAssembly"
    )
}
Invoke-CheckedProcess @modBuildParameters

$modAssembly = Join-Path $repoRoot "AbyssMod/bin/$Configuration/AbyssMod.dll"
Assert-RequiredPath -Path $modAssembly -PathType Leaf -Label "Built Mod assembly"
$builtAssembly = [Reflection.Assembly]::LoadFile($modAssembly)
$modInfoType = $builtAssembly.GetType("$($builtAssembly.GetName().Name).ModInfo", $false)
$modInfoField = if ($null -eq $modInfoType) { $null } else { $modInfoType.GetField("Version") }
$modInfoVersion = if ($null -eq $modInfoField) { $null } else { $modInfoField.GetRawConstantValue() }
if ($modInfoVersion -cne $version) {
    throw "ModInfo version '$modInfoVersion' does not match project version '$version'."
}

$releaseRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot "artifacts/release"))
$outputDirectory = [System.IO.Path]::GetFullPath((Join-Path $releaseRoot $releaseVersion))
$archivePath = Join-Path $outputDirectory "AbyssMod-Android.zip"
$archiveTempPath = $archivePath + ".tmp"
$sumsPath = Join-Path $outputDirectory "SHA256SUMS.txt"
if (-not $outputDirectory.StartsWith($releaseRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Release output escaped the repository artifacts directory: $outputDirectory"
}

$inputs = [ordered]@{
    "Mods/AbyssMod/AbyssMod.dll" = $modAssembly
    "Mods/AbyssMod/Utility.dll" = $utilityAssembly
    "UserData/AbyssMod/ttcuyuanj" = $fontBundle
}

New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$unexpectedFiles = @(
    Get-ChildItem -LiteralPath $outputDirectory -Force |
        Where-Object { $_.Name -notin @("AbyssMod-Android.zip", "AbyssMod-Android.zip.tmp", "SHA256SUMS.txt") }
)
if ($unexpectedFiles.Count -ne 0) {
    throw "Release output contains files not owned by this script: $($unexpectedFiles.Name -join ', ')"
}

$archive = $null
$archiveStream = $null
try {
    if (Test-Path -LiteralPath $archiveTempPath -PathType Leaf) {
        Remove-Item -LiteralPath $archiveTempPath -Force
    }

    $archiveStream = [System.IO.File]::Open(
        $archiveTempPath,
        [System.IO.FileMode]::CreateNew,
        [System.IO.FileAccess]::ReadWrite,
        [System.IO.FileShare]::None
    )
    $archive = [System.IO.Compression.ZipArchive]::new(
        $archiveStream,
        [System.IO.Compression.ZipArchiveMode]::Create,
        $false
    )
    foreach ($entryName in $inputs.Keys) {
        $entry = $archive.CreateEntry(
            $entryName,
            [System.IO.Compression.CompressionLevel]::Optimal
        )
        $entry.LastWriteTime = [System.DateTimeOffset]::new(
            1980,
            1,
            1,
            0,
            0,
            0,
            [System.TimeSpan]::Zero
        )

        $inputStream = [System.IO.File]::OpenRead($inputs[$entryName])
        $entryStream = $entry.Open()
        try {
            $inputStream.CopyTo($entryStream)
        } finally {
            $entryStream.Dispose()
            $inputStream.Dispose()
        }
    }

    $archive.Dispose()
    $archive = $null
    $archiveStream.Dispose()
    $archiveStream = $null
    [System.IO.File]::Move($archiveTempPath, $archivePath, $true)
} finally {
    if ($null -ne $archive) {
        $archive.Dispose()
    }
    if ($null -ne $archiveStream) {
        $archiveStream.Dispose()
    }
    if (Test-Path -LiteralPath $archiveTempPath -PathType Leaf) {
        Remove-Item -LiteralPath $archiveTempPath -Force -ErrorAction SilentlyContinue
    }
}

$expectedEntries = @($inputs.Keys)
$verificationArchive = [System.IO.Compression.ZipFile]::OpenRead($archivePath)
try {
    $actualEntries = @($verificationArchive.Entries | ForEach-Object { $_.FullName })
    if ([string]::Join("`n", $actualEntries) -ne [string]::Join("`n", $expectedEntries)) {
        throw "Release archive entries do not match the expected deployment layout."
    }

    foreach ($entryName in $expectedEntries) {
        $entryStream = $verificationArchive.GetEntry($entryName).Open()
        try {
            $entryHash = Get-StreamSha256 -Stream $entryStream
        } finally {
            $entryStream.Dispose()
        }

        $inputHash = (Get-FileHash -LiteralPath $inputs[$entryName] -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($entryHash -ne $inputHash) {
            throw "Release archive content hash mismatch: $entryName"
        }
    }
} finally {
    $verificationArchive.Dispose()
}

$archiveHash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
[System.IO.File]::WriteAllText(
    $sumsPath,
    "$archiveHash  AbyssMod-Android.zip`n",
    [System.Text.UTF8Encoding]::new($false)
)

[ordered]@{
    Version = $releaseVersion
    Archive = $archivePath
    ArchiveSha256 = $archiveHash
    Checksums = $sumsPath
    ModSha256 = (Get-FileHash -LiteralPath $modAssembly -Algorithm SHA256).Hash.ToLowerInvariant()
    UtilitySha256 = (Get-FileHash -LiteralPath $utilityAssembly -Algorithm SHA256).Hash.ToLowerInvariant()
    FontSha256 = (Get-FileHash -LiteralPath $fontBundle -Algorithm SHA256).Hash.ToLowerInvariant()
    Entries = $expectedEntries
} | ConvertTo-Json -Depth 4
