using NUnit.Framework.Legacy;
using Office.VCards.Testing.Samples;
using Regira.Office.VCards.Abstractions;
using Regira.Office.VCards.Exceptions;
using Regira.Office.VCards.Models;
[assembly: Parallelizable(ParallelScope.Fixtures)]

namespace Office.VCards.Testing.Abstractions;

public abstract class VCardsTestsBase(IVCardService manager)
{
    [Test]
    public virtual async Task Can_Read_Empty()
    {
        var content = @"BEGIN:VCARD
VERSION:2.1
END:VCARD";
        var item = await manager.Read(content);
        ClassicAssert.IsNotNull(item);
    }
    [Test]
    public virtual void Read_Invalid_Expect_InvalidCardException()
    {
        var content = "";
        Assert.ThrowsAsync<InvalidCardException>(async () => await manager.Read(content));
    }

    [Test]
    public Task Can_Read_V2_1() => CanRead(ContentSamples.VCards[VCardVersion.V2_1], VCardVersion.V2_1);
    [Test]
    public virtual Task Can_Read_V3_0() => CanRead(ContentSamples.VCards[VCardVersion.V3_0], VCardVersion.V3_0);
    [Test]
    public virtual Task Can_Read_V4_0() => CanRead(ContentSamples.VCards[VCardVersion.V3_0], VCardVersion.V3_0);


    [Test]
    public virtual async Task Write_Empty()
    {
        var content = await manager.Write(new VCard());
        Assert.That(content, Is.Not.Empty);
    }
    [Test]
    public virtual Task Can_Write_V2_1() => CanWrite(CardSamples.BramCard, VCardVersion.V2_1);
    [Test]
    public virtual Task Can_Write_V3_0() => CanWrite(CardSamples.BramCard, VCardVersion.V3_0);
    [Test]
    public virtual Task Can_Write_V4_0() => CanWrite(CardSamples.BramCard, VCardVersion.V4_0);

    public virtual async Task CanRead(string content, VCardVersion version)
    {
        var item = await manager.Read(content);
        ClassicAssert.IsNotNull(item);
        Assert.That(item.Name!.SurName, Is.EqualTo("Gump"));
        Assert.That(item.Name.GivenName, Is.EqualTo("Forrest"));
        Assert.That(item.Name.Prefix, Is.EqualTo("Mr."));
        Assert.That(item.FormattedName, Is.EqualTo("Forrest Gump"));
        switch (version)
        {
            case VCardVersion.V2_1:
            case VCardVersion.V3_0:
                Assert.That(item.Tels!.First().Uri, Is.EqualTo("+1-111-555-1212"));
                Assert.That(item.Tels!.Last().Uri, Is.EqualTo("+1-404-555-1212"));
                break;
            case VCardVersion.V4_0:
                Assert.That(item.Tels!.First().Uri, Is.EqualTo("tel:+1-111-555-1212"));
                Assert.That(item.Tels!.Last().Uri, Is.EqualTo("tel:+1-404-555-1212"));
                break;
        }
        Assert.That((VCardTelType.Cell | VCardTelType.Work) == item.Tels!.First().Type, Is.True);
        Assert.That((VCardTelType.Voice | VCardTelType.Home) == item.Tels!.Last().Type, Is.True);
        Assert.That(item.Emails!.First().Text, Is.EqualTo("forrestgump@example.com"));

        // ReSharper disable PossibleInvalidOperationException
        Assert.That(item.Emails!.First().Type!.Value, Is.EqualTo(VCardPropertyType.Home));
        Assert.That(item.Emails!.Last().Type!.Value, Is.EqualTo(VCardPropertyType.Work));
        // ReSharper restore PossibleInvalidOperationException

        Assert.That(item.Addresses!.First().StreetAndNumber, Is.EqualTo("100 Waters Edge"));
        Assert.That(item.Addresses!.Last().StreetAndNumber, Is.EqualTo("42 Plantation St."));
        Assert.That(item.Addresses!.First().Locality, Is.EqualTo("Baytown"));
        Assert.That(item.Addresses!.Last().Locality, Is.EqualTo("Baytown"));

        Assert.That(item.Organization!.Name, Is.EqualTo("Bubba Gump Shrimp Co."));
    }
    public virtual async Task CanWrite(VCard vCard, VCardVersion version)
    {
        var content = await manager.Write(vCard, version);
        Assert.That(!string.IsNullOrWhiteSpace(content), Is.True);

        var lines = content.Trim().Split(Environment.NewLine);
        Assert.That(lines.First(), Does.StartWith("BEGIN:VCARD"));
        Assert.That(lines.Last(), Does.EndWith("END:VCARD"));

        var nameLine = lines.First(l => l.StartsWith("N:"));
        Assert.That(nameLine, Does.Contain(vCard.Name!.SurName));
        Assert.That(nameLine, Does.Contain(vCard.Name!.GivenName));
        Assert.That(nameLine, Does.Contain(vCard.Name!.Prefix));

        var telLines = lines.Where(l => l.StartsWith("TEL")).ToArray();
        Assert.That(telLines.First().ToUpper(), Does.Contain(vCard.Tels!.First().Uri!.ToUpper()));
        switch (version)
        {
            case VCardVersion.V2_1:
                Assert.That(telLines.Last().ToUpper(), Does.Contain("WORK;VOICE"));
                break;
            case VCardVersion.V3_0:
                Assert.That(telLines.Last().ToUpper(), Does.Contain("WORK,VOICE"));
                break;
            case VCardVersion.V4_0:
                Assert.That(telLines.Last().ToUpper(), Does.Contain("WORK,VOICE,TEXT"));
                break;
        }
        if (version == VCardVersion.V4_0)
        {
            var emailLines = lines.Where(l => l.StartsWith("EMAIL")).ToArray();
            Assert.That(emailLines.Last().ToUpper(), Does.Contain("HOME"));
            Assert.That(emailLines.Last().ToUpper(), Does.Contain("WORK"));
        }

        var addressLines = lines.Where(l => l.StartsWith("ADR")).ToArray();
        Assert.That(addressLines.First(), Does.Contain("Albertlei 12"));
        switch (version)
        {
            case VCardVersion.V2_1:
                Assert.That(addressLines.First().ToUpper(), Does.Contain(";HOME"));
                break;
            case VCardVersion.V3_0:
            case VCardVersion.V4_0:
                Assert.That(addressLines.First().ToUpper(), Does.Contain(";TYPE=HOME"));
                break;
        }

        var orgLines = lines.Where(l => l.StartsWith("ORG"));
        Assert.That(orgLines.First(), Does.Contain("Regira bv"));
    }
}