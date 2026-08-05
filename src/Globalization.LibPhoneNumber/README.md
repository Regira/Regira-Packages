# Regira Globalization

Regira Globalization extends the phone number formatting and country utilities built into [Common](https://regira.github.io/Regira-Packages/src/Common).

## Projects

| Project | Package | Backend |
|---------|---------|---------|
| `Globalization.LibPhoneNumber` | `Regira.Globalization.LibPhoneNumber` | libphonenumber-csharp |

## Installation

```xml
<PackageReference Include="Regira.Globalization.LibPhoneNumber" Version="6.*" />
```

## PhoneNumberFormatter

Implements both `INormalizer` and `IFormatter`. Note that both interfaces declare only `Normalize` — `Format` is a method of the `PhoneNumberFormatter` class itself, so call it through a `PhoneNumberFormatter` reference.

```csharp
// Use the system culture to infer the default country code
var fmt = new PhoneNumberFormatter();

string? e164  = fmt.Normalize("+32 471 12 34 56");   // "+32471123456"
string? intl  = fmt.Format("+32 471 12 34 56");       // "+32 471 12 34 56"

// Supply a specific culture for regional number resolution
var be = new PhoneNumberFormatter(new CultureInfo("nl-BE"));
string? local = be.Normalize("0471 12 34 56");        // "+32471123456"
```

| Method | Output format |
|--------|---------------|
| `Normalize` | E.164 (e.g. `+32471123456`) — suitable for storage |
| `Format` | International display (e.g. `+32 471 12 34 56`) |

`null` or whitespace input is returned unchanged. Input that cannot be parsed as a phone number throws a `PhoneNumbers.NumberParseException` — wrap calls in a try/catch when the input is untrusted.

## Country Utilities (Common)

`CountryUtility` and the `Country` model are in `Regira.Common`:

```csharp
IEnumerable<Country> countries = CountryUtility.GetCountries();
Country? be   = CountryUtility.GetCountry("BE");    // by ISO 2-letter code
string name   = be!.GetName("fr");                  // "Belgique"

// Search by localized name
Country? found = CountryUtility.GetCountries()
    .FirstOrDefault(c => c.GetName("fr") == "Belgique");
```
