using Regira.Office.VCards.Abstractions;
using Regira.Office.VCards.Models;

namespace Office.VCards.Testing.Samples;

public static class CardSamples
{
    public static VCard ForrestCard => new()
    {

    };
    public static VCard BramCard => new()
    {
        Name = new VCardName
        {
            GivenName = "Bram",
            SurName = "Verboven",
            Prefix = "Mr."
        },
        Organization = new VCardOrganization
        {
            Name = "Regira bv"
        },
        Tels = new List<VCardTel>
        {
            "0486XXXXXX",
            new()
            {
                Uri = "0488XYXYXY",
                Type = VCardTelType.Work | VCardTelType.Voice | VCardTelType.Text
            }
        },
        Emails = new List<VCardEmail>()
        {
            "bramverboven@hotmail.com",
            new()
            {
                Text = "bram@regira.com",
                Type = VCardPropertyType.Home | VCardPropertyType.Work
            }
        },
        Addresses = new List<VCardAddress>()
        {
            new()
            {
                Type = VCardPropertyType.Home,
                Country = "BE",
                StreetAndNumber = "Albertlei 12",
                Locality = "Antwerpen",
                PostalCode = "2018"
            }
        }
    };
}