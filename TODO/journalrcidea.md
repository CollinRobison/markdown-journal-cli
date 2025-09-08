# journalrc idea

This document explains the recommended approach for creating and editing a `.journalrc` JSON configuration file in C#, with examples for both Newtonsoft.Json (Json.NET) and System.Text.Json.

---

## Overview

- Use a simple POCO (Plain Old CLR Object) as the JSON data shape (a record or class).
- Use a JSON serialization library to read/write the file. Preferred options:
  - `System.Text.Json` (built-in, high performance)
  - `Newtonsoft.Json` (Json.NET, mature & feature-rich)

---

## Recommended flow

1. Define a POCO that matches the JSON structure you want for `.journalrc`.
2. Create a small service (interface + implementation) that handles Ensure/Load/Save operations.
3. Inject an `IFileSystem` abstraction into the service for testability.
4. Serialize/deserialize with `System.Text.Json` by default; use `Newtonsoft.Json` only if you need features it provides.

---

## Example POCO (JournalConfig)

```csharp
namespace markdown_journal_cli.Infrastructure.Configuration;

using System.Text.Json.Serialization;

public record JournalConfig
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = "My Journal";

    [JsonPropertyName("path")]
    public string Path { get; init; } = ".";

    [JsonPropertyName("date_format")]
    public string DateFormat { get; init; } = "yyyy-MM-dd";

    // Add additional settings as required
}
```

---

## Creating JSON files (serialize)

Steps:
1. Create instances of your POCO with desired values.
2. Serialize to JSON string (with indentation).
3. Write the JSON string to the `.journalrc` file path using `IFileSystem` or `System.IO`.

Newtonsoft.Json (Json.NET):

```csharp
var cfg = new JournalConfig { Name = "My Journal", Path = ".", DateFormat = "yyyy-MM-dd" };
string jsonString = Newtonsoft.Json.JsonConvert.SerializeObject(cfg, Newtonsoft.Json.Formatting.Indented);
System.IO.File.WriteAllText(Path.Combine(directory, ".journalrc"), jsonString);
```

System.Text.Json (recommended):

```csharp
var cfg = new JournalConfig();
var opts = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
string jsonString = System.Text.Json.JsonSerializer.Serialize(cfg, opts);
System.IO.File.WriteAllText(Path.Combine(directory, ".journalrc"), jsonString);
```

---

## Editing JSON files

Steps:
1. Read the file into a string.
2. Deserialize into the POCO.
3. Modify the POCO.
4. Serialize and overwrite the file.

Newtonsoft.Json example:

```csharp
string jsonFromFile = System.IO.File.ReadAllText(Path.Combine(directory, ".journalrc"));
var cfg = Newtonsoft.Json.JsonConvert.DeserializeObject<JournalConfig>(jsonFromFile) ?? new JournalConfig();
// Modify
cfg = cfg with { Path = "./journals" };
string outJson = Newtonsoft.Json.JsonConvert.SerializeObject(cfg, Newtonsoft.Json.Formatting.Indented);
System.IO.File.WriteAllText(Path.Combine(directory, ".journalrc"), outJson);
```

System.Text.Json example:

```csharp
string jsonFromFile = System.IO.File.ReadAllText(Path.Combine(directory, ".journalrc"));
var cfg = System.Text.Json.JsonSerializer.Deserialize<JournalConfig>(jsonFromFile) ?? new JournalConfig();
// Modify
cfg = cfg with { Path = "./journals" };
var opts = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
string outJson = System.Text.Json.JsonSerializer.Serialize(cfg, opts);
System.IO.File.WriteAllText(Path.Combine(directory, ".journalrc"), outJson);
```

---

## Example `.journalrc` (JSON)

```json
{
  "name": "My Journal",
  "path": ".",
  "date_format": "yyyy-MM-dd"
}
```

---

## Implementation guidance (service)

Create an `IJournalConfigService` and `JournalConfigService` that:

- Ensures the target directory exists.
- Creates a default `.journalrc` when missing.
- Loads and saves configuration.

Minimal interface:

```csharp
public interface IJournalConfigService
{
    void EnsureConfigExists(string directory);
    JournalConfig Load(string directory);
    void Save(string directory, JournalConfig config);
}
```

Notes:
- Inject `IFileSystem` into the service rather than calling `System.IO` directly (improves testability).
- Consider adding `IFileSystem.FileExists` and `IFileSystem.WriteFile` helpers if desirable.
- Use `System.Text.Json` for most cases; switch to `Newtonsoft.Json` when you need polymorphic handling, reference loops, or converters not yet supported.

---

## Tasks (suggested)

- [ ] Add `JournalConfig` record in `Infrastructure/Configuration`.
- [ ] Add `IJournalConfigService` and `JournalConfigService` implementations.
- [ ] Register `IJournalConfigService` in `Program.cs`'s DI registrar.
- [ ] Add unit tests for `JournalConfigService` using `TestFileSystem`.
- [ ] Add an example `.journalrc` to the repo (docs or templates).

---

## Tips & best practices

- Always write JSON with indentation for user-editable config files.
- Consider schema/versioning inside `.journalrc` (e.g. add a `schema_version` field) so you can upgrade configs in the future.
- Use nullable properties carefully; prefer defaults to reduce runtime null checks when reading user files.
- For small CLI tools, `System.Text.Json` is sufficient and keeps dependencies minimal.

---

If you want, I can scaffold the POCO and service files and add the DI registration and tests. Let me know and I will implement them.
