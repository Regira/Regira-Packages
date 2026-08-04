using Microsoft.AspNetCore.Identity;

namespace Web.Security.Testing.Infrastructure.Identity;

// Concrete IdentityUser (string key) satisfying the TUser : IdentityUser<string>, new() constraints
// of the Security.Authentication.Web base controllers.
public class TestUser : IdentityUser;
