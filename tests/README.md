# Tests Stage 0 Baseline

This folder now contains the phase-0 testing scaffold for ACI.

## Goals
- Keep test projects runnable from day 1.
- Establish shared helpers before large-scale test expansion.
- Keep conventions stable so later test batches stay consistent.

## Structure
- `Common/`: Shared helpers used by all test projects.
- `ACI.Core.Tests/`: Unit tests focused on `ACI.Core`.
- `ACI.Framework.Tests/`: Unit tests focused on `ACI.Framework`.

## Naming Rules
- Test file: `<TargetType>Tests.cs`
- Test method: `<MethodOrBehavior>_<Scenario>_<ExpectedResult>`
- Arrange/Act/Assert blocks should stay explicit and short.

## Shared Helpers
- `Common/TestData`: IDs, JSON parsing, simple builders.
- `Common/Fakes`: lightweight fake and spy implementations.

## Run
- Quick run (all tests): `dotnet test ACI.sln`
- One-command script: `tests/run-tests.ps1`
- With coverage: `tests/run-tests.ps1 -CollectCoverage`
- Offline/no-restore run: `tests/run-tests.ps1 -NoBuild`
