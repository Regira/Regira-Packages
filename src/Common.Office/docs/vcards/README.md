# Regira VCards

Regira VCards provides reading and writing of vCard contact files in versions 2.1, 3.0, and 4.0.

## Projects

| Project | Package | Backend |
|---------|---------|---------|
| `Common.Office` | *(transitive)* | Shared abstractions and models |
| `VCards.FolkerKinzel` | `Regira.Office.VCards.FolkerKinzel` | FolkerKinzel.VCards |

## Installation

```xml
<PackageReference Include="Regira.Office.VCards.FolkerKinzel" Version="6.*" />
```

## VCardManager

Implements `IVCardService`.

```csharp
var manager = new VCardManager();
```

### Read

```csharp
var manager = new VCardManager();
string vcfContent = await File.ReadAllTextAsync("contacts.vcf");

// Single vCard from a .vcf string
VCard contact = await manager.Read(vcfContent);

// Multiple vCards from a single .vcf file (multiple VCARD blocks)
IEnumerable<VCard> contacts = await manager.ReadMany(vcfContent);
```

### Write

```csharp
var manager = new VCardManager();
VCard contact = new() { FormattedName = "Alice Smith" };
VCard[] contacts = [contact];

// Single contact
string vcf = await manager.Write(contact);

// Single contact, explicit version
string vcf4 = await manager.Write(contact, VCardVersion.V4_0);

// Multiple contacts into one .vcf string
string vcfAll = await manager.Write(contacts, VCardVersion.V3_0);
```

### VCardVersion

```
V2_1   V3_0 (default)   V4_0
```

## Notes

- The `VCard` type is `Regira.Office.VCards.Models.VCard`, a backend-agnostic model with properties such as `Name` (`VCardName`), `FormattedName`, `Emails` (`ICollection<VCardEmail>`), `Tels` (`ICollection<VCardTel>`), `Organization`, `Photo`, `Addresses`, `BirthDay`, `Gender`, and `Homepage`. `VCardManager` converts to and from the FolkerKinzel representation internally.
- Version 3.0 is the most widely compatible and is used as the default.
