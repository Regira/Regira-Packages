# Office.VCards — Example: Contact Directory Export

> Context: A CRM lets users download a single contact or their entire contact list as a `.vcf` file.

## Export a single contact

```csharp
public async Task<string> ExportContact(Contact contact)
{
    var manager = new VCardManager();

    var vCard = new VCard
    {
        Name = new VCardName { SurName = contact.LastName, GivenName = contact.FirstName },
        Emails = [new VCardEmail { Text = contact.Email }],
        Tels = [new VCardTel { Uri = contact.Phone }]
    };

    return await manager.Write(vCard, VCardVersion.V3_0);
}
```

## Export multiple contacts

```csharp
var contacts = _contactService.List();
var manager  = new VCardManager();

var cards = contacts.Select(c => new VCard
{
    Name = new VCardName { SurName = c.LastName, GivenName = c.FirstName },
    Emails = [new VCardEmail { Text = c.Email }]
});

string vcf = await manager.Write(cards, VCardVersion.V3_0);
```

## Import contacts from an uploaded .vcf file

```csharp
using var reader = new StreamReader(file.OpenReadStream());
string vcfContent = await reader.ReadToEndAsync();

var manager = new VCardManager();
return await manager.ReadMany(vcfContent);
```
