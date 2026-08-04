namespace Regira.Globalization.Models;

public record Language
{
    /// <summary>2-letter ISO 639-1 code</summary>
    public string Iso2Code { get; set; } = null!;
    /// <summary>3-letter ISO 639-2 code</summary>
    public string Iso3Code { get; set; } = null!;
    /// <summary>Name in English</summary>
    public string Title { get; set; } = null!;
    public string NativeName { get; set; } = null!;
}
