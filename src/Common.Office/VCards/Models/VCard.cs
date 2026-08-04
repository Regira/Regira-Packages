using Regira.Office.VCards.Serializing;

namespace Regira.Office.VCards.Models;

public class VCard
{
    [VCardProperty("version")]
    public string? Version { get; set; }
    [VCardProperty("n")]
    public VCardName? Name { get; set; } = new();
    [VCardProperty("fn")]
    public string? FormattedName { get; set; }
    [VCardProperty("org")]
    public VCardOrganization? Organization { get; set; } = new();
    [VCardProperty("title")]
    public string? Title { get; set; }
    [VCardProperty("photo")]
    public VPhoto? Photo { get; set; }
    [VCardProperty("tel")]
    public ICollection<VCardTel>? Tels { get; set; } = new List<VCardTel>();
    [VCardProperty("email")]
    public ICollection<VCardEmail>? Emails { get; set; } = new List<VCardEmail>();
    [VCardProperty("adr")]
    public ICollection<VCardAddress>? Addresses { get; set; } = new List<VCardAddress>();
    [VCardProperty("bday/date")]
    public VCardBirthdate? BirthDay { get; set; } = new();
    [VCardProperty("gender")]
    public VCardGender? Gender { get; set; } = new();
    [VCardProperty("a/href")]
    public string? Homepage { get; set; }
}