# Copyright (c) 2026 Neil Colvin. MIT License; see LICENSE.
param([Parameter(Mandatory)][string]$PackagePath)
$ErrorActionPreference='Stop'
& "$PSScriptRoot/../build/shared/nuget-artifacts/Test-NuGetDocumentation.ps1" @PSBoundParameters
