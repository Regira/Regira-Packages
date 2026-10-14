using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Regira.Security.Authentication.Jwt.Abstraction;
using Regira.Security.Authentication.Web.Controllers;

namespace Web.Security.Testing.Infrastructure.Identity;

// Concrete controllers over the abstract Security.Authentication.Web base controllers.
// Routes (auth, auth/password, users) and actions are inherited from the bases.

public class TestAccountController(
    ITokenHelper tokenHelper,
    UserManager<TestUser> userManager,
    IUserClaimsPrincipalFactory<TestUser> claimsFactory,
    ILogger<TestAccountController> logger)
    : AccountControllerBase<TestUser>(tokenHelper, userManager, claimsFactory, logger);

public class TestPasswordController(UserManager<TestUser> userManager)
    : PasswordControllerBase<TestUser>(userManager);

public class TestUsersController(UserManager<TestUser> userManager)
    : UserControllerBase<TestUser>(userManager);

// The three bases take any user keyed by a string, not only IdentityUser. Abstract, so MVC never maps it; it only has
// to compile.
public class StringKeyUser : IdentityUser<string>;

public abstract class StringKeyPasswordController(UserManager<StringKeyUser> userManager)
    : PasswordControllerBase<StringKeyUser>(userManager);
