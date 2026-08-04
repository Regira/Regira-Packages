namespace Web.Security.Testing.Infrastructure.Jwt;

public static class JwtUsers
{
    public const string AdminRole = "admin";

    public static IList<JwtUser> Value =>
    [
        new JwtUser {UserId = "TEST_USER_ID", Name = "TestUser", Email = "test-user@example.com", Role = AdminRole},
        new JwtUser {UserId = "TEST_PLAIN_USER_ID", Name = "PlainUser", Email = "plain-user@example.com"}
    ];

    /// <summary>Looked up by name, never by position — adding a user must not silently change what a test asserts.</summary>
    public static JwtUser Admin => Value.Single(user => user.Role == AdminRole);
    public static JwtUser WithoutRole => Value.Single(user => user.Role == null);
}
