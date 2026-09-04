using System.ComponentModel.DataAnnotations;
using Regira.Web.Analytics.Models;

namespace Regira.Web.Analytics.GeoIP2.Models;

/// <summary>The default entity plus geo columns, for hosts without an entity of their own.</summary>
public class GeoPageView : PageView, IGeoPageView
{
    [MaxLength(2)]
    public string? CountryCode { get; set; }
    [MaxLength(128)]
    public string? Country { get; set; }
    [MaxLength(128)]
    public string? City { get; set; }
}