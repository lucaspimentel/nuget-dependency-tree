# NuGet Dependency Tree

A .NET CLI tool that visualizes NuGet package dependencies as a tree using the NuGet API v3.

## Features

- 🔍 Recursively fetches all package dependencies
- 🎨 Beautiful tree visualization using Spectre.Console
- 🎯 Target Framework Moniker (TFM) filtering
- 📦 Version-specific dependency analysis
- 🔄 Circular dependency detection
- 🚀 Built with .NET 10

## Installation

```bash
# Clone or navigate to the project directory
cd NuGetDependencyTree

# Build the project
dotnet build

# Run the tool
dotnet run -- <PACKAGE> [OPTIONS]
```

## Usage

### Basic Usage

Show dependencies for the latest version of a package:

```bash
dotnet run -- Microsoft.EntityFrameworkCore
```

### Specify a Version

Analyze a specific package version:

```bash
dotnet run -- Newtonsoft.Json --version 13.0.3
# or use the short form
dotnet run -- Newtonsoft.Json -v 13.0.3
```

### Target Framework Filtering

View dependencies for a specific target framework:

```bash
dotnet run -- Microsoft.EntityFrameworkCore -v 3.1.15 -f net461
dotnet run -- Microsoft.EntityFrameworkCore -v 3.1.15 -f netcoreapp3.1
dotnet run -- Microsoft.Extensions.Http -v 8.0.0 -f net8.0
```

### Get Help

```bash
dotnet run -- --help
```

## Command-Line Options

| Option | Short | Description |
|--------|-------|-------------|
| `<PACKAGE>` | (required) | The NuGet package name |
| `--version <VERSION>` | `-v` | Package version (defaults to latest) |
| `--framework <TFM>` | `-f` | Target framework moniker (e.g., net8.0, net461, netstandard2.0) |
| `--help` | `-h` | Show help information |

## Target Framework Monikers (TFM)

Common TFM values include:

- **Modern .NET**: `net8.0`, `net7.0`, `net6.0`, `net5.0`
- **.NET Core**: `netcoreapp3.1`, `netcoreapp3.0`, `netcoreapp2.1`
- **.NET Framework**: `net48`, `net472`, `net471`, `net47`, `net462`, `net461`, `net46`, `net452`, `net451`, `net45`
- **.NET Standard**: `netstandard2.1`, `netstandard2.0`, `netstandard1.6`, `netstandard1.5`, etc.

## Examples

### Compare Dependencies Across Frameworks

See how dependencies differ between target frameworks:

```bash
# .NET 8.0 dependencies
dotnet run -- Microsoft.Extensions.Http -v 8.0.0 -f net8.0

# .NET 6.0 dependencies (may differ)
dotnet run -- Microsoft.Extensions.Http -v 8.0.0 -f net6.0
```

### Analyze Legacy Packages

```bash
dotnet run -- Microsoft.AspNetCore.Mvc -v 2.2.0 -f netstandard2.0
```

### View Dependencies for Packages Without Dependencies

```bash
dotnet run -- Newtonsoft.Json
```

## Output

The tool displays a colorful tree structure:

- **Cyan**: Root package name
- **Yellow**: Dependency names
- **Grey**: Version ranges
- **Dim**: Circular references (when detected)

Example output:

```
Microsoft.EntityFrameworkCore 3.1.15 (net461)
├── Microsoft.EntityFrameworkCore.Abstractions [3.1.15, )
├── Microsoft.EntityFrameworkCore.Analyzers [3.1.15, )
├── Microsoft.Bcl.AsyncInterfaces [1.1.1, )
├── Microsoft.Bcl.HashCode [1.1.1, )
├── Microsoft.Extensions.Caching.Memory [3.1.15, )
│   ├── Microsoft.Extensions.Caching.Abstractions [6.0.0, )
│   │   └── Microsoft.Extensions.Primitives [6.0.0, )
│   └── ...
└── ...
```

## How It Works

1. **Service Discovery**: Queries the NuGet API v3 service index to find the registration base URL
2. **Package Metadata**: Fetches package registration data including all versions
3. **Version Selection**: Selects the specified version or defaults to the latest
4. **Framework Filtering**: Filters dependency groups by the specified target framework (with fallback to netstandard or framework-agnostic dependencies)
5. **Recursive Resolution**: Recursively fetches dependencies for each package
6. **Circular Detection**: Tracks visited packages to detect and display circular references
7. **Tree Visualization**: Renders the complete dependency tree using Spectre.Console

## Framework Matching Strategy

When you specify a target framework, the tool uses this matching strategy:

1. **Exact Match**: First looks for an exact TFM match
2. **.NET Standard Fallback**: Falls back to netstandard dependencies (compatible with most frameworks)
3. **Framework-Agnostic**: Uses dependencies with no specific framework requirement
4. **Default**: If none found, uses the highest available framework

## Technical Details

- **Language**: C# 13 / .NET 10
- **NuGet API**: v3 Registration API
- **UI Framework**: Spectre.Console & Spectre.Console.Cli
- **HTTP Client**: System.Net.Http with JSON deserialization

## Limitations

- Large dependency trees (e.g., ASP.NET Core MVC) may take time to fetch
- Network connectivity required to access NuGet API
- Version ranges are displayed but not resolved to specific versions
- Framework compatibility is based on exact or heuristic matching

## License

MIT
