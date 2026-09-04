using System.Net;
using Regira.Web.Analytics.GeoIP2.Models;

namespace Regira.Web.Analytics.GeoIP2.Services;

/// <summary>Resolves an address to a location; null when unknown, local, or lookup is disabled.</summary>
public interface IGeoLocationService
{
    GeoLocation? Lookup(IPAddress? ip);
}