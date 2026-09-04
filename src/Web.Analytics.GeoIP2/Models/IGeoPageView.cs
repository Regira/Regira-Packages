namespace Regira.Web.Analytics.GeoIP2.Models;

/// <summary>The columns the geo enricher fills; add it to any page-view entity to opt in.</summary>
public interface IGeoPageView
{
    string? CountryCode { get; set; }
    string? Country { get; set; }
    string? City { get; set; }
}