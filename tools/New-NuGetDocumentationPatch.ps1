# Copyright (c) 2026 Neil Colvin. MIT License; see LICENSE.
param([string]$PlanPath=(Join-Path $PSScriptRoot '../NuGetDocumentationPatch.json'),[string]$OutputDirectory)
$ErrorActionPreference='Stop'
& "$PSScriptRoot/../build/shared/nuget-artifacts/New-NuGetDocumentationPatch.ps1" -Root (Split-Path $PSScriptRoot -Parent) @PSBoundParameters
