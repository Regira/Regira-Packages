namespace Regira.Web.Analytics;

/// <summary>Closed types for the non-generic UseAnalytics/MapAnalyticsEndpoints; one instance per host.</summary>
internal sealed class AnalyticsRegistration
{
    public required Type ViewType { get; init; }
    public required Type MiddlewareType { get; init; }
    public required Type StoreServiceType { get; init; }
}