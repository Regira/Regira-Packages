# Regira Office.VCards AI Agent Instructions

---

## Module Context

Part of **Regira Office**. For routing and full module overview, see [`office.instructions.md`](./office.instructions.md).

| Namespace | Covers |
|---|---|
| `Regira.Office.VCards` | vCard contact file reading and writing (.vcf) |

---

## Installation

```xml
<PackageReference Include="Regira.Office.VCards.FolkerKinzel" Version="6.*" />
```

---

## `VCardManager`

Implements `IVCardService`. Construct directly — no DI extensions needed.

```csharp
var manager = new VCardManager();
```

---

## Reading

```csharp
// Single vCard from a .vcf string
VCard contact = await manager.Read(vcfContent);

// Multiple vCards from a single .vcf file (multiple VCARD blocks)
IEnumerable<VCard> contacts = await manager.ReadMany(vcfContent);
```

---

## Writing

```csharp
// Single contact (default version: 3.0)
string vcf = await manager.Write(contact);

// Single contact, explicit version
string vcf = await manager.Write(contact, VCardVersion.V4_0);

// Multiple contacts into one .vcf string
string vcf = await manager.Write(contacts, VCardVersion.V3_0);
```

---

## `VCardVersion`

```
V2_1   V3_0 (default)   V4_0
```

Version 3.0 is the most widely compatible.

---

## Notes

- The `VCard` type is `Regira.Office.VCards.Models.VCard`, a backend-agnostic model (`Name` (`VCardName`), `FormattedName`, `Emails`, `Tels`, `Organization`, `Photo`, `Addresses`, `BirthDay`, `Gender`, `Homepage`). `VCardManager` converts to and from the FolkerKinzel representation internally.
- A single `.vcf` file may contain multiple VCARD blocks — use `ReadMany` in that case.

---
