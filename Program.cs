using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Spectre.Console;
using Spectre.Console.Cli;

var app = new CommandApp<DependencyTreeCommand>();
return await app.RunAsync(args);

class DependencyTreeCommand : AsyncCommand<DependencyTreeCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<PACKAGE>")]
        [Description("The NuGet package name")]
        public required string PackageName { get; init; }

        [CommandOption("-v|--version <VERSION>")]
        [Description("The package version (defaults to latest)")]
        public string? Version { get; init; }

        [CommandOption("-f|--framework <TFM>")]
        [Description("Target framework moniker (e.g., net8.0, net461, netstandard2.0)")]
        public string? TargetFramework { get; init; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var client = new NuGetClient();

        try
        {
            await AnsiConsole.Status()
                .StartAsync("Fetching package metadata...", async ctx =>
                {
                    var packageInfo = await client.GetPackageInfoAsync(
                        settings.PackageName,
                        settings.Version,
                        settings.TargetFramework);

                    if (packageInfo == null)
                    {
                        AnsiConsole.MarkupLine($"[red]Package '{settings.PackageName}' not found[/]");
                        return;
                    }

                    ctx.Status("Building dependency tree...");
                    var tfmLabel = settings.TargetFramework != null ? $" [dim]({settings.TargetFramework})[/]" : "";
                    var tree = new Tree($"[bold cyan]{packageInfo.Id.EscapeMarkup()}[/] [grey]{packageInfo.Version.EscapeMarkup()}[/]{tfmLabel}");

                    if (packageInfo.Dependencies != null && packageInfo.Dependencies.Count > 0)
                    {
                        foreach (var dep in packageInfo.Dependencies)
                        {
                            var depNode = tree.AddNode($"[yellow]{dep.Id.EscapeMarkup()}[/] [grey]{dep.Range.EscapeMarkup()}[/]");
                            var depInfo = await client.GetPackageInfoAsync(dep.Id, null, settings.TargetFramework);
                            if (depInfo != null)
                            {
                                await BuildDependencyTreeAsync(client, depNode, depInfo, settings.TargetFramework, new HashSet<string> { $"{packageInfo.Id}@{packageInfo.Version}" });
                            }
                        }
                    }

                    AnsiConsole.Write(tree);
                });
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
            return 1;
        }

        return 0;
    }

    private static async Task BuildDependencyTreeAsync(NuGetClient client, TreeNode parentNode, PackageInfo package, string? targetFramework, HashSet<string> visited)
    {
        var packageKey = $"{package.Id}@{package.Version}";

        if (visited.Contains(packageKey))
        {
            parentNode.AddNode($"[dim]{package.Id.EscapeMarkup()} {package.Version.EscapeMarkup()} (circular reference)[/]");
            return;
        }

        visited.Add(packageKey);

        if (package.Dependencies == null || package.Dependencies.Count == 0)
        {
            return;
        }

        foreach (var dep in package.Dependencies)
        {
            var depNode = parentNode.AddNode($"[yellow]{dep.Id.EscapeMarkup()}[/] [grey]{dep.Range.EscapeMarkup()}[/]");

            var depInfo = await client.GetPackageInfoAsync(dep.Id, null, targetFramework);
            if (depInfo != null)
            {
                await BuildDependencyTreeAsync(client, depNode, depInfo, targetFramework, new HashSet<string>(visited));
            }
        }
    }
}

class NuGetClient
{
    private readonly HttpClient _httpClient;
    private const string ServiceIndexUrl = "https://api.nuget.org/v3/index.json";
    private string? _registrationBaseUrl;

    public NuGetClient()
    {
        _httpClient = new HttpClient();
    }

    public async Task<PackageInfo?> GetPackageInfoAsync(string packageId, string? version, string? targetFramework)
    {
        if (_registrationBaseUrl == null)
        {
            var serviceIndex = await _httpClient.GetFromJsonAsync<ServiceIndex>(ServiceIndexUrl);
            var registrationResource = serviceIndex?.Resources?.FirstOrDefault(r =>
                r.Type == "RegistrationsBaseUrl/3.6.0" ||
                r.Type == "RegistrationsBaseUrl");

            if (registrationResource == null)
            {
                throw new Exception("Could not find registration base URL in service index");
            }

            _registrationBaseUrl = registrationResource.Id.TrimEnd('/');
        }

        var registrationUrl = $"{_registrationBaseUrl}/{packageId.ToLowerInvariant()}/index.json";

        var registration = await _httpClient.GetFromJsonAsync<RegistrationIndex>(registrationUrl);
        if (registration?.Items == null || registration.Items.Count == 0)
        {
            return null;
        }

        var allLeaves = new List<RegistrationLeaf>();

        foreach (var page in registration.Items)
        {
            List<RegistrationLeaf>? pageItems;

            if (page.Items != null)
            {
                pageItems = page.Items;
            }
            else if (page.Id != null)
            {
                var loadedPage = await _httpClient.GetFromJsonAsync<RegistrationPage>(page.Id);
                pageItems = loadedPage?.Items;
            }
            else
            {
                pageItems = null;
            }

            if (pageItems != null)
            {
                allLeaves.AddRange(pageItems);
            }
        }

        if (allLeaves.Count == 0)
        {
            return null;
        }

        RegistrationLeaf? targetLeaf;
        if (version != null)
        {
            targetLeaf = allLeaves.FirstOrDefault(i =>
                i.CatalogEntry?.Version?.Equals(version, StringComparison.OrdinalIgnoreCase) == true);
        }
        else
        {
            targetLeaf = allLeaves.LastOrDefault();
        }

        if (targetLeaf?.CatalogEntry == null)
        {
            return null;
        }

        var catalogEntry = targetLeaf.CatalogEntry;
        var dependencies = new List<DependencyInfo>();

        if (catalogEntry.DependencyGroups != null)
        {
            DependencyGroup? targetGroup;

            if (targetFramework != null)
            {
                targetGroup = FindBestMatchingFramework(catalogEntry.DependencyGroups, targetFramework);
            }
            else
            {
                targetGroup = catalogEntry.DependencyGroups
                    .OrderByDescending(g => g.TargetFramework)
                    .FirstOrDefault();
            }

            if (targetGroup?.Dependencies != null)
            {
                dependencies.AddRange(targetGroup.Dependencies.Select(d => new DependencyInfo
                {
                    Id = d.Id,
                    Range = d.Range ?? "*"
                }));
            }
        }

        return new PackageInfo
        {
            Id = catalogEntry.Id ?? packageId,
            Version = catalogEntry.Version ?? "unknown",
            Dependencies = dependencies
        };
    }

    private static DependencyGroup? FindBestMatchingFramework(List<DependencyGroup> groups, string targetFramework)
    {
        var exactMatch = groups.FirstOrDefault(g =>
            g.TargetFramework?.Equals(targetFramework, StringComparison.OrdinalIgnoreCase) == true);

        if (exactMatch != null)
        {
            return exactMatch;
        }

        var netStandardMatch = groups.FirstOrDefault(g =>
            g.TargetFramework?.StartsWith("netstandard", StringComparison.OrdinalIgnoreCase) == true);

        if (netStandardMatch != null)
        {
            return netStandardMatch;
        }

        return groups.FirstOrDefault(g => string.IsNullOrEmpty(g.TargetFramework)) ??
               groups.OrderByDescending(g => g.TargetFramework).FirstOrDefault();
    }
}

class PackageInfo
{
    public required string Id { get; set; }
    public required string Version { get; set; }
    public List<DependencyInfo>? Dependencies { get; set; }
}

class DependencyInfo
{
    public required string Id { get; set; }
    public required string Range { get; set; }
}

class ServiceIndex
{
    [JsonPropertyName("resources")]
    public List<ServiceResource>? Resources { get; set; }
}

class ServiceResource
{
    [JsonPropertyName("@id")]
    public required string Id { get; set; }

    [JsonPropertyName("@type")]
    public required string Type { get; set; }
}

class RegistrationIndex
{
    [JsonPropertyName("items")]
    public List<RegistrationPage>? Items { get; set; }
}

class RegistrationPage
{
    [JsonPropertyName("@id")]
    public string? Id { get; set; }

    [JsonPropertyName("items")]
    public List<RegistrationLeaf>? Items { get; set; }
}

class RegistrationLeaf
{
    [JsonPropertyName("catalogEntry")]
    public CatalogEntry? CatalogEntry { get; set; }
}

class CatalogEntry
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("dependencyGroups")]
    public List<DependencyGroup>? DependencyGroups { get; set; }
}

class DependencyGroup
{
    [JsonPropertyName("targetFramework")]
    public string? TargetFramework { get; set; }

    [JsonPropertyName("dependencies")]
    public List<Dependency>? Dependencies { get; set; }
}

class Dependency
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("range")]
    public string? Range { get; set; }
}
