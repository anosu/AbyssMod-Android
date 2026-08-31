[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$InteropDirectory,

    [Parameter(Mandatory)]
    [string]$MelonLoaderDirectory,

    [Parameter(Mandatory)]
    [string]$UtilityAssemblyPath
)

$ErrorActionPreference = "Stop"
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$dependencyRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot "dependencies"))
$interopTarget = Join-Path $dependencyRoot "interop/assemblies"
$melonLoaderTarget = Join-Path $dependencyRoot "melonloader/net6"
$utilityTarget = Join-Path $dependencyRoot "managed/Utility.dll"

$projects = @(
    Get-ChildItem -LiteralPath $repoRoot -Filter "*.csproj" -File -Recurse |
        Where-Object {
            $_.FullName -notmatch '[\\/](?:bin|obj|dependencies)[\\/]'
        }
)
if ($projects.Count -ne 1) {
    throw "Expected exactly one Mod project, found $($projects.Count)."
}

[xml]$projectXml = [IO.File]::ReadAllText($projects[0].FullName)

function Get-RequiredReferences {
    param([Parameter(Mandatory)] [string]$PropertyName)

    $prefix = '$(' + $PropertyName + ')/'
    $names = @(
        foreach ($reference in @($projectXml.Project.ItemGroup.Reference)) {
            if ($null -eq $reference) {
                continue
            }

            $hintPath = $reference.GetAttribute("HintPath")
            if ([string]::IsNullOrWhiteSpace($hintPath)) {
                $hintPath = [string]$reference.HintPath
            }
            $normalized = $hintPath.Replace('\', '/')
            if (-not $normalized.StartsWith($prefix, [StringComparison]::Ordinal)) {
                continue
            }

            $name = $normalized.Substring($prefix.Length)
            if ([string]::IsNullOrWhiteSpace($name) -or
                $name.Contains('/') -or
                [IO.Path]::GetExtension($name) -cne ".dll") {
                throw "Unsupported tracked reference path: $hintPath"
            }
            $name
        }
    )
    return @($names | Sort-Object -Unique)
}

function Assert-DependencyTarget {
    param([Parameter(Mandatory)] [string]$Path)

    $fullPath = [IO.Path]::GetFullPath($Path)
    $prefix = $dependencyRoot.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    if (-not $fullPath.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Dependency destination escaped the repository: $fullPath"
    }
    return $fullPath
}

function Sync-ReferenceSet {
    param(
        [Parameter(Mandatory)] [string]$SourceDirectory,
        [Parameter(Mandatory)] [string]$DestinationDirectory,
        [Parameter(Mandatory)] [string[]]$Names
    )

    $source = [IO.Path]::GetFullPath($SourceDirectory)
    $destination = Assert-DependencyTarget -Path $DestinationDirectory
    if (-not (Test-Path -LiteralPath $source -PathType Container)) {
        throw "Reference source directory is missing: $source"
    }
    if ($source -ieq $destination) {
        throw "Reference source and destination must differ: $source"
    }

    foreach ($name in $Names) {
        $sourceFile = Join-Path $source $name
        if (-not (Test-Path -LiteralPath $sourceFile -PathType Leaf)) {
            throw "Required reference is missing: $sourceFile"
        }
    }

    [void][IO.Directory]::CreateDirectory($destination)
    foreach ($name in $Names) {
        Copy-Item -LiteralPath (Join-Path $source $name) -Destination (Join-Path $destination $name) -Force
    }

    Get-ChildItem -LiteralPath $destination -File |
        Where-Object { $_.Name -notin $Names } |
        Remove-Item -Force
}

$interopReferences = @(Get-RequiredReferences -PropertyName "GameInteropReferenceDirectory")
$melonLoaderReferences = @(Get-RequiredReferences -PropertyName "MelonLoaderReferenceDirectory")
if ($interopReferences.Count -eq 0 -or $melonLoaderReferences.Count -eq 0) {
    throw "The Mod project does not declare tracked Interop and MelonLoader references."
}

$utilitySource = [IO.Path]::GetFullPath($UtilityAssemblyPath)
$utilityDestination = Assert-DependencyTarget -Path $utilityTarget
if (-not (Test-Path -LiteralPath $utilitySource -PathType Leaf)) {
    throw "Utility assembly is missing: $utilitySource"
}
if ($utilitySource -ieq $utilityDestination) {
    throw "Utility source and destination must differ: $utilitySource"
}

Sync-ReferenceSet -SourceDirectory $InteropDirectory -DestinationDirectory $interopTarget -Names $interopReferences
Sync-ReferenceSet -SourceDirectory $MelonLoaderDirectory -DestinationDirectory $melonLoaderTarget -Names $melonLoaderReferences

[void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($utilityDestination))
Copy-Item -LiteralPath $utilitySource -Destination $utilityDestination -Force

Write-Host "Synchronized $($projects[0].Name):"
Write-Host "  Interop references: $($interopReferences.Count)"
Write-Host "  MelonLoader references: $($melonLoaderReferences.Count)"
Write-Host "  Utility: $utilityDestination"
