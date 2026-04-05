# Research: Adding a `--version` Flag to Spectre.Console.Cli in .NET 9

**Date:** 2025  
**Scope:** Spectre.Console.Cli version flag implementation, Assembly version flow, real-world patterns  
**Branch examined:** `spectreconsole/spectre.console` @ `0.53.1-hotfix`  

---

## Executive Summary

Spectre.Console.Cli has **first-class, built-in `--version` flag support** activated by a single configuration call. Setting `config.SetApplicationVersion(...)` or `config.UseAssemblyInformationalVersion()` registers the version string; the framework then automatically wires `-v|--version` as a recognized flag at the root level, outputs the string to the console, and exits with code `0`. The recommended best practice is to use `UseAssemblyInformationalVersion()` (a convenience extension), which reads `AssemblyInformationalVersionAttribute` — the attribute the .NET SDK automatically populates from your `.csproj`'s `<Version>` (or `<InformationalVersion>`) property at build time.

---

## 1. Does Spectre.Console.Cli Have Built-in Version Support?

**Yes — fully built-in.** There are two API surfaces:

### 1a. `SetApplicationVersion` — Extension Method on `IConfigurator`

**File:** `src/Spectre.Console.Cli/ConfiguratorExtensions.cs`  
**Repo:** [spectreconsole/spectre.console](https://github.com/spectreconsole/spectre.console) (also mirrored at [spectreconsole/spectre.console.cli](https://github.com/spectreconsole/spectre.console.cli))

```csharp
/// <summary>
/// Sets the version of the application.
/// </summary>
/// <param name="configurator">The configurator.</param>
/// <param name="version">The version of application.</param>
/// <returns>A configurator that can be used to configure the application further.</returns>
public static IConfigurator SetApplicationVersion(this IConfigurator configurator, string version)
{
    if (configurator == null)
    {
        throw new ArgumentNullException(nameof(configurator));
    }

    configurator.Settings.ApplicationVersion = version;
    return configurator;
}
```

This is a fluent extension method on `IConfigurator` that sets `Settings.ApplicationVersion` (a `string?` property on `ICommandAppSettings`).

### 1b. `UseAssemblyInformationalVersion` — The "Auto-Wire" Helper

Same file (`ConfiguratorExtensions.cs`), this extension reads the attribute automatically:

```csharp
/// <summary>
/// Uses the version retrieved from the AssemblyInformationalVersionAttribute
/// as the application's version.
/// </summary>
public static IConfigurator UseAssemblyInformationalVersion(this IConfigurator configurator)
{
    if (configurator == null)
    {
        throw new ArgumentNullException(nameof(configurator));
    }

    configurator.Settings.ApplicationVersion =
        VersionHelper.GetVersion(Assembly.GetEntryAssembly());

    return configurator;
}
```

### 1c. The `ICommandAppSettings.ApplicationVersion` Property

**File:** `src/Spectre.Console.Cli/ICommandAppSettings.cs`

```csharp
/// <summary>
/// Gets or sets the application version (use it to override auto-detected value).
/// </summary>
string? ApplicationVersion { get; set; }
```

The concrete implementation lives in `src/Spectre.Console.Cli/Internal/Configuration/CommandAppSettings.cs` as a plain auto-property `public string? ApplicationVersion { get; set; }`.

### 1d. How `--version` Gets Intercepted at Runtime

**File:** `src/Spectre.Console.Cli/Internal/CommandExecutor.cs`

The executor checks for `-v`/`--version` as the **first argument** before the full parse:

```csharp
// Got at least one argument?
var firstArgument = arguments.FirstOrDefault();
if (firstArgument != null)
{
    // Asking for version?
    if (firstArgument.Equals("-v", StringComparison.OrdinalIgnoreCase) ||
        firstArgument.Equals("--version", StringComparison.OrdinalIgnoreCase))
    {
        if (configuration.Settings.ApplicationVersion != null)
        {
            // We need to check if the command has a version option on its setting class.
            // Do this by first parsing the command line args and checking the remaining args.
            try
            {
                parsedResult = ParseCommandLineArguments(model, configuration.Settings, arguments);
            }
            catch (Exception)
            {
                // Something went wrong with parsing, but we know --version was asked for.
                var console = configuration.Settings.Console.GetConsole();
                console.MarkupLine(configuration.Settings.ApplicationVersion);
                return 0;
            }

            // Check the parsed remaining args for the version options.
            if ((firstArgument.Equals("-v", ...) && parsedResult.Remaining.Parsed.Contains("-v")) ||
                (firstArgument.Equals("--version", ...) && parsedResult.Remaining.Parsed.Contains("--version")))
            {
                var console = configuration.Settings.Console.GetConsole();
                console.MarkupLine(configuration.Settings.ApplicationVersion);
                return 0;
            }
        }
    }
}
```

**Key behavior:**
- If `ApplicationVersion` is `null`, `-v`/`--version` is silently ignored (no flag registered).
- The executor uses `console.MarkupLine(...)` — meaning the version string **supports Spectre markup** (e.g. `[green]1.0.0[/]`).
- If the default command defines its own `-v` or `--version` option in its `CommandSettings`, that takes priority (Spectre is smart enough to not steal the flag).
- Returns exit code `0` on success.

### 1e. How `-v|--version` Appears in Help Output

**File:** `src/Spectre.Console.Cli/Help/HelpProvider.cs`

The `HelpProvider` automatically adds the `-v|--version` option row when:
1. `model.ApplicationVersion != null` (i.e., you called `SetApplicationVersion`), AND
2. You are at the root command level (no parent), AND
3. The default command does not define its own `-v` or `--version` option.

```csharp
if ((command?.Parent == null) && !(command?.IsBranch ?? false) && (command?.IsDefaultCommand ?? true))
{
    // Check whether the default command has a version option in its settings.
    var versionCommandOption = command?.Parameters?.OfType<CommandOption>()?.FirstOrDefault(o =>
        (o.ShortNames.FirstOrDefault(v => v.Equals("v", ...)) != null) ||
        (o.LongNames.FirstOrDefault(v => v.Equals("version", ...)) != null));

    if (versionCommandOption == null)
    {
        if (model.ApplicationVersion != null)
        {
            parameters.Add(new HelpOption("v", "version", null, null, false,
                resources.PrintVersionDescription, null));
        }
    }
}
```

---

## 2. The `.csproj` `<Version>` → `AssemblyInformationalVersionAttribute` Flow

The .NET SDK automatically handles this entire pipeline — **you do not write an `AssemblyInfo.cs`**.

### The MSBuild Property Chain

```
.csproj <Version>
    ↓  (if <InformationalVersion> is not set, Version becomes InformationalVersion)
<InformationalVersion>
    ↓  (SDK auto-generates obj/[proj].AssemblyInfo.cs)
[assembly: AssemblyInformationalVersion("1.2.3")]
    ↓  (compiled into assembly metadata)
Assembly.GetEntryAssembly()
    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
    .InformationalVersion
    ↓
VersionHelper.GetVersion()   (Spectre's internal helper)
    ↓
configurator.Settings.ApplicationVersion
    ↓
printed by console.MarkupLine() when --version is passed
```

### Key MSBuild Properties

| Property | Attribute Set | Purpose |
|---|---|---|
| `<Version>` | `AssemblyInformationalVersion`, `AssemblyVersion`, `AssemblyFileVersion` | **Master version.** Drives all three attributes if not individually overridden. |
| `<InformationalVersion>` | `AssemblyInformationalVersionAttribute` | Overrides only the informational version. Can be any string (SemVer, pre-release, etc.) |
| `<AssemblyVersion>` | `AssemblyVersion` | CLR binding version. Usually set to `Major.0.0.0`. |
| `<VersionSuffix>` | Appended to `<Version>` | Used to build pre-release strings: `1.0.0-$(VersionSuffix)` |

### Source Link Behavior in .NET 8/9

Starting with .NET 8, when Source Link is present, the SDK **appends the commit SHA** to `InformationalVersion`:

```
1.2.3+abc1234def5678  ← InformationalVersion becomes this automatically
```

This is controlled by `<IncludeSourceRevisionInInformationalVersion>` (default `true`). Set it to `false` to suppress the SHA suffix.

### Practical `.csproj` Setup

```xml
<PropertyGroup>
  <Version>1.2.3</Version>
  <!-- Optional: suppress git commit hash appended to InformationalVersion -->
  <!-- <IncludeSourceRevisionInInformationalVersion>false</IncludeSourceRevisionInInformationalVersion> -->
</PropertyGroup>
```

This produces (automatically, in `obj/YourProject.AssemblyInfo.cs`):

```csharp
[assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.2.3")]
[assembly: System.Reflection.AssemblyVersionAttribute("1.2.3")]
[assembly: System.Reflection.AssemblyFileVersionAttribute("1.2.3")]
```

---

## 3. `Assembly.GetName().Version` vs `AssemblyInformationalVersionAttribute` — Which to Use?

**Use `AssemblyInformationalVersionAttribute`. Always.**

### Comparison

| | `Assembly.GetName().Version` | `AssemblyInformationalVersionAttribute` |
|---|---|---|
| Type | `System.Version` (4-part numeric: `1.2.3.0`) | `string` (any format) |
| Supports SemVer | ❌ No pre-release suffixes | ✅ Yes (`1.2.3-beta.1`) |
| Supports build metadata | ❌ No | ✅ Yes (`1.2.3+sha.abc123`) |
| Set by `<Version>` | ✅ Yes | ✅ Yes |
| Can express `1.0.0-rc.2` | ❌ No | ✅ Yes |
| Used by Spectre.Console internally | ❌ No | ✅ Yes (`VersionHelper.GetVersion`) |

### Spectre's Internal `VersionHelper`

**File:** `src/Spectre.Console.Cli/Internal/VersionHelper.cs`

```csharp
internal static class VersionHelper
{
    public static string GetVersion(Assembly? assembly)
    {
        return assembly?
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? "?";
    }
}
```

This is exactly what `UseAssemblyInformationalVersion()` calls. It reads `AssemblyInformationalVersionAttribute` from `Assembly.GetEntryAssembly()`.

**Rule of thumb:** `AssemblyName.Version` is a `System.Version` object limited to `Major.Minor.Build.Revision` four-integer format. `AssemblyInformationalVersionAttribute.InformationalVersion` is a free-form `string` that supports full SemVer 2.0, pre-release tags, and build metadata. For CLIs, always use the informational version.

---

## 4. How Real Open-Source .NET CLIs Use Spectre.Console for Version Info

### Pattern A: `UseAssemblyInformationalVersion()` (Cleanest)

**Aaru Data Preservation Suite** ([aaru-dps/Aaru](https://github.com/aaru-dps/Aaru))  
File: `Aaru/Main.cs` (line ~299)

```csharp
var app = new CommandApp();

app.Configure(static config =>
{
    config.PropagateExceptions();
    config.UseAssemblyInformationalVersion();
    // ... branches and commands ...
});
```

This is the most concise possible form — one line delegates everything to the framework.

---

### Pattern B: `SetApplicationVersion(...)` with Manual Assembly Reflection

**busly-cli** ([TraGicCode/busly-cli](https://github.com/TraGicCode/busly-cli))  
File: `src/BuslyCLI.Console/Spectre/AppConfiguration.cs`

```csharp
using System.Reflection;

public static Action<IConfigurator> GetSpectreCommandConfiguration()
{
    return config =>
    {
        config.SetApplicationName("busly");
        var assembly = Assembly.GetExecutingAssembly()
                               .GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        config.SetApplicationVersion(assembly.InformationalVersion);
        // ...
    };
}
```

> ⚠️ **Note:** This uses `GetExecutingAssembly()` — which works when the configuration code is in the same assembly as the entry point. When it's in a separate project (e.g. a class library), use `Assembly.GetEntryAssembly()` instead, or use `UseAssemblyInformationalVersion()` which always uses `GetEntryAssembly()`.

---

### Pattern C: `SetApplicationVersion(...)` with Build-Time Metadata (GitVersion)

**recyclarr** ([recyclarr/recyclarr](https://github.com/recyclarr/recyclarr))  
File: `src/Recyclarr.Cli/Console/CliSetup.cs`

```csharp
config.SetApplicationName("recyclarr");
config.SetApplicationVersion(
    $"v{GitVersionInformation.SemVer} ({GitVersionInformation.FullBuildMetaData})"
);
```

This pattern uses [GitVersion](https://gitversion.net/) to inject version info at build time into a `GitVersionInformation` static class. The version string includes full build metadata in parentheses.

---

## 5. How Popular Open-Source CLIs Handle the `--version` Pattern

### The Spectre.Console.Cli Pattern (Recommended for .NET)

The two-step approach all Spectre-based CLIs use:

**Step 1:** Set the version in `Program.cs` (or equivalent):
```csharp
var app = new CommandApp();
app.Configure(config =>
{
    config.SetApplicationVersion("1.0.0");
    // OR: config.UseAssemblyInformationalVersion();
});
return await app.RunAsync(args);
```

**Step 2:** Framework handles everything: `-v`, `--version`, help display, exit code 0.

### The `CommandApp<TDefaultCommand>` Variant

When using a default command, the pattern is the same:

```csharp
var app = new CommandApp<MyDefaultCommand>();
app.Configure(config =>
{
    config.UseAssemblyInformationalVersion();
    config.SetApplicationName("mytool");
});
return await app.RunAsync(args);
```

**Important edge case:** If `MyDefaultCommand`'s `CommandSettings` class defines `[CommandOption("-v|--version")]`, Spectre will **not** intercept `--version` and will route it to your command instead. This is intentional and documented in `HelpProvider.cs`.

---

## Complete Working Example for This Project

### 1. `.csproj` Configuration

```xml
<PropertyGroup>
  <OutputType>Exe</OutputType>
  <TargetFramework>net9.0</TargetFramework>
  <AssemblyName>journal</AssemblyName>
  <RootNamespace>MarkdownJournalCli</RootNamespace>

  <!-- Single source of truth for version -->
  <Version>1.0.0</Version>

  <!-- Optional: strip +gitsha suffix from --version output -->
  <!-- <IncludeSourceRevisionInInformationalVersion>false</IncludeSourceRevisionInInformationalVersion> -->
</PropertyGroup>
```

### 2. `Program.cs` — Minimal Wiring

```csharp
using System.Reflection;
using Spectre.Console.Cli;

var app = new CommandApp();

app.Configure(config =>
{
    // Reads AssemblyInformationalVersionAttribute from entry assembly
    // Activates -v|--version flag automatically
    config.UseAssemblyInformationalVersion();

    config.SetApplicationName("journal");

    config.AddCommand<NewEntryCommand>("new")
        .WithDescription("Create a new journal entry.");

    config.AddCommand<ListCommand>("list")
        .WithAlias("ls")
        .WithDescription("List journal entries.");

#if DEBUG
    config.PropagateExceptions();
    config.ValidateExamples();
#endif
});

return await app.RunAsync(args);
```

### 3. Manual Alternative (if you need to format the version string)

```csharp
var version = Assembly.GetEntryAssembly()
    ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
    ?.InformationalVersion
    ?? "unknown";

app.Configure(config =>
{
    config.SetApplicationVersion(version);   // or $"v{version}"
    config.SetApplicationName("journal");
    // ...
});
```

### 4. Result

```
$ journal --version
1.0.0

$ journal -v
1.0.0

$ journal --help
USAGE:
    journal [COMMAND] [OPTIONS]

OPTIONS:
    -h, --help     Prints help information
    -v, --version  Prints version information   ← appears automatically

COMMANDS:
    new   Create a new journal entry
    list  List journal entries
```

---

## API Quick Reference

| Method / Property | Location | Purpose |
|---|---|---|
| `config.SetApplicationVersion(string)` | `ConfiguratorExtensions.cs` | Set version to any string literal or computed value |
| `config.UseAssemblyInformationalVersion()` | `ConfiguratorExtensions.cs` | Auto-reads `AssemblyInformationalVersionAttribute` from entry assembly |
| `config.Settings.ApplicationVersion` | `ICommandAppSettings.cs` | Direct property access (same effect as `SetApplicationVersion`) |
| `VersionHelper.GetVersion(Assembly?)` | `Internal/VersionHelper.cs` | Internal helper; reads `AssemblyInformationalVersionAttribute` |
| `<Version>1.0.0</Version>` in `.csproj` | MSBuild SDK | Sets all three assembly version attributes automatically |

---

## Architecture: `--version` Data Flow

```
.csproj <Version>
    │
    ▼ (MSBuild SDK generates)
obj/YourProject.AssemblyInfo.cs
    [assembly: AssemblyInformationalVersion("1.0.0")]
    │
    ▼ (compiled into binary)
Entry Assembly metadata
    │
    ▼ (at startup, in Configure block)
UseAssemblyInformationalVersion()
    └─► VersionHelper.GetVersion(Assembly.GetEntryAssembly())
            └─► assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                    └─► .InformationalVersion → "1.0.0"
    │
    ▼ (stored in)
CommandAppSettings.ApplicationVersion = "1.0.0"
    │
    ▼ (at runtime, when user passes --version)
CommandExecutor.ExecuteAsync()
    ├─► checks firstArgument == "-v" or "--version"
    ├─► checks ApplicationVersion != null
    ├─► console.MarkupLine(ApplicationVersion)  ← supports Spectre markup
    └─► return 0
```

---

## Confidence Assessment

| Finding | Confidence | Source |
|---|---|---|
| `SetApplicationVersion` and `UseAssemblyInformationalVersion` are extension methods on `IConfigurator` | ✅ Verified | `ConfiguratorExtensions.cs` direct source read |
| `ApplicationVersion` is a `string?` property on `ICommandAppSettings` | ✅ Verified | `ICommandAppSettings.cs` + `CommandAppSettings.cs` direct source read |
| The executor intercepts `-v`/`--version` as first argument only | ✅ Verified | `CommandExecutor.cs` direct source read |
| Version string supports Spectre markup via `MarkupLine` | ✅ Verified | `CommandExecutor.cs` direct source read |
| `UseAssemblyInformationalVersion` calls `Assembly.GetEntryAssembly()` | ✅ Verified | `ConfiguratorExtensions.cs` direct source read |
| `<Version>` in `.csproj` flows to `AssemblyInformationalVersionAttribute` | ✅ Verified | Microsoft docs + SDK behavior |
| `AssemblyInformationalVersionAttribute` is preferred over `Assembly.GetName().Version` | ✅ Verified | Spectre source, MS library guidance docs |
| recyclarr, busly-cli, Aaru use this pattern in production | ✅ Verified | Direct source file reads from their repos |
| The main branch of `spectreconsole/spectre.console` has moved Cli into the main package | ✅ Observed | Directory listing showed no separate `Spectre.Console.Cli` folder in main branch; CLI code was fetched from `0.53.1-hotfix` branch and the new [spectreconsole/spectre.console.cli](https://github.com/spectreconsole/spectre.console.cli) repo |

---

## Footnotes

[^1]: `src/Spectre.Console.Cli/ConfiguratorExtensions.cs` — `SetApplicationVersion` extension method, [spectreconsole/spectre.console @ 0.53.1-hotfix](https://github.com/spectreconsole/spectre.console/blob/0.53.1-hotfix/src/Spectre.Console.Cli/ConfiguratorExtensions.cs)

[^2]: `src/Spectre.Console.Cli/ConfiguratorExtensions.cs` — `UseAssemblyInformationalVersion` extension method, same file as [^1]

[^3]: `src/Spectre.Console.Cli/ICommandAppSettings.cs` — `ApplicationVersion` property declaration, [spectreconsole/spectre.console @ 0.53.1-hotfix](https://github.com/spectreconsole/spectre.console/blob/0.53.1-hotfix/src/Spectre.Console.Cli/ICommandAppSettings.cs)

[^4]: `src/Spectre.Console.Cli/Internal/Configuration/CommandAppSettings.cs` — `ApplicationVersion` concrete property, [spectreconsole/spectre.console @ 0.53.1-hotfix](https://github.com/spectreconsole/spectre.console/blob/0.53.1-hotfix/src/Spectre.Console.Cli/Internal/Configuration/CommandAppSettings.cs)

[^5]: `src/Spectre.Console.Cli/Internal/CommandExecutor.cs` — `-v`/`--version` interception logic, [spectreconsole/spectre.console @ 0.53.1-hotfix](https://github.com/spectreconsole/spectre.console/blob/0.53.1-hotfix/src/Spectre.Console.Cli/Internal/CommandExecutor.cs)

[^6]: `src/Spectre.Console.Cli/Help/HelpProvider.cs` — conditional `-v|--version` help entry logic, [spectreconsole/spectre.console @ 0.53.1-hotfix](https://github.com/spectreconsole/spectre.console/blob/0.53.1-hotfix/src/Spectre.Console.Cli/Help/HelpProvider.cs)

[^7]: `src/Spectre.Console.Cli/Internal/VersionHelper.cs` — `GetVersion` reads `AssemblyInformationalVersionAttribute`, [spectreconsole/spectre.console @ 0.53.1-hotfix](https://github.com/spectreconsole/spectre.console/blob/0.53.1-hotfix/src/Spectre.Console.Cli/Internal/VersionHelper.cs)

[^8]: `src/Spectre.Console.Cli/CommandApp.cs` — `CommandApp` entry point with `VersionCommand` wired as hidden built-in, [spectreconsole/spectre.console @ 0.53.1-hotfix](https://github.com/spectreconsole/spectre.console/blob/0.53.1-hotfix/src/Spectre.Console.Cli/CommandApp.cs)

[^9]: `src/Recyclarr.Cli/Console/CliSetup.cs` — `SetApplicationVersion` with GitVersion, [recyclarr/recyclarr](https://github.com/recyclarr/recyclarr/blob/56bf07bf0d511269abf23d5dcd7280fc879d49f4/src/Recyclarr.Cli/Console/CliSetup.cs)

[^10]: `src/BuslyCLI.Console/Spectre/AppConfiguration.cs` — manual `GetExecutingAssembly()` + `SetApplicationVersion`, [TraGicCode/busly-cli](https://github.com/TraGicCode/busly-cli/blob/0c2d8cc91b2632ea59bbd325f19ec080342845fe/src/BuslyCLI.Console/Spectre/AppConfiguration.cs)

[^11]: `Aaru/Main.cs` — `UseAssemblyInformationalVersion()` pattern, [aaru-dps/Aaru](https://github.com/aaru-dps/Aaru/blob/db1cf10eff3f0d68ff557fc6df9e2e4d1b18118b/Aaru/Main.cs)

[^12]: Microsoft Learn — Assembly Informational Version, versioning guidance: [learn.microsoft.com/dotnet/standard/library-guidance/versioning](https://learn.microsoft.com/en-us/dotnet/standard/library-guidance/versioning)

[^13]: Microsoft Learn — `IncludeSourceRevisionInInformationalVersion` MSBuild property: [learn.microsoft.com/dotnet/core/project-sdk/msbuild-props](https://learn.microsoft.com/en-us/dotnet/core/project-sdk/msbuild-props#includesourcerevisionininformationalversion)

[^14]: Spectre.Console official docs — Configuring CommandApp: [spectreconsole.net/cli/how-to/configuring-commandapp-and-commands](https://spectreconsole.net/cli/how-to/configuring-commandapp-and-commands)
