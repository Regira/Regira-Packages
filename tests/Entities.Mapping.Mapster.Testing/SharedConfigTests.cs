using Mapster;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.DependencyInjection.Extensions;
using Regira.Entities.Mapping.Abstractions;
using Regira.Entities.Mapping.Mapster;
using Regira.Entities.Models.Abstractions;

namespace Entities.Mapping.Mapster.Testing;

/// <summary>
/// Two <c>UseEntities&lt;TContext&gt;()</c> stacks each calling <c>UseMapsterMapping()</c> must share one
/// TypeAdapterConfig, so both contexts' entity→DTO mappings work regardless of registration order — the
/// previous last-wins registration silently dropped the earlier stack's mappings.
/// </summary>
[TestFixture]
public class SharedConfigTests
{
    public class Alpha : IEntity<int> { public int Id { get; set; } public string? Name { get; set; } }
    public class Beta : IEntity<int> { public int Id { get; set; } public string? Label { get; set; } }
    public class AlphaDto { public int Id { get; set; } public string? Name { get; set; } }
    public class AlphaInputDto { public string? Name { get; set; } }
    public class BetaDto { public int Id { get; set; } public string? Label { get; set; } }
    public class BetaInputDto { public string? Label { get; set; } }

    public class AlphaContext(DbContextOptions<AlphaContext> options) : DbContext(options)
    {
        public DbSet<Alpha> Alphas => Set<Alpha>();
    }
    public class BetaContext(DbContextOptions<BetaContext> options) : DbContext(options)
    {
        public DbSet<Beta> Betas => Set<Beta>();
    }

    private static IEntityMapper BuildMapper(bool alphaFirst)
    {
        var services = new ServiceCollection();
        services.AddDbContext<AlphaContext>(o => o.UseSqlite("Filename=:memory:"));
        services.AddDbContext<BetaContext>(o => o.UseSqlite("Filename=:memory:"));

        void RegisterAlpha() => services.UseEntities<AlphaContext>(o => o.UseMapsterMapping())
            .For<Alpha>(e => e.UseMapping<AlphaDto, AlphaInputDto>());
        void RegisterBeta() => services.UseEntities<BetaContext>(o => o.UseMapsterMapping())
            .For<Beta>(e => e.UseMapping<BetaDto, BetaInputDto>());

        if (alphaFirst) { RegisterAlpha(); RegisterBeta(); }
        else { RegisterBeta(); RegisterAlpha(); }

        return services.BuildServiceProvider().GetRequiredService<IEntityMapper>();
    }

    [TestCase(true)]
    [TestCase(false)]
    public void Both_Contexts_Mappings_Work_Regardless_Of_Order(bool alphaFirst)
    {
        var mapper = BuildMapper(alphaFirst);

        var alphaDto = mapper.Map<AlphaDto>(new Alpha { Id = 1, Name = "a" });
        var betaDto = mapper.Map<BetaDto>(new Beta { Id = 2, Label = "b" });

        Assert.Multiple(() =>
        {
            Assert.That(alphaDto.Id, Is.EqualTo(1));
            Assert.That(alphaDto.Name, Is.EqualTo("a"));
            Assert.That(betaDto.Id, Is.EqualTo(2));
            Assert.That(betaDto.Label, Is.EqualTo("b"));
        });
    }

    [Test]
    public void Single_Context_Mapping_Still_Works()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AlphaContext>(o => o.UseSqlite("Filename=:memory:"));
        services.UseEntities<AlphaContext>(o => o.UseMapsterMapping())
            .For<Alpha>(e => e.UseMapping<AlphaDto, AlphaInputDto>());

        var mapper = services.BuildServiceProvider().GetRequiredService<IEntityMapper>();
        var dto = mapper.Map<AlphaDto>(new Alpha { Id = 5, Name = "solo" });

        Assert.That(dto.Name, Is.EqualTo("solo"));
    }

    // Every UseMapsterMapping(configure) delegate must run eagerly at registration time, in call order —
    // the first call used to defer into the singleton factory and therefore ran LAST, clobbering later
    // stacks' settings.
    [Test]
    public void Configure_Delegates_Apply_In_Call_Order_Last_Wins()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AlphaContext>(o => o.UseSqlite("Filename=:memory:"));
        services.AddDbContext<BetaContext>(o => o.UseSqlite("Filename=:memory:"));

        services.UseEntities<AlphaContext>(o => o.UseMapsterMapping(cfg => cfg.Default.PreserveReference(true)))
            .For<Alpha>(e => e.UseMapping<AlphaDto, AlphaInputDto>());
        services.UseEntities<BetaContext>(o => o.UseMapsterMapping(cfg => cfg.Default.PreserveReference(false)))
            .For<Beta>(e => e.UseMapping<BetaDto, BetaInputDto>());

        var config = services.BuildServiceProvider().GetRequiredService<global::Mapster.TypeAdapterConfig>();

        // pre-fix, the FIRST delegate deferred into the singleton factory and ran last → true
        Assert.That(config.Default.Settings.PreserveReference, Is.False, "the LAST configure delegate must win");
    }

    [Test]
    public void First_Calls_PerType_Config_Survives_A_Later_Stack()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AlphaContext>(o => o.UseSqlite("Filename=:memory:"));
        services.AddDbContext<BetaContext>(o => o.UseSqlite("Filename=:memory:"));

        services.UseEntities<AlphaContext>(o => o.UseMapsterMapping(cfg =>
                cfg.NewConfig<Alpha, AlphaDto>().Map(d => d.Name, s => s.Name + "!")))
            .For<Alpha>(e => e.UseMapping<AlphaDto, AlphaInputDto>());
        services.UseEntities<BetaContext>(o => o.UseMapsterMapping())
            .For<Beta>(e => e.UseMapping<BetaDto, BetaInputDto>());

        var mapper = services.BuildServiceProvider().GetRequiredService<IEntityMapper>();
        var dto = mapper.Map<AlphaDto>(new Alpha { Id = 1, Name = "a" });

        Assert.That(dto.Name, Is.EqualTo("a!"), "the first call's per-type tweak must not be lost or clobbered");
    }
}
