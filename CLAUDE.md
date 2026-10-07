# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

Z2Randomizer is a randomizer for Zelda II: The Adventure of Link (NES), written in C# (.NET 10, nullable enabled). It takes a vanilla ROM plus a flag/options configuration and a seed, and produces a patched ROM. Fork of Digshake's randomizer, maintained by Ellendar. User-facing docs: `README.md`, `FAQ.md`, `PatchNotes.md` (changelog; add entries for user-visible changes).

## Commands

```bash
dotnet build Z2Randomizer.sln
dotnet test Tests/Tests.csproj                                   # MSTest + FluentAssertions
dotnet test Tests/Tests.csproj --filter "FullyQualifiedName~PalaceTests"          # one class
dotnet test Tests/Tests.csproj --filter "FullyQualifiedName~PalaceTests.RollPalaceLengths_AllLowSeed_MatchesExpected"  # one test
dotnet run --project CrossPlatformUI.Desktop                     # desktop (Avalonia) UI
dotnet run --project CommandLine                                 # CLI (see CommandLine/Sample.json)
dotnet publish -c Release CrossPlatformUI.Browser/CrossPlatformUI.Browser.csproj   # web/WASM build (needs `dotnet workload install wasm-tools`)
```

Setup: `FtRandoLib` is a git submodule; run `git submodule update --init` (or clone with `--recurse-submodules`) or the solution won't build. `Tests.csproj` targets `net10.0-windows` but builds and passes on Linux. Shared package versions live in `Directory.Packages.props`; shared MSBuild properties in `Directory.Build.props`.

## Architecture

- **RandomizerCore** – all randomization logic, no UI. Entry point is `Hyrule.Randomize(vanillaRomData, config, progress, ct)` in `Hyrule.cs` (very large; orchestrates the whole pipeline and returns a `RandomizerResult`). Generation retries on failure (`ImpossibleException`); `UserFacingException` is for errors to show the user. `ROM.cs` writes patched bytes; `Asm/*.s` plus `.ips` files are assembly patches applied to the ROM.
  - `RandomizerConfiguration.cs` – user-facing options; serialized to/from a compact **flag string** via `Flags/` (`FlagBuilder`/`FlagReader`, with attributes like `IgnoreInFlags`, `Minimum`, `Maximum`, `DifficultyOnly`). Changing option fields can change flag-string compatibility; `FlagsTests`/`FlagBuilderTests` cover this.
  - `RandomizerProperties.cs` – resolved/derived settings (config + rolled random choices) consumed by generation.
  - `Overworld/` – `World` subclasses (`WestHyrule`, `EastHyrule`, `DeathMountain`, `MazeIsland`), `Location`, terrain/climate generation and connectivity checks. `Requirements.cs`/`RequirementType.cs` model item/spell logic used to verify seeds are completable.
  - `Sidescroll/` – side-view levels: `Palace/` (palace layout generators such as Chaos, Mirror, Coordinate, Reconstructed, plus `PalaceRooms`/`RoomPool` loaded from `PalaceRooms.json` and `CustomRooms.json`) and `Town/`.
  - Other randomizers: `StatRandomizer`, `DropRandomizer`, `Enemy/`, `Music/`, `Sprites/`, `CustomTexts`/hints, `Palettes`.
  - `Random.cs` wraps RNG; tests inject deterministic mocks (`Tests/RandomOverrides.cs`).
- **CoreSourceGenerator** – Roslyn incremental source generator (e.g. `ReactiveObjectSerializeGenerator`) that generates flag serialization for partial classes with `FlagSerializeAttribute`. Generated code is referenced as an analyzer by the UI/core projects.
- **CrossPlatformUI** – shared Avalonia UI (MVVM with ReactiveUI, compiled bindings). Hosts: `CrossPlatformUI.Desktop` (with `Desktop.Common`), `CrossPlatformUI.Browser` (WASM), and `Web`.
- **CommandLine** – headless CLI generator driven by JSON player options. **Statistics** – bulk-seed statistics tool (EF Core DB). **DumpRooms** / **ValidateRooms** – dev utilities for palace room data.
- **Tests** – unit tests for flags, palaces, requirements, room pools/serialization, hints, RNG helpers.

CI is in `.github/workflows` (Debug/branch builds and per-version release workflows; main development branch is `main`).
