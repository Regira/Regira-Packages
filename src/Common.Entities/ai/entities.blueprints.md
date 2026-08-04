# Regira Entities — Domain Blueprints

Ready-to-copy **feature slices** proven in the Regira reference applications. Each blueprint composes built-in framework hooks (`For<>()`, `Related()`, global query filters, primers, preppers, normalizers) into a complete domain feature. **None of the types below ship in a NuGet package** — copy the code into your app and adapt the names; only the interfaces/base classes marked with a namespace are framework types.

| Blueprint | Use when |
|---|---|
| [Stakeholders](#stakeholders--parties-contactdata-addresses) | People/organizations with contact data, addresses and typed relations (CRM-style) |
| [EntityLabels](#entitylabels--free-form-labels-on-any-entity) | User-defined key/value tags on arbitrary entities, searchable via `Q` |
| [Multi-tenancy](#multi-tenancy--ihastenantid--global-filter--primer) | Row-level tenant isolation: every query scoped, every write stamped |
| [Recursive entities](#recursive-entities--whole-subtree-filtering-with-mapped-db-functions) | Ancestor/descendant filters and tree endpoints over self-referencing entities — **trees and multi-parent graphs alike** (the worked example is a category with several parents) |
| [Identity users as entities](#identity-users-as-framework-entities) | Manage ASP.NET Identity users through the standard entity pipeline |
| [Virtual entity](#virtual-entity--reference-data-without-a-table) | Read-only reference data (countries, currencies) served like any entity, no table |

Budget note (free tier): each blueprint states what it costs in `.For<>()` slots. Owned children managed via `e.Related()` never cost a slot. **Stakeholders doubles as the standard fix for an over-budget domain** — one role-discriminated party replaces `Customer` + `Supplier` + `Employee`.

Every blueprint is split into `###` parts (*Model*, *Registration*, *DTOs*, *Gotchas*, …) — `get_section_toc(id: "Regira.Entities", section: "blueprints")` lists them, and `get_package(..., section: "blueprints", heading: "<part>")` fetches one for a fraction of the whole slice.

---

## Stakeholders — Parties, ContactData, Addresses

One aggregate models every person/organization the app deals with (customers, suppliers, employees), instead of a table per role. Three ideas carry the blueprint:

1. **TPH party hierarchy** — abstract `Party` base, `Person`/`Organization` leaves, single table with a string discriminator.
2. **Owned child concerns** — contact data, addresses and relations are collections on the party, synced with nested `e.Related()`; they have no own `.For<>()`, controller, or budget slot.
3. **Typed self-relations** — parties link to parties through a join entity carrying a `RelationshipType` (employee-of, subsidiary-of, …).

**Budget:** 1 complex slot (`Party` — the TPH base counts once, however many leaf types) + 1 simple slot (`RelationshipType`).

### Model

```csharp no-compile
public abstract class Party(string partyType) : IEntityWithSerial, IHasCode, IHasDescription,
    IHasTimestamps, IArchivable, IHasContactData, IHasContactData<PartyContactDetails>,
    IHasStartEndDate, IHasNormalizedTitle, IHasNormalizedContent
{
    public int Id { get; set; }
    [MaxLength(16)] public string PartyType { get; init; } = partyType;   // TPH discriminator
    [MaxLength(16)] public string? Code { get; set; }

    public abstract string? Title { get; }                    // computed per subtype — get-only satisfies IHasTitle
    [MaxLength(256)] public abstract string? NormalizedTitle { get; set; } // [Normalized] source differs per subtype
    [MaxLength(2048)] public string? Description { get; set; }
    [MaxLength(2048)] public virtual string? NormalizedContent { get; set; }

    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime Created { get; set; }
    public DateTime? LastModified { get; set; }
    public bool IsArchived { get; set; }

    public ICollection<PartyContactDetails>? ContactData { get; set; }
    ICollection<IContactDetails>? IHasContactData.ContactData          // non-generic bridge
    {
        get => ContactData?.Cast<IContactDetails>().ToList();
        set => ContactData = value?.Cast<PartyContactDetails>().ToList();
    }
    public ICollection<PartyAddress>? Addresses { get; set; }
    public ICollection<PartyRelationship>? ChildRelationships { get; set; }   // this party is the Parent
    public ICollection<PartyRelationship>? ParentRelationships { get; set; }  // this party is the Child
}

public static class PartyTypes { public const string Person = "PERSON"; public const string Organization = "ORGANIZATION"; }

public class Person() : Party(PartyTypes.Person)
{
    [MaxLength(32)] public string? Salutation { get; set; }
    [MaxLength(128)] public string? GivenName { get; set; }
    [MaxLength(128)] public string? FamilyName { get; set; }
    [Normalized(SourceProperties = [nameof(FamilyName), nameof(GivenName)])]
    public override string? NormalizedTitle { get; set; }
    public override string Title => $"{GivenName} {FamilyName}".Trim();
}
public class Organization() : Party(PartyTypes.Organization)
{
    [Required, MaxLength(256)] public string Name { get; set; } = null!;
    [MaxLength(32)] public string? LegalEntity { get; set; }
    [Normalized(SourceProperty = nameof(Name))]
    public override string? NormalizedTitle { get; set; }
    public override string Title => Name;
}
```

- `Title` is **deliberately get-only** (computed per subtype). `IHasTitle` declares only a getter, so this compiles — the usual "declare `{ get; set; }`" rule applies to *stored* titles, not computed ones. Keyword search works because `[Normalized]` on each leaf's `NormalizedTitle` names the real source properties.
- The primary-constructor parameter + `init` discriminator means a `Person` can never be saved with the wrong `PartyType`.

### Child contracts — contact data & addresses

Define the child shape once as an interface + abstract base; each owner gets a thin concrete subclass (own table):

```csharp no-compile
[Flags] public enum ContactDataTypes { Other = 0, Phone = 1 << 0, Email = 1 << 1, Website = 1 << 2 }

public interface IContactDetails : IEntity<int>, IHasTitle, ISortable, IHasDescription, IHasTimestamps
{
    string Value { get; set; }
    string? NormalizedValue { get; set; }
    ContactDataTypes DataType { get; set; }
}
public interface IHasContactData { ICollection<IContactDetails>? ContactData { get; set; } }
public interface IHasContactData<T> where T : class, IContactDetails, new() { ICollection<T>? ContactData { get; set; } }

public abstract class ContactDetailsBase : IContactDetails, IEntityWithSerial
{
    public int Id { get; set; }
    [MaxLength(64)] public string? Title { get; set; }        // e.g. "work", "mobile"
    [MaxLength(256)] public string Value { get; set; } = null!;
    [MaxLength(256)] public string? NormalizedValue { get; set; }
    public ContactDataTypes DataType { get; set; }
    [MaxLength(512)] public string? Description { get; set; }
    public int SortOrder { get; set; }
    public DateTime Created { get; set; }
    public DateTime? LastModified { get; set; }
}
public class PartyContactDetails : ContactDetailsBase;                 // FK to Party is a shadow FK
public class PartyRelationshipContactDetails : ContactDetailsBase      // contact data on a relation itself
{
    public int PartyRelationshipId { get; set; }
}

public abstract class AddressBase : IEntityWithSerial, IHasTitle, IHasNormalizedContent, ISortable
{
    public int Id { get; set; }
    [MaxLength(64)] public string? Title { get; set; }
    [MaxLength(128)] public string? Street { get; set; }
    [MaxLength(16)] public string? HouseNumber { get; set; }
    [MaxLength(16)] public string? PostalCode { get; set; }
    [MaxLength(128)] public string? City { get; set; }
    [StringLength(2)] public string? CountryCode { get; set; }
    public int SortOrder { get; set; }
    [MaxLength(2048)] public string? NormalizedContent { get; set; }
}
public class PartyAddress : AddressBase { public int PartyId { get; set; } }
```

The generic + non-generic `IHasContactData` pair (with the explicit-interface cast bridge on the owner) lets one normalizer/service handle *any* owner's contact rows through `IContactDetails` while EF maps the typed navigation.

### Typed relations between parties

```csharp no-compile
public class RelationshipType : IEntityWithSerial, IHasCode, IHasTitle, IHasDescription, IHasNormalizedContent
{
    public int Id { get; set; }
    [MaxLength(16)] public string? Code { get; set; }
    [Required, MaxLength(64)] public string? Title { get; set; }
    [MaxLength(512)] public string? Description { get; set; }
    [MaxLength(128), Normalized(SourceProperties = [nameof(Title), nameof(Code)])]
    public string? NormalizedContent { get; set; }
}

public class PartyRelationship : IEntityWithSerial, IHasStartEndDate, ISortable,
    IHasContactData, IHasContactData<PartyRelationshipContactDetails>
{
    public int Id { get; set; }
    public int ParentId { get; set; }
    public int ChildId { get; set; }
    public int RelationshipTypeId { get; set; }
    public DateTime? StartDate { get; set; }      // when the relation started/ended
    public DateTime? EndDate { get; set; }
    public int SortOrder { get; set; }

    public Party? Parent { get; set; }
    public Party? Child { get; set; }
    public RelationshipType? RelationshipType { get; set; }
    public ICollection<PartyRelationshipContactDetails>? ContactData { get; set; }
    ICollection<IContactDetails>? IHasContactData.ContactData
    {
        get => ContactData?.Cast<IContactDetails>().ToList();
        set => ContactData = value?.Cast<PartyRelationshipContactDetails>().ToList();
    }
}
```

### DbContext

```csharp no-compile
modelBuilder.Entity<PartyUser>(entity =>
{
    entity.HasOne(pu => pu.Party).WithOne().HasForeignKey<PartyUser>(pu => pu.PartyId).OnDelete(DeleteBehavior.Cascade);
});
modelBuilder.Entity<Party>(entity =>
{
    entity.HasDiscriminator(p => p.PartyType)
        .HasValue<Person>(PartyTypes.Person)
        .HasValue<Organization>(PartyTypes.Organization);
    entity.HasMany(e => e.ContactData).WithOne().OnDelete(DeleteBehavior.Cascade);
    entity.HasMany(e => e.Addresses).WithOne().HasForeignKey(a => a.PartyId).OnDelete(DeleteBehavior.Cascade);
});
modelBuilder.Entity<PartyRelationship>(entity =>
{
    entity.HasOne(r => r.Parent).WithMany(p => p.ChildRelationships).HasForeignKey(r => r.ParentId).OnDelete(DeleteBehavior.Restrict);
    entity.HasOne(r => r.Child).WithMany(p => p.ParentRelationships).HasForeignKey(r => r.ChildId).OnDelete(DeleteBehavior.Restrict);
    entity.HasIndex(r => new { r.ParentId, r.ChildId, r.RelationshipTypeId }).IsUnique();
    entity.HasMany(e => e.ContactData).WithOne().HasForeignKey(c => c.PartyRelationshipId).OnDelete(DeleteBehavior.Cascade);
});
```

- `Party` is `IArchivable`, so the archived query filter `UseDefaults()` wires in applies — on the `Party` root only, which covers both discriminator values.
- Owned children **cascade**; the self-referencing relation FKs are **`Restrict`** (cascade on a self-reference is rejected by SQL Server, and archival goes through `IArchivable` anyway). Per the `OnDelete` rule in the instructions: deleting a still-referenced party surfaces as a 409 Conflict; guard it in a prepper for a field-level 400.
- The unique index makes a duplicate edge a `UNIQUE constraint failed` instead of silent data drift.

### Registration

```csharp no-compile
public static IEntityServiceCollection<AppDbContext> AddParties(this IEntityServiceCollection<AppDbContext> services)
{
    services.For<Party, PartySearchObject, PartySortBy, PartyIncludes>(e =>
    {
        e.AddFilter<PartyQueryFilter>();
        e.AddSortBy<PartySortingQueryBuilder>();
        e.AddIncludes<PartyIncludingQueryBuilder>();

        e.Related(item => item.ContactData, item => item.ContactData?.Prepare());
        e.Related(item => item.Addresses, item => item.Addresses?.Prepare());
        e.Related(item => item.ChildRelationships, item => item.ChildRelationships?.Prepare(),
            rel => rel.Related(r => r.ContactData, r => r.ContactData?.Prepare()));   // nested: relation-level contact data
        e.Related(item => item.ParentRelationships, item => item.ParentRelationships?.Prepare(),
            rel => rel.Related(r => r.ContactData, r => r.ContactData?.Prepare()));

        e.AddNormalizer<PartyNormalizer>();
        e.HasRepository<PartyRepository>();                    // optional: adds tree methods, see Recursive entities
        e.AddTransient<IPartyService, PartyRepository>();      // domain interface for custom controller actions
    });
    services.For<RelationshipType>(e => e.SortBy(query => query.OrderBy(x => x.Title)));
    return services;
}
```

> **This is the canonical nested `Related()` example** — a two-level owned graph: the party owns its
> relationship rows, and each relationship row owns its *own* contact-data rows. The third argument
> (`configure`) receives a `RelatedEntityBuilder`; its `rel.Related(...)` registers a nested sync that
> runs **per relationship row**, matched to its original by `Id` — diffing that row's `ContactData`
> exactly like the outer sync diffs the relationships themselves (new rows: all nested rows tracked as
> added). Deeper graphs nest the same way (`configure` again). No hand-written prepper is needed for
> grandchild collections — write one only for logic *beyond* collection syncing.

`Prepare()` is a two-line app helper reused by every owned collection — it resets client-generated ids and applies sort order using framework extensions (`Regira.Entities.Extensions`):

```csharp no-compile
public static ICollection<T> Prepare<T>(this ICollection<T> items) where T : IEntity<int>
{
    items.Cast<IEntity<int>>().AdjustIdForEfCore();                       // new rows: negative/temp ids -> 0
    (items as IEnumerable<ISortable>)?.SetSortOrder();                    // ISortable children: index -> SortOrder
    return items;
}
```

### Polymorphic DTOs

One endpoint serves both subtypes; System.Text.Json needs the discriminator declared, and Mapster needs a runtime-type branch:

```csharp no-compile
[JsonPolymorphic(TypeDiscriminatorPropertyName = "partyType")]
[JsonDerivedType(typeof(PersonDto), PartyTypes.Person)]
[JsonDerivedType(typeof(OrganizationDto), PartyTypes.Organization)]
public abstract class PartyDto
{
    public int Id { get; set; }
    public string? Code { get; set; }
    public string? Title { get; set; }
    public ICollection<ContactDetailsDto>? ContactData { get; set; }
    public ICollection<AddressDto>? Addresses { get; set; }
    public ICollection<PartyParentDto>? ParentRelationships { get; set; }
    public ICollection<PartyChildDto>? ChildRelationships { get; set; }
    // + Description, StartDate/EndDate, Created/LastModified
}
public class PersonDto : PartyDto { public string? Salutation { get; set; } public string? GivenName { get; set; } public string? FamilyName { get; set; } }
public class OrganizationDto : PartyDto { public string? Name { get; set; } public string? LegalEntity { get; set; } }
// PartyInputDto mirrors this shape ([JsonPolymorphic] + PersonInputDto/OrganizationInputDto).

// Mapster maps by *declared* type — teach it to branch on the runtime type (both directions):
options.UseMapsterMapping(cfg =>
{
    cfg.ForType<Party, PartyDto>()
        .MapWith(src => (src as Person) != null ? (PartyDto)src.Adapt<PersonDto>() : src.Adapt<OrganizationDto>());
    cfg.ForType<PartyInputDto, Party>()
        .MapWith(src => (src as PersonInputDto) != null ? (Party)src.Adapt<Person>() : src.Adapt<Organization>());
});
```

Relationship DTOs come in a parent and a child flavor so each side embeds only the *other* party (`PartyParentDto { Parent }`, `PartyChildDto { Child }` — both extending a shared `PartyRelationshipDto` with ids, type, dates, contact data). Use a flat, non-polymorphic `PartySimpleDto` (with a plain `PartyType` string) inside them to stop the nesting from recursing.

### Controller

```csharp no-compile
[ApiController, Route("parties")]
public class PartyController(IPartyService service)
    : EntityControllerBase<Party, PartySearchObject, PartySortBy, PartyIncludes, PartyDto, PartyInputDto>
{
    // base CRUD comes from EntityControllerBase; custom actions use the injected DOMAIN interface
    [HttpGet("family")]
    public async Task<IActionResult> GetFamily([FromQuery] IList<int> ids, [FromQuery] int level = 9)
        => Ok((await service.GetFamily(ids, level)).ToTreeViewListResult());
}
```

> Injecting `IPartyService` works because the registration added `e.AddTransient<IPartyService, PartyRepository>()`. The "don't declare a controller constructor" rule targets the **framework's own** `IEntityService<>` (resolved internally); a custom-registered domain interface for extra actions is fine.

### Linking a party to a user account

Keep the identity link **off** the `Party` itself — a 1:1 join entity keeps the stakeholder domain independent from the identity store:

```csharp no-compile
public class PartyUser : IEntityWithSerial
{
    public int Id { get; set; }
    public string UserId { get; set; } = null!;   // external identity key (string — matches ASP.NET Identity)
    public int PartyId { get; set; }
    public Party? Party { get; set; }
}
```

(For entities that carry the user key directly, the framework offers the `IHasUserId` marker — `Regira.Entities.Models.Abstractions` — as a convention; no built-in services attach to it.)

### Gotchas

- **Filtered includes trip an EF warning.** A filtered include chained into a `ThenInclude` (`.Include(x => x.ParentRelationships!.Where(r => r.TypeId == typeId)).ThenInclude(...)`) triggers `CoreEventId.NavigationBaseIncludeIgnored`; suppress it in `AddDbContext`: `options.ConfigureWarnings(w => w.Ignore(CoreEventId.NavigationBaseIncludeIgnored))`. Archived counterparts need no such predicate — the archived query filter reaches into `Include()` on its own.
- **Normalize children before composing the parent.** The party normalizer first runs the contact-data/address normalizers, then joins their normalized output into the party's `NormalizedContent` — so one global `Q` filter finds parties by phone number or city (see §Filtering with Normalized Content in the instructions). Normalize phone numbers with `Regira.Globalization.LibPhoneNumber`'s `PhoneNumberFormatter` so `0475…` and `+32 475…` match.
- **Sorting builder:** implement `ISortedQueryBuilder<Party, int, PartySortBy>` directly (there is no `SortedQueryBuilderBase`) and continue existing ordering with `OrderOrThenBy(...)`/`OrderOrThenByDescending(...)` — never a hand-rolled `is IOrderedQueryable<T>` check.
- **TPH costs one budget slot**, not one per subtype — a strong reason to prefer TPH over a table per party type.

---

## EntityLabels — free-form labels on any entity

User-defined `Title`/`Value` pairs ("IP address = 10.0.0.5", "serial = X-123") attached to an entity, editable inside the owner's form, orderable, and searchable through the owner's `Q`. **Not** a polymorphic single-table design: each owner gets its own thin label subclass and table — no `ObjectType` discriminator, no cross-owner queries to guard, cheap FKs.

**Budget:** 0 slots — labels are owned `Related()` children of entities you already registered.

### Contract + base + per-owner subclass

```csharp no-compile
public interface IEntityLabel : IHasTitle, ISortable, IHasTimestamps, IHasNormalizedContent
{
    int ObjectId { get; set; }        // FK to the owner
    string Value { get; set; }
    string? LabelType { get; set; }   // display hint: Phone/Email/Url/Date/… (client-detected)
}

public abstract class EntityLabelBase : IEntityLabel, IEntityWithSerial
{
    public int Id { get; set; }
    public int ObjectId { get; set; }
    [MaxLength(64)] public string? Title { get; set; }
    [MaxLength(512)] public string Value { get; set; } = null!;
    [MaxLength(64)] public string? LabelType { get; set; }
    public int SortOrder { get; set; }
    public DateTime Created { get; set; }
    public DateTime? LastModified { get; set; }
    [MaxLength(1024)] public string? NormalizedContent { get; set; }
}

// one empty subclass per owner = one table per owner
public class VehicleLabel : EntityLabelBase;
public class InterventionLabel : EntityLabelBase;
```

### Owner wiring

```csharp no-compile
public interface IHasLabels { ICollection<IEntityLabel>? Labels { get; set; } }
public interface IHasLabels<T> where T : IEntityLabel { ICollection<T>? Labels { get; set; } }

public class Vehicle : IEntityWithSerial, IHasNormalizedContent, IHasLabels<VehicleLabel>, IHasLabels /* … */
{
    // …
    public ICollection<VehicleLabel>? Labels { get; set; }
    ICollection<IEntityLabel>? IHasLabels.Labels               // non-generic bridge for shared services
    {
        get => Labels?.Cast<IEntityLabel>().ToList();
        set => Labels = value?.Cast<VehicleLabel>().ToList();
    }
}
```

DbContext — same three lines per owner (add a `DbSet<VehicleLabel>` too):

```csharp no-compile
modelBuilder.Entity<Vehicle>(entity =>
{
    entity.HasMany(e => e.Labels).WithOne()        // no inverse navigation on the label
        .HasForeignKey(e => e.ObjectId)
        .HasPrincipalKey(e => e.Id);               // required FK -> cascade delete by convention
});
```

### DTOs — one shared pair for every owner

```csharp no-compile
public class EntityLabelDto
{
    public int Id { get; set; }
    public int ObjectId { get; set; }
    public string? Title { get; set; }
    public string Value { get; set; } = null!;
    public string? LabelType { get; set; }
    public int SortOrder { get; set; }
    public DateTime Created { get; set; }
    public DateTime? LastModified { get; set; }
}
public class EntityLabelInputDto
{
    public int Id { get; set; }
    public int ObjectId { get; set; }
    [MaxLength(64)] public string? Title { get; set; }
    [Required, MaxLength(512)] public string Value { get; set; } = null!;
    [MaxLength(64)] public string? LabelType { get; set; }
    public int SortOrder { get; set; }
}
// owner DTOs expose: public ICollection<EntityLabelDto>? Labels   (input: EntityLabelInputDto)
// Mapster maps VehicleLabel <-> EntityLabelDto by convention — no AddMapping needed.
```

### Registration + search

```csharp no-compile
services.UseEntities<AppDbContext>(options =>
{
    options.UseMapsterMapping();
    options.AddNormalizer<IEntityLabel, EntityLabelNormalizer>();   // ONE normalizer serves every label subclass
    options.UseDefaults();
})
.For<Vehicle, VehicleSearchObject, EntitySortBy, VehicleIncludes>(e =>
{
    e.AddNormalizer<VehicleNormalizer>();
    e.Related(item => item.Labels, item => item.Labels?.Prepare()); // owned: no For<>, no controller, no slot
    // gate eager-loading behind an includes flag, ordered for display:
    e.Includes((query, includes) => includes?.HasFlag(VehicleIncludes.Labels) == true
        ? query.Include(x => x.Labels!.OrderBy(l => l.SortOrder))
        : query);
});
```

Labels become searchable by folding their text into the **owner's** `NormalizedContent` (the global `Q` filter then covers them — never add a second `Q` filter):

```csharp no-compile
public class EntityLabelNormalizer(INormalizer defaultNormalizer) : EntityNormalizerBase<IEntityLabel>(defaultNormalizer)
{
    public override Task HandleNormalize(IEntityLabel item, CancellationToken token = default)
    {
        // optionally branch per value shape (phone via PhoneNumberFormatter, IP with '.'->'_' variant, …)
        item.NormalizedContent = $"{DefaultPropertyNormalizer.Normalize(item.Title)} {DefaultPropertyNormalizer.Normalize(item.Value)}".Trim();
        return Task.CompletedTask;
    }
}

public class VehicleNormalizer(INormalizer normalizer, IEntityNormalizer<IEntityLabel> labelNormalizer)
    : EntityNormalizerBase<Vehicle>(normalizer)
{
    public override async Task HandleNormalize(Vehicle item, CancellationToken token = default)
    {
        await base.HandleNormalize(item, token);
        var entries = new List<string?> { item.NormalizedContent };
        if (item.Labels?.Any() == true)
        {
            await labelNormalizer.HandleNormalizeMany(item.Labels, token);
            entries.AddRange(item.Labels.Select(l => l.NormalizedContent));
        }
        item.NormalizedContent = string.Join(' ', entries.Where(x => !string.IsNullOrWhiteSpace(x)));
    }
}
```

### Gotchas

- **Labels are searchable, not filterable.** `?q=` matches label text through the owner's `NormalizedContent`; there is no `LabelType`/`Value` filter unless you add one (a `Where(x => x.Labels!.Any(...))` in the owner's query filter).
- **Re-normalization timing:** the owner's `NormalizedContent` embeds label text at save time — labels edited *through the owner's form* (the normal path with `Related()`) keep it fresh automatically.
- **Duplicates are allowed by design** (no unique constraint) — the same `Title` can appear twice with different values.
- **`LabelType` is a display hint set by the client** (detect email/phone/url/date from the value shape in the SPA); the backend only stores it.
- Owner deletion removes its labels via cascade (required FK). `SortOrder` is maintained by the `Prepare()` helper (see Stakeholders) + a draggable list in the SPA.

---

## Multi-tenancy — IHasTenantId + global filter + primer

Row-level tenant isolation in three small pieces: a marker interface on tenant-owned entities, **one global query filter** that scopes every read, and **one primer** that stamps every write. Nothing per-entity — implementing the interface is all an entity needs.

**Budget:** the plumbing costs 0 slots; the `Tenant` entity itself (if exposed) is 1 complex slot.

### Marker + tenant context

```csharp no-compile
public interface IHasTenantId { string TenantId { get; set; } }   // string: matches Identity/GUID keys

public interface ITenantContext { string? TenantId { get; } }

// HTTP: read the tenant lazily from the principal on every access, whichever scheme authenticated it
// (register AddHttpContextAccessor()!)
public class TenantContext(IHttpContextAccessor httpContextAccessor) : ITenantContext
{
    public string? TenantId => httpContextAccessor.HttpContext?.User.FindFirstValue("tenant");
}
// non-HTTP hosts (seeders, jobs): a writable stand-in the host sets imperatively
public class WritableTenantContext : ITenantContext { public string? TenantId { get; set; } }
```

Tenant-owned entities just implement the marker (typically via a shared app-level base interface):

```csharp no-compile
public interface IAppEntity : IEntity<int>, IHasTimestamps, IHasTenantId { }   // every domain entity
// on the entity: [StringLength(32)] public string TenantId { get; set; } = null!;
```

### Global filter + primer

```csharp no-compile
// Scope every query of every IHasTenantId entity — reads simply never see foreign rows.
public class FilterHasTenantQueryBuilder(ITenantContext tenantContext) : FilterHasTenantQueryBuilder<int>(tenantContext);
public class FilterHasTenantQueryBuilder<TKey>(ITenantContext tenantContext) : GlobalFilteredQueryBuilderBase<IHasTenantId, TKey>
{
    public override IQueryable<IHasTenantId> Build(IQueryable<IHasTenantId> query, ISearchObject<TKey>? _)
        => query.Where(x => x.TenantId == tenantContext.TenantId);
}

// Stamp the active tenant on every saved IHasTenantId entity — the client's TenantId is never trusted.
public class HasTenantPrimer(ITenantContext tenantContext) : EntityPrimerBase<IHasTenantId>
{
    public override Task PrepareAsync(IHasTenantId entity, EntityEntry entry, CancellationToken token = default)
    {
        if (!string.IsNullOrWhiteSpace(tenantContext.TenantId))
            entity.TenantId = tenantContext.TenantId;
        return Task.CompletedTask;
    }
}
```

The generic-`TKey` base + int specialization mirrors the framework's own global filters: the query pipeline picks the key-matching variant per entity, so string-keyed tenant-owned entities are covered by registering the `<string>` variant too.

### Registration

```csharp no-compile
services.AddHttpContextAccessor()
    .AddScoped<ITenantContext, TenantContext>();

services.UseEntities<AppDbContext>(options =>
{
    options.UseDefaults();
    options.AddGlobalFilterQueryBuilder<FilterHasTenantQueryBuilder>();  // every IHasTenantId read is scoped
    options.AddPrimer<HasTenantPrimer>();                                // every IHasTenantId write is stamped
});
// the primer interceptor is auto-wired by UseDefaults(); without it, select e.WireDbContext(DbContextWiring.PrimerInterceptors) — see §DbContext Interceptors.

// seeding host: swap in the writable context and set the tenant per wave
services.AddSingleton<WritableTenantContext>();
services.AddSingleton<ITenantContext>(p => p.GetRequiredService<WritableTenantContext>());
```

### Where the tenant id comes from

The active tenant travels **inside the caller's credential** as a `tenant` claim — no header or route parameter on ordinary requests:

- At sign-in, a claims step reads the requested tenant (e.g. `?tenantId=`), verifies membership, and adds `new Claim("tenant", tenantId)` plus that tenant's permission claims (`permissions=can_read`, …) from a `TenantUserClaim` store (user × tenant × claim rows).
- **Switching tenants = re-issuing the credential.** With a JWT that means re-minting the token; with a cookie session it means signing in again with the new claim set. Either way `TenantContext` picks the new claim up automatically — it reads `HttpContext.User`, so it does not care which scheme put the claim there.
  - ⚠️ If you drive this off a refresh endpoint, note that `auth/refresh` and `auth/refresh-token` are different: the first needs a **still-valid** bearer token, the second takes the refresh token and works after expiry. See `security.instructions` → *Refresh Tokens*.
- Per-tenant permissions are enforced with a global authorization filter checking the claim (e.g. forbid unless `permissions=can_read`).
- **On Microsoft Entra ID the tenant is already there** as `tid`, and the stable per-user key is **`oid`, not `sub`** — Entra's `sub` is pairwise per application, so keying tenant or owner rows on it fragments one person across apps.

### The Tenant entity itself

`Tenant` is a normal string-keyed entity in the identity context — **not** tenant-filtered (it *is* the tenant; admins list all of them):

```csharp no-compile
public class Tenant : IEntity<string>, IHasCode, IHasNormalizedTitle, IHasDescription, IHasTimestamps
{
    [StringLength(32)] public string Id { get; set; } = Guid.NewGuid().ToString("N");
    [MaxLength(8)] public string? Code { get; set; }
    [MaxLength(64)] public string Title { get; set; } = null!;
    [MaxLength(256), Normalized(SourceProperties = [nameof(Title), nameof(Code)])]
    public string? NormalizedTitle { get; set; }
    public ICollection<TenantSubscription>? Subscriptions { get; set; }
    // + DefaultCulture, Description, timestamps, Languages …
}

services.For<Tenant, string, TenantSearchObject, EntitySortBy, TenantIncludes>(e =>
{
    e.AddFilter<TenantFilteredQueryBuilder>();
    e.AddIncludes<TenantIncludableQueryBuilder>();
    e.Related<TenantSubscription, int>(c => c.Subscriptions);
});
```

### Gotchas

- **Register the tenant filter only on the domain context.** The identity/admin context (users, tenants) must stay unscoped, or an admin can no longer list tenants.
- **The primer stamps but does not verify** — a request whose JWT carries tenant A simply cannot see or keep rows for tenant B (filter scopes reads; primer overwrites the FK on save). Seeding with an *empty* `WritableTenantContext.TenantId` leaves explicitly-set ids intact — that's the escape hatch for cross-tenant seed data.
- **Direct `IEntityService` reads are scoped too** (global filters run in the query pipeline, not the controller), but only when the resolved `ITenantContext` carries a tenant — background jobs without a tenant see everything. Decide per job: set `WritableTenantContext.TenantId` or accept the global view.
- Non-int keyed tenant-owned entities need the matching key variant of the filter registered (same rule as the built-in global filters).

---

## Recursive entities — whole-subtree filtering with mapped DB functions

Three escalating layers for self-referencing entities. Layers 1–2 are covered elsewhere; this blueprint is layer 3.

**Not tree-only.** The worked example is a **graph** — a `Category` reachable from several parents through the `RelatedCategories` join — so "recursive" here means self-referencing, not single-parent. If you need to filter on a subtree/subgraph of a node, this is the section, whatever the arity.

1. **Direct relations** — `ParentEntities`/`ChildEntities` + `ParentId`/`ChildId`/`IsRoot` filters: see the **Category** entity in [`entities.examples.md`](./entities.examples.md) (single-parent variant: §Hierarchical Data in [`entities.patterns.md`](./entities.patterns.md)).
2. **In-memory trees** — load flat rows, assemble with `Regira.TreeList` (`ToTreeList`, `GetOffspring`, `OrderByHierarchy`, …): `get_package("Regira.TreeList")`.
3. **Whole-subtree SQL** — *"all products under category X, any depth"* as a **table-valued function** (recursive CTE) mapped into EF Core, composable inside any query filter. This scales where loading the whole table into a `TreeList` doesn't, at the cost of provider-specific SQL (shown for SQL Server).

> **Which layer? Match your provider.** Layer 3 is **SQL Server** (`OPENJSON`/`CREATE FUNCTION` don't exist on
> SQLite; a Postgres/MySQL port needs its own dialect). On the **default SQLite starter use layer 1** (direct
> `ParentId`/`ChildId` filters) or **layer 2** (`Regira.TreeList`, whole-tree in memory) — enough for most
> apps. Reach for layer 3 only for trees too large to load, on a provider with recursive-CTE functions.

The blueprint continues the multi-parent `Category`/`RelatedCategory` example (join table `RelatedCategories`, entities table `Categories`).

**Budget:** 0 extra slots — everything below attaches to entities you already registered. (Keyless projections are not entity registrations.)

### 1. Keyless projection + mapped functions on the DbContext

```csharp no-compile
// the row shape returned by the tree functions — keyless, never tracked, no table
public class CategoryTreeItem
{
    public int ParentId { get; set; }
    public int ChildId { get; set; }
    public int Level { get; set; }     // 0-based depth from the seed; negative = ancestor side of Family
    public int RootId { get; set; }    // the seed id this row was reached from
    public Category? Parent { get; set; }
    public Category? Child { get; set; }
}

public partial class WebshopDbContext
{
    // DB-mapped stubs: EF translates calls into SELECT * FROM dbo.GetCategoryOffspring(@ids, @max_level)
    protected IQueryable<CategoryTreeItem> GetCategoryOffspring(string? ids, int maxLevel)
        => FromExpression(() => GetCategoryOffspring(ids, maxLevel));
    protected IQueryable<CategoryTreeItem> GetCategoryAncestors(string? ids, int maxLevel)
        => FromExpression(() => GetCategoryAncestors(ids, maxLevel));
    protected IQueryable<CategoryTreeItem> GetCategoryFamily(string? ids, int maxLevel)
        => FromExpression(() => GetCategoryFamily(ids, maxLevel));

    // convenience overloads: int ids -> JSON array string ("[1,2,3]"), parsed by OPENJSON in SQL
    public IQueryable<CategoryTreeItem> GetCategoryOffspring(IEnumerable<int>? ids = null, int maxLevel = 9)
        => GetCategoryOffspring(ToJsonArray(ids), maxLevel);
    public IQueryable<CategoryTreeItem> GetCategoryAncestors(IEnumerable<int>? ids, int maxLevel = 9)
        => GetCategoryAncestors(ToJsonArray(ids), maxLevel);
    public IQueryable<CategoryTreeItem> GetCategoryFamily(IEnumerable<int>? ids, int maxLevel = 9)
        => GetCategoryFamily(ToJsonArray(ids), maxLevel);

    partial void ConfigureFunctions(ModelBuilder modelBuilder)   // call from OnModelCreating
    {
        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic; // stubs are protected
        modelBuilder.Entity<CategoryTreeItem>(entity =>
        {
            entity.HasNoKey().ToTable((string?)null);            // query-only projection
            entity.HasOne(x => x.Parent).WithMany();             // optional: navs let you Include real entities
            entity.HasOne(x => x.Child).WithMany();
        });
        modelBuilder.HasDbFunction(typeof(WebshopDbContext).GetMethod(nameof(GetCategoryOffspring), flags, [typeof(string), typeof(int)])!).HasSchema("dbo");
        modelBuilder.HasDbFunction(typeof(WebshopDbContext).GetMethod(nameof(GetCategoryAncestors), flags, [typeof(string), typeof(int)])!).HasSchema("dbo");
        modelBuilder.HasDbFunction(typeof(WebshopDbContext).GetMethod(nameof(GetCategoryFamily), flags, [typeof(string), typeof(int)])!).HasSchema("dbo");
    }

    private static string? ToJsonArray(IEnumerable<int>? ids)
    {
        var list = ids as IList<int> ?? ids?.ToList();
        return list == null || list.Count == 0 ? null : $"[{string.Join(",", list)}]";
    }
}
```

### 2. The SQL — recursive CTE functions (SQL Server)

Keep the DDL as constants next to the DbContext; `CREATE OR ALTER` makes execution idempotent:

```csharp no-compile
public static class CategoryDbFunctions
{
    public static readonly string CREATE_GetCategoryOffspring = """
        CREATE OR ALTER FUNCTION [dbo].[GetCategoryOffspring] (@ids NVARCHAR(MAX) = NULL, @max_level INT = 9)
        RETURNS TABLE AS RETURN
            WITH offspring (ParentId, ChildId, Level, RootId) AS (
                SELECT     r.ParentId, r.ChildId, 0, r.ParentId
                FROM       RelatedCategories r
                WHERE      (@ids IS NULL OR @ids = '' OR r.ParentId IN (SELECT CAST(value AS INT) FROM OPENJSON(@ids)))
                       AND NOT EXISTS (SELECT 1 FROM Categories s WHERE (s.Id = r.ChildId OR s.Id = r.ParentId) AND s.IsArchived = 1)
                UNION ALL
                SELECT     sc.ParentId, sc.ChildId, offspring.Level + 1, offspring.RootId
                FROM       RelatedCategories sc
                INNER JOIN offspring ON offspring.ChildId = sc.ParentId
                WHERE      (@max_level IS NULL OR offspring.Level < @max_level)
                       AND NOT EXISTS (SELECT 1 FROM Categories s WHERE (s.Id = sc.ChildId OR s.Id = sc.ParentId) AND s.IsArchived = 1)
            )
            SELECT * FROM offspring;
        """;

    // GetCategoryAncestors: identical shape, walking the other way —
    //   seed:      WHERE r.ChildId IN (OPENJSON ids), RootId = r.ChildId
    //   recursion: INNER JOIN ancestors ON ancestors.ParentId = sc.ChildId

    public static readonly string CREATE_GetCategoryFamily = """
        CREATE OR ALTER FUNCTION [dbo].[GetCategoryFamily] (@ids NVARCHAR(MAX) = NULL, @max_level INT = 9)
        RETURNS TABLE AS RETURN
        (
            SELECT ParentId, ChildId, -(Level + 1) AS Level, RootId FROM [dbo].[GetCategoryAncestors](@ids, @max_level)
            UNION ALL
            SELECT ParentId, ChildId,  Level + 1  AS Level, RootId FROM [dbo].[GetCategoryOffspring](@ids, @max_level)
        );
        """;

    public static string[] CREATE_ALL => [CREATE_GetCategoryOffspring, CREATE_GetCategoryAncestors, CREATE_GetCategoryFamily];
}
```

Semantics: **Offspring** walks parent→child from the seed ids (`Level` 0-based, `RootId` = the seed); **Ancestors** walks child→parent; **Family** = both directions with signed levels (negative = ancestors). `@ids = NULL` seeds from *every* edge. Archived nodes are pruned at every step — the global archived filter can't reach inside a TVF, so the SQL must re-apply it.

**Creating the functions.** With migrations: `migrationBuilder.Sql(CategoryDbFunctions.CREATE_GetCategoryOffspring)` (one per statement). With `EnsureCreated()`: execute after schema creation, provider-gated —

```csharp no-compile
await db.Database.EnsureCreatedAsync();
if (db.Database.ProviderName == "Microsoft.EntityFrameworkCore.SqlServer")
    foreach (var sql in CategoryDbFunctions.CREATE_ALL)
        await db.Database.ExecuteSqlRawAsync(sql);
```

### 3. SearchObject + query-filter composition

```csharp no-compile
public record CategorySearchObject : SearchObject
{
    public ICollection<int>? ParentId { get; set; }     // direct relation (layer 1)
    public ICollection<int>? ChildId { get; set; }
    public bool? IsRoot { get; set; }
    public ICollection<int>? AncestorId { get; set; }   // recursive: any depth below these ids
    public ICollection<int>? OffspringId { get; set; }  // recursive: any depth above these ids
    public ICollection<int>? RootId { get; set; }       // recursive: reachable from these seeds
}

// in the query filter — the TVF composes server-side inside the predicate (one SQL statement):
public override IQueryable<Category> Build(IQueryable<Category> query, CategorySearchObject? so)
{
    // … direct-relation filters (see the Category example) …
    if (so?.AncestorId?.Any() == true)
        query = query.Where(x => dbContext.GetCategoryOffspring(so.AncestorId, 9).Any(o => o.ChildId == x.Id));
    if (so?.OffspringId?.Any() == true)
        query = query.Where(x => dbContext.GetCategoryAncestors(so.OffspringId, 9).Any(o => o.ParentId == x.Id));
    if (so?.RootId?.Any() == true)
        query = query.Where(x => dbContext.GetCategoryOffspring(so.RootId, 9).Any(o => o.RootId == x.Id));
    return query;
}
```

The same composition powers *indirect* filters on other entities — e.g. "products in category X **or any of its subcategories**": resolve the subtree ids once (`GetCategoryOffspring(so.CategoryId).Select(o => o.ChildId)`) and filter the product query with them.

### 4. Tree endpoints — TVF → `TreeList` → controller

Extend the entity's repository (`e.HasRepository<CategoryRepository>()` + a domain interface, as in Stakeholders) with tree methods that materialize the flat rows and assemble a `TreeList` (`Regira.TreeList`):

```csharp no-compile
public class CategoryRepository(WebshopDbContext dbContext,
    IEntityReadService<Category, int, CategorySearchObject> readService,
    IEntityWriteService<Category, int> writeService)
    : EntityRepository<Category, int, CategorySearchObject>(readService, writeService), ICategoryService
{
    public async Task<TreeList<CategoryTreeItem>> GetOffspring(IList<int> ids, int maxLevel = 9)
    {
        var items = await dbContext.GetCategoryOffspring(ids, maxLevel).ToListAsync();
        // ToTreeList's selector returns each row's PARENT rows: an edge-row's parent is the row
        // that ends where it starts (p.ChildId == x.ParentId) — same rule for every direction.
        return items.ToTreeList(x => items.FindAll(p => p.ChildId == x.ParentId));
    }
    public async Task<TreeList<CategoryTreeItem>> GetAncestors(IList<int> ids, int maxLevel = 9)
    {
        var items = await dbContext.GetCategoryAncestors(ids, maxLevel).ToListAsync();
        return items.ToTreeList(x => items.FindAll(p => p.ChildId == x.ParentId));
    }
}

// controller actions (on the entity's controller): GET categories/offspring?ids=1&level=9  (+ /ancestors, /family)
[HttpGet("offspring")]
public async Task<IActionResult> GetOffspring([FromQuery] IList<int> ids, [FromQuery] int level = 9)
    => Ok(new ListResult<CategoryTreeItem> { Items = (await service.GetOffspring(ids, level)).ToTreeView() });
```

`ToTreeView()` flattens the tree depth-first, so the SPA receives parent-before-children order and can rebuild its own tree client-side (the `regira_modules/treelist` npm module is the front-end counterpart).

### Gotchas

- **SQL Server syntax shown** (`OPENJSON`, `CREATE OR ALTER FUNCTION`). PostgreSQL/MySQL need their own dialect (`WITH RECURSIVE`, `jsonb_array_elements_text`/`JSON_TABLE`); the C# mapping side is provider-neutral. Gate function creation on `Database.ProviderName`, and don't offer the recursive filters on providers where the functions don't exist.
- **The functions must exist before the first query** that composes them — `EnsureCreated()` does *not* create them. Run the `CREATE OR ALTER` batch at startup/seed time or in a migration.
- **`@max_level` is the cycle guard.** The multi-parent join allows cycles in principle; the level cap keeps the CTE from recursing forever. Keep the unique `(ParentId, ChildId)` index from the Category example, and validate "child may not be its own ancestor" on write with `TreeList.IsValidChild` when users edit the hierarchy.
- **Prune archived rows inside the SQL** — global query filters (archived, tenant) do not apply within a TVF. A tenant-scoped tree needs `TenantId` as a function parameter.
- The keyless projection is never tracked and has no `DbSet` — query it only through the mapped functions.
- Materialize (`ToListAsync`/`ToHashSet`) when the ids feed multiple later predicates; leave it composed inside a single `Where` when it's one predicate — that runs as one SQL statement.

---

## Identity users as framework entities

Expose ASP.NET Core Identity users with the same List/Search/Details/Save surface, DTO mapping and SearchObject filtering as any entity — one custom `IEntityRepository` implementation wrapping `UserManager<TUser>`, then a plain thin controller. Password hashing, validation and claim storage stay Identity's job.

**Budget:** 1 complex slot.

### Exposed model + repository over UserManager

```csharp no-compile
// The exposed entity model (NOT the IdentityUser itself): string-keyed, carries claims + write-only password
public class AppUserEntity : IEntity<string>
{
    public string Id { get; set; } = null!;
    public string? UserName { get; set; }
    public string? Email { get; set; }
    public string? NewPassword { get; set; }                       // write-only; excluded from the read DTO
    public ICollection<UserClaimModel>? UserClaims { get; set; }
}

public class AppUserRepository(AccountsDbContext dbContext, UserManager<AppIdentityUser> userManager,
    IEnumerable<IFilteredQueryBuilder<AppIdentityUser, string, AppUserSearchObject>> queryFilters, IMapper mapper)
    : IEntityRepository<AppUserEntity, string, AppUserSearchObject, EntitySortBy, AppUserIncludes>
{
    public async Task<IList<AppUserEntity>> List(IList<AppUserSearchObject?> searchObjects, IList<EntitySortBy> sortBy,
        AppUserIncludes? includes = null, PagingInfo? pagingInfo = null, CancellationToken token = default)
    {
        var query = Filter(dbContext.Users, searchObjects);        // run the registered query filters yourself
        query = query.OrderBy(x => x.UserName).PageQuery(pagingInfo);
        if (includes?.HasFlag(AppUserIncludes.UserClaims) == true)
            query = query.Include(x => x.UserClaims);
        var items = await query.AsNoTrackingWithIdentityResolution().ToListAsync(token);
        return mapper.Map<List<AppUserEntity>>(items);             // inner IdentityUser -> exposed entity model
    }

    public async Task Add(AppUserEntity model, CancellationToken token = default)
    {
        var item = mapper.Map<AppIdentityUser>(model);
        var result = string.IsNullOrWhiteSpace(model.NewPassword)
            ? await userManager.CreateAsync(item)                  // Identity owns hashing + validation
            : await userManager.CreateAsync(item, model.NewPassword);
        // …then reconcile claims (diff original vs incoming: RemoveRange / AddRange / update changed values)
    }
    // Details/Count/Modify/Save/Remove follow the same shape; Save = upsert on existence.
    public Task<int> SaveChanges(CancellationToken token = default) => dbContext.SaveChangesAsync(token);
}
```

### Registration + controller

```csharp no-compile
// Note the filter is typed on the INNER IdentityUser (the type the query runs against):
services.For<AppUserEntity, string, AppUserSearchObject, EntitySortBy, AppUserIncludes>(e =>
{
    e.AddTransient<IFilteredQueryBuilder<AppIdentityUser, string, AppUserSearchObject>, AppUserQueryFilter>();
    e.HasRepository<AppUserRepository>();
    e.UseEntityService<AppUserRepository>();
});

// Controller — the standard thin base:
public class AppUserController : EntityControllerBase<AppUserEntity, string, AppUserSearchObject, EntitySortBy, AppUserIncludes, AppUserDto, AppUserInputDto>;
```

### Gotchas

- The repository, not the framework, applies filters/sorting/paging/includes — you implement `IEntityRepository` from scratch, so inject the registered `IFilteredQueryBuilder<...>` set and run them in `List`/`Count` (that keeps SearchObject filters in their own classes).
- The query-builder generics are typed on the **inner** `IdentityUser` type, while the `For<>()` generics use the **exposed** model — the mapper bridges the two.
- Exclude `NewPassword` from the read DTO; hash only via `userManager.PasswordHasher` on update. Deleting goes through `userManager.DeleteAsync`.

---

## Virtual entity — reference data without a table

Serve static/computed reference data (countries, currencies, time zones) through the standard entity surface — same endpoints, same SPA components — by implementing `IEntityService` over an in-memory source and swapping it in with `UseEntityService`. No DbContext, no table, no migration.

**Budget:** 1 simple slot.

### Model + in-memory IEntityService

```csharp no-compile
public class Country : IHasCode, IHasNormalizedTitle, IHasDefault<string>
{
    public string Id { get; set; } = null!;                        // ISO2
    public string? Title { get; set; }
    public string? Code { get => Id; set => Id = value!; }
    public bool IsDefault { get; set; }                            // current culture's country
    public string? NormalizedTitle { get; set; }
}

public class CountryRepository(ICultureContext cultureContext) : IEntityService<Country, string, SearchObject<string>>
{
    public Task<Country?> Details(string id, CancellationToken token = default)
        => Task.FromResult(Convert(CountryUtility.GetCountry(id)));   // Regira.Globalization.Utilities

    public Task<IList<Country>> List(SearchObject<string>? so = null, PagingInfo? pagingInfo = null, CancellationToken token = default)
    {
        var query = CountryUtility.GetCountries();
        query = !string.IsNullOrWhiteSpace(so?.Q)
            ? query.Select(x => new { country = x, weight = CalculateWeight(x, so) })   // relevance: exact > initials > prefix > contains
                   .Where(x => x.weight > 0).OrderByDescending(x => x.weight).Select(x => x.country)
            : query.OrderBy(x => x.Iso2Code == cultureContext.CountryCode ? 0 : 1).ThenBy(x => x.Title);
        // apply pagingInfo manually (PageItems), map to Country, return
    }

    // untyped overloads delegate via ObjectUtility.Create<SearchObject<string>>(so);
    // Add/Modify/Save/Remove/SaveChanges throw NotImplementedException — read-only by contract.
}
```

### Registration + controller

```csharp no-compile
services.For<Country, string>(e => e.UseEntityService<CountryRepository>());
public class CountryController : EntityControllerBase<Country, string, SearchObject<string>, CountryDto, CountryDto>;
```

### Gotchas

- You own everything the pipeline normally does: apply `Q`, ordering and `PagingInfo` yourself (`PageQuery`/`PageItems` from `Regira.DAL.Paging`); registered query builders/processors don't run against in-memory data.
- Ranked matching beats plain `Contains` for picker UX — weight exact code/name matches above prefix above substring, and boost the caller's own country (`IHasDefault`).
- Write endpoints exist on the controller but return 500 from the `NotImplementedException` — add `[ApiExplorerSettings(IgnoreApi = true)]` overrides or authorization if that surface bothers you; the reference apps simply never call them.

---

## In-code recipes (how_to)

Task-oriented entries served by the MCP `how_to` tool (same marker convention as [`entities.patterns.md`](./entities.patterns.md)).

### Model parties with contact data and addresses (stakeholders)
<!-- how_to: key=stakeholders aliases=party,parties,stakeholder,contact,contactdata,address,addresses,crm,person,organization,relation,relations -->
Copy the **Stakeholders blueprint**: a TPH `Party` base (`Person`/`Organization` leaves, string
discriminator, 1 budget slot total) with `ContactData`, `Addresses` and typed `PartyRelationship`
collections managed as owned children via nested `e.Related(...)` — no own registrations. DTOs are
polymorphic (`[JsonPolymorphic]` + Mapster `MapWith` branching on the runtime type). Link users via a
1:1 `PartyUser { UserId, PartyId }` join, not a field on `Party`.

**See:** `get_package("Regira.Entities", section: "blueprints", heading: "Stakeholders")`.

### Add free-form labels/tags to an entity
<!-- how_to: key=entity-labels aliases=label,labels,tag,tags,entitylabel,keyvalue,metadata -->
Copy the **EntityLabels blueprint**: an abstract `EntityLabelBase` (`Title`/`Value`/`LabelType`/
`SortOrder`) with one empty subclass **per owner** (own table, FK `ObjectId`, cascade). Wire as an owned
collection — `e.Related(x => x.Labels, x => x.Labels?.Prepare())` — behind an includes flag ordered by
`SortOrder`. Search works by folding label text into the owner's `NormalizedContent` via one global
`options.AddNormalizer<IEntityLabel, EntityLabelNormalizer>()` + the owner's normalizer. Costs no
budget slots.

**See:** `get_package("Regira.Entities", section: "blueprints", heading: "EntityLabels")`.

### Make an app multi-tenant (row-level isolation)
<!-- how_to: key=multi-tenancy aliases=tenant,tenants,tenancy,multitenant,tenantid,isolation -->
Copy the **Multi-tenancy blueprint**: an `IHasTenantId { string TenantId }` marker on tenant-owned
entities, plus two registrations inside `UseEntities`:

```csharp no-compile
options.AddGlobalFilterQueryBuilder<FilterHasTenantQueryBuilder>(); // scopes every IHasTenantId read
options.AddPrimer<HasTenantPrimer>();                               // stamps TenantId on every write
```

Both resolve the active tenant from a scoped `ITenantContext` reading the `tenant` claim off the caller's principal (whichever scheme authenticated it)
(`AddHttpContextAccessor()` required); switching tenants re-mints the token. Seeders swap in a
`WritableTenantContext`. Don't register the filter on the identity/admin context.

**See:** `get_package("Regira.Entities", section: "blueprints", heading: "Multi-tenancy")`.

### Filter a whole subtree (ancestors / descendants at any depth)
<!-- how_to: key=recursive-tree aliases=tree,subtree,hierarchy,recursive,ancestor,ancestors,offspring,descendants,cte,dbfunction,treelist -->
Copy the **Recursive entities blueprint**: map recursive-CTE table-valued functions
(`GetXOffspring/Ancestors/Family(@ids, @max_level)`) into the DbContext with `FromExpression` stubs +
`HasDbFunction`, returning a keyless projection (`HasNoKey().ToTable((string?)null)`). Compose them
inside query filters:

```csharp no-compile
if (so.AncestorId?.Any() == true)
    query = query.Where(x => dbContext.GetCategoryOffspring(so.AncestorId, 9).Any(o => o.ChildId == x.Id));
```

Create the functions with `ExecuteSqlRawAsync` after `EnsureCreated()` (or `migrationBuilder.Sql`) —
SQL Server syntax; other providers need their own dialect. For tree *endpoints*, materialize the rows
and assemble with `Regira.TreeList`'s `ToTreeList(...)` + `ToTreeView()`. In-memory-only trees skip the
SQL entirely — see `get_package("Regira.TreeList")`.

**See:** `get_package("Regira.Entities", section: "blueprints", heading: "Recursive entities")`.

### Manage ASP.NET Identity users through the entity pipeline
<!-- how_to: key=identity-user-entity aliases=identity,user,users,usermanager,accounts,claims -->
Copy the **Identity users blueprint**: implement
`IEntityRepository<AppUserEntity, string, TSearchObject, EntitySortBy, TIncludes>` over
`UserManager<TUser>` + the accounts DbContext (mapper bridges inner `IdentityUser` ↔ exposed model;
creates via `userManager.CreateAsync(item, password)`; claim diffs reconciled manually), register with
`e.HasRepository<...>()` + `e.UseEntityService<...>()`, and expose with the standard
`EntityControllerBase`. Query filters are typed on the **inner** IdentityUser type.

**See:** `get_package("Regira.Entities", section: "blueprints", heading: "Identity users")`.

### Serve read-only reference data as an entity (no table)
<!-- how_to: key=virtual-entity aliases=readonly,read-only,reference,static,countries,currencies,in-memory,lookup -->
Copy the **Virtual entity blueprint**: implement `IEntityService<T, TKey, SearchObject<TKey>>` over an
in-memory source (apply `Q`, ordering and `PagingInfo` yourself; write methods throw), then swap it in:

```csharp no-compile
services.For<Country, string>(e => e.UseEntityService<CountryRepository>());
```

The standard controller and SPA components work unchanged.

**See:** `get_package("Regira.Entities", section: "blueprints", heading: "Virtual entity")`.
