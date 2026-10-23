using Microsoft.Extensions.DependencyInjection;
using Office.Clients.Testing.Abstractions;
using Regira.Office.VCards.Abstractions;
using Regira.Office.VCards.Exceptions;
using Regira.Office.VCards.Models;

namespace Office.Clients.Testing;

[TestFixture]
[Parallelizable(ParallelScope.Self)]
[Category("Network")]
public class VCardClientTests : OfficeClientTestsBase
{
    private IVCardService _service = null!;

    [SetUp]
    public void Setup()
    {
        _service = Services.GetRequiredService<IVCardService>();
    }

    [TestCase(VCardVersion.V3_0)]
    [TestCase(VCardVersion.V4_0)]
    public async Task Write_And_Read_RoundTrip(VCardVersion version)
    {
        var card = new VCard
        {
            Name = new VCardName { GivenName = "Jane", SurName = "Doe" },
            FormattedName = "Jane Doe",
            Organization = "Example Ltd",
            Tels = [new VCardTel { Type = VCardTelType.Cell | VCardTelType.Work, Uri = "+32 470 00 00 00" }],
            Emails = [new VCardEmail { Type = VCardPropertyType.Work, Text = "jane.doe@example.com" }],
            Addresses =
            [
                new VCardAddress
                {
                    Type = VCardPropertyType.Work,
                    StreetAndNumber = "Stationsstraat 1",
                    PostalCode = "9000",
                    Locality = "Gent",
                    Country = "Belgium"
                }
            ]
        };

        var vcf = await _service.Write(card, version);
        var result = await _service.Read(vcf);

        Assert.That(vcf, Does.StartWith("BEGIN:VCARD"));
        Assert.That(vcf, Does.Contain(version == VCardVersion.V4_0 ? "VERSION:4.0" : "VERSION:3.0"));
        Assert.That(result.FormattedName, Is.EqualTo("Jane Doe"));
        Assert.That(result.Name?.GivenName, Is.EqualTo("Jane"));
        Assert.That(result.Name?.SurName, Is.EqualTo("Doe"));
        Assert.That(result.Organization?.Name, Is.EqualTo("Example Ltd"));
        var tel = result.Tels!.Single();
        Assert.That(tel.Uri, Does.Contain("+32 470 00 00 00"));
        Assert.That(tel.Type, Is.EqualTo(VCardTelType.Cell | VCardTelType.Work));
        var email = result.Emails!.Single();
        Assert.That(email.Text, Is.EqualTo("jane.doe@example.com"));
        // FolkerKinzel writes a 3.0 e-mail as TYPE=INTERNET, without its WORK or HOME
        Assert.That(email.Type, Is.EqualTo(version == VCardVersion.V4_0 ? VCardPropertyType.Work : null));
        var address = result.Addresses!.Single();
        Assert.That(address.StreetAndNumber, Is.EqualTo("Stationsstraat 1"));
        Assert.That(address.PostalCode, Is.EqualTo("9000"));
        Assert.That(address.Locality, Is.EqualTo("Gent"));
        Assert.That(address.Country, Is.EqualTo("Belgium"));
    }

    [Test]
    public async Task ReadMany_Reads_Every_Contact()
    {
        const string vcf = """
            BEGIN:VCARD
            VERSION:3.0
            N:Doe;Jane;;;
            FN:Jane Doe
            TEL;TYPE=CELL:+32 470 00 00 00
            EMAIL;TYPE=INTERNET:jane.doe@example.com
            ADR;TYPE=WORK:;;Stationsstraat 1;Gent;;9000;Belgium
            END:VCARD
            BEGIN:VCARD
            VERSION:3.0
            N:Smith;John;;;
            FN:John Smith
            TEL;TYPE=HOME:+32 9 000 00 00
            EMAIL;TYPE=INTERNET:john.smith@example.com
            ADR;TYPE=HOME:;;Kerkstraat 2;Brugge;;8000;Belgium
            END:VCARD
            """;

        var result = (await _service.ReadMany(vcf)).ToList();

        Assert.That(result.Select(x => x.FormattedName), Is.EqualTo(new[] { "Jane Doe", "John Smith" }));
        Assert.That(result[0].Tels!.Single().Uri, Does.Contain("+32 470 00 00 00"));
        Assert.That(result[0].Emails!.Single().Text, Is.EqualTo("jane.doe@example.com"));
        Assert.That(result[0].Addresses!.Single().Locality, Is.EqualTo("Gent"));
        Assert.That(result[1].Name?.SurName, Is.EqualTo("Smith"));
        Assert.That(result[1].Tels!.Single().Uri, Does.Contain("+32 9 000 00 00"));
        Assert.That(result[1].Emails!.Single().Text, Is.EqualTo("john.smith@example.com"));
        Assert.That(result[1].Addresses!.Single().Locality, Is.EqualTo("Brugge"));
    }

    [Test]
    public async Task Read_Invalid_Content_Throws()
    {
        const string content = "This is not a vCard.";

        var ex = await Assert.ThrowsAsync<InvalidCardException>(() => _service.Read(content));

        Assert.That(ex!.CardContent, Is.EqualTo(content));
    }
}
