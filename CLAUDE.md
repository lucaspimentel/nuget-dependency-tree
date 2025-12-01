# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

NuGet Dependency Tree is a .NET 10 CLI tool that visualizes NuGet package dependencies recursively using the NuGet API v3. It's a single-file console application (`Program.cs`) with no tests or solution file.

## Build and Run Commands

```bash
# Build the project
dotnet build

# Run the tool (note the -- separator before arguments)
dotnet run -- <PACKAGE> [OPTIONS]

# Examples
dotnet run -- Microsoft.EntityFrameworkCore
dotnet run -- Newtonsoft.Json -v 13.0.3
dotnet run -- Microsoft.Extensions.Http -v 8.0.0 -f net8.0
dotnet run -- --help

# Restore packages (if needed)
dotnet restore
```

## Architecture

### Single-File Design

All code lives in `Program.cs` with this structure:

1. **Entry Point**: Uses `Spectre.Console.Cli` `CommandApp<T>` pattern
2. **DependencyTreeCommand**: Main command implementation with `Settings` class for CLI arguments
3. **NuGetClient**: Handles NuGet API v3 interactions
4. **Data Models**: JSON serialization classes for NuGet API responses (`ServiceIndex`, `RegistrationIndex`, `RegistrationPage`, `RegistrationLeaf`, `CatalogEntry`, `DependencyGroup`, `Dependency`)
5. **Domain Models**: `PackageInfo` and `DependencyInfo` for internal representation

### Key Components

**NuGetClient.GetPackageInfoAsync()**:
- Fetches package metadata from NuGet API v3 Registration endpoint
- Handles paginated registration indexes (packages with many versions require fetching multiple pages)
- Implements framework matching strategy: exact match → netstandard fallback → framework-agnostic → default
- Uses `FindBestMatchingFramework()` helper for TFM resolution

**BuildDependencyTreeAsync()**:
- Recursive function that builds the Spectre.Console `Tree` structure
- Tracks visited packages via `HashSet<string>` to detect circular dependencies
- Fetches each dependency's metadata and recursively builds subtrees
- Passes target framework through recursive calls for consistent filtering

### NuGet API Flow

1. Query service index at `https://api.nuget.org/v3/index.json` for `RegistrationsBaseUrl`
2. Fetch registration index at `{baseUrl}/{packageId}/index.json`
3. Load all registration pages (may be inlined or require additional fetches)
4. Select version (specified or latest)
5. Filter dependency groups by target framework
6. Recursively resolve dependencies

### Target Framework Matching

When a TFM is specified (via `-f`), the tool:
1. Looks for exact match in `DependencyGroup.TargetFramework`
2. Falls back to `netstandard*` dependencies (broadest compatibility)
3. Falls back to framework-agnostic dependencies (empty/null TFM)
4. Defaults to highest available framework

This ensures reasonable results even when exact TFM isn't available.

### CLI Argument Handling

Uses `Spectre.Console.Cli` attributes:
- `[CommandArgument]` for positional package name
- `[CommandOption]` for `-v|--version` and `-f|--framework`
- `AsyncCommand<Settings>` base class with `ExecuteAsync()` override

### Markup Escaping

All user-facing strings use `.EscapeMarkup()` to prevent Spectre.Console markup injection from package names containing special characters like `[]`.

## Dependencies

- **Spectre.Console** (v0.54.0): Tree rendering and ANSI console output
- **Spectre.Console.Cli** (v0.53.1): Command-line argument parsing
- **System.Net.Http.Json**: Built-in JSON deserialization for NuGet API

## Code Conventions

- Top-level statements for entry point
- File-scoped classes (no namespaces)
- `required` properties for non-nullable reference types
- Async/await throughout for API calls
- No `#region` directives (per user preferences)
