using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Web.Security.Testing.Infrastructure.Identity;

public class TestDbContext(DbContextOptions<TestDbContext> options) : IdentityDbContext<TestUser>(options);
