param(
    [switch]$NoBuild,
    [switch]$CollectCoverage
)

$repoRoot = Split-Path -Parent $PSScriptRoot

Push-Location $repoRoot
try {
    $dotnetHomeRoot = if ([string]::IsNullOrWhiteSpace($env:TEMP)) {
        Join-Path $repoRoot ".tmp-dotnet-home"
    } else {
        Join-Path $env:TEMP "aci-dotnet-home"
    }

    $env:DOTNET_CLI_HOME = $dotnetHomeRoot
    if (!(Test-Path $env:DOTNET_CLI_HOME)) {
        New-Item -ItemType Directory -Path $env:DOTNET_CLI_HOME | Out-Null
    }

    $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
    $env:MSBuildEnableWorkloadResolver = "false"

    $projects = @(
        "tests\ACI.Core.Tests\ACI.Core.Tests.csproj",
        "tests\ACI.Framework.Tests\ACI.Framework.Tests.csproj"
    )

    foreach ($project in $projects) {
        $args = @(
            "test",
            $project,
            "--nologo",
            "-m:1",
            "-p:BuildInParallel=false"
        )

        if ($NoBuild) {
            $args += "--no-build"
            $args += "--no-restore"
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
