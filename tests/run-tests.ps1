param(
    [switch]$NoBuild,
    [switch]$CollectCoverage,
    [string[]]$Projects,
    [string]$Filter
)

$repoRoot = Split-Path -Parent $PSScriptRoot

Push-Location $repoRoot
try {
    $dotnetHomeRoot = if ([string]::IsNullOrWhiteSpace($env:TEMP)) {
        Join-Path $repoRoot ".tmp-dotnet-home"
    }
    else {
        Join-Path $env:TEMP "aci-dotnet-home"
    }

    $env:DOTNET_CLI_HOME = $dotnetHomeRoot
    if (!(Test-Path $env:DOTNET_CLI_HOME)) {
        New-Item -ItemType Directory -Path $env:DOTNET_CLI_HOME | Out-Null
    }

    $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
    $env:MSBuildEnableWorkloadResolver = "false"

    $defaultProjects = @(
        "tests\ACI.Core.Tests\ACI.Core.Tests.csproj",
        "tests\ACI.Framework.Tests\ACI.Framework.Tests.csproj",
        "tests\ACI.LLM.Tests\ACI.LLM.Tests.csproj",
        "tests\ACI.Server.Tests\ACI.Server.Tests.csproj",
        "tests\ACI.Storage.Tests\ACI.Storage.Tests.csproj"
    )

    $projectMap = @{
        "core"      = "tests\ACI.Core.Tests\ACI.Core.Tests.csproj"
        "framework" = "tests\ACI.Framework.Tests\ACI.Framework.Tests.csproj"
        "llm"       = "tests\ACI.LLM.Tests\ACI.LLM.Tests.csproj"
        "server"    = "tests\ACI.Server.Tests\ACI.Server.Tests.csproj"
        "storage"   = "tests\ACI.Storage.Tests\ACI.Storage.Tests.csproj"
    }

    $selectedProjects = if ($Projects -and $Projects.Count -gt 0) {
        $resolved = @()
        foreach ($project in $Projects) {
            $key = $project.ToLowerInvariant()
            if ($projectMap.ContainsKey($key)) {
                $resolved += $projectMap[$key]
                continue
            }

            $resolved += $project
        }
        $resolved
    }
    else {
        $defaultProjects
    }

    foreach ($project in $selectedProjects) {
        $args = @(
            "test",
            "--nologo",
            $project,
            "-m:1",
            "-p:BuildInParallel=false",
            "-p:MSBuildEnableWorkloadResolver=false",
            "-p:RestoreIgnoreFailedSources=true",
            "-p:NuGetAudit=false"
        )

        if ($NoBuild) {
            $args += "--no-build"
            $args += "--no-restore"
        }

        if (![string]::IsNullOrWhiteSpace($Filter)) {
            $args += "--filter"
            $args += $Filter
        }

        if ($CollectCoverage) {
            $args += "--collect:XPlat Code Coverage"
        }

        Write-Host "Running: dotnet $($args -join ' ')"
        dotnet @args

        if ($LASTEXITCODE -ne 0) {
            exit $LASTEXITCODE
        }
    }
}
finally {
    Pop-Location
}
