namespace Regira.Security.Authentication.Jwt.Models;

/// <summary>
/// What a sign-in or a refresh hands back: a short-lived access token, and the refresh token that will renew it.
/// </summary>
/// <param name="AccessToken">The bearer token to send on API calls.</param>
/// <param name="AccessTokenExpiresAt">
/// When it stops working, so a client can refresh ahead of time instead of discovering it on a 401.
/// </param>
/// <param name="RefreshToken">
/// Null when no refresh-token service is registered — sign-in then behaves exactly as it did before refresh tokens
/// existed.
/// </param>
/// <param name="RefreshTokenExpiresAt">When the refresh token itself stops working.</param>
public record TokenPair(string AccessToken, DateTimeOffset AccessTokenExpiresAt, string? RefreshToken = null, DateTimeOffset? RefreshTokenExpiresAt = null);
