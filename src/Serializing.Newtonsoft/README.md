# Regira Serializing

Regira Serializing provides JSON serialisation via the `ISerializer` contract defined in [Common](https://regira.github.io/Regira-Packages/src/Common#serializing).

## Projects

| Project | Package | Backend |
|---------|---------|---------|
| `Serializing.Newtonsoft` | `Regira.Serializing.Newtonsoft` | Newtonsoft.Json |

## Installation

```xml
<PackageReference Include="Regira.Serializing.Newtonsoft" Version="6.*" />
```

## JsonSerializer

Implements `ISerializer`. Registers as a singleton in most consuming projects.

```csharp
ISerializer json = new Regira.Serializing.Newtonsoft.Json.JsonSerializer();

string s    = json.Serialize(myObject);
MyType obj  = json.Deserialize<MyType>(s)!;
object dyn  = json.Deserialize(s, typeof(MyType))!;
```

### Options

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `EnumAsString` | `bool` | `true` | Serialise enums as their name, not integer |
| `BoolAsNumber` | `bool` | `true` | Serialise `bool` as `1`/`0` |
| `IgnoreNullValues` | `bool` | `true` | Omit null properties |
| `WriteIndented` | `bool` | `false` | Pretty-print JSON |

```csharp
ISerializer json = new JsonSerializer(new JsonSerializer.Options
{
    EnumAsString  = true,
    WriteIndented = true
});
```

### Built-in converters

| Converter | Handles | Registered |
|-----------|---------|------------|
| `StringEnumConverter` | enum ↔ name | when `EnumAsString` is `true` |
| `BoolNumberConverter` | `bool` ↔ `0`/`1` | when `BoolAsNumber` is `true` |
| `DateOnlyJsonConverter` | `DateOnly` | always |
| `DateAndTimeConverter` | `DateTime` / `DateTimeOffset` | always |

Independent of converters, the serializer always uses camelCase property naming (contract resolver) and ignores reference loops (`ReferenceLoopHandling.Ignore`).

### DI Registration

```csharp
services.AddSingleton<ISerializer, Regira.Serializing.Newtonsoft.Json.JsonSerializer>();
```
