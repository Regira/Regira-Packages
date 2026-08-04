# Entity Mapping

## EntityMapper

- A custom mapper should implement interface `IEntityMapper`
- A base class `EntityMapperBase` is provided with support for AfterMappers
- Built-in support for **AutoMapper** and **Mapster**.


## AfterMapper

- They can be configured **globally** (interface based) or for specific **entity** types
- After mappers decorate DTOs **after the mapping engine** completes
- A base class `EntityAfterMapperBase` is provided

```csharp
// interface
public interface IEntityAfterMapper
{
    bool CanMap(object source);
    void AfterMap(object source, object target);
}
public interface IEntityAfterMapper<in TSource, in TTarget> : IEntityAfterMapper
{
    void AfterMap(TSource source, TTarget target);
}
// base class
public abstract class EntityAfterMapperBase<TSource, TTarget> : IEntityAfterMapper<TSource, TTarget>
{
    public abstract void AfterMap(TSource source, TTarget target);
    public bool CanMap(object source)
}
```

## Convention-based mapping (Mapster)

When using **Mapster** (`options.UseMapsterMapping()`), the explicit `UseMapping<>()` and `AddMapping<>()` statements can be **omitted**. Mapster maps entities to and from their DTOs by convention, so as long as the entity and its DTOs share a **similar structure** (matching property names and types), the DTO type arguments on the controller are enough — no per-entity mapping registration is required.

You only need the explicit statements when:

- attaching an **AfterMapper** — `e.UseMapping<TDto, TInputDto>().After(...)` / `.AfterInput(...)`, or
- registering a **custom or additional** type pair that doesn't map cleanly by convention — `e.AddMapping<TSource, TTarget>()`.

> **AutoMapper** (`options.UseAutoMapper()`) does **not** map by convention: every entity ↔ DTO pair must be registered explicitly (each `UseMapping<>()` / `AddMapping<>()` call performs a `CreateMap`). Without it, AutoMapper throws a missing-type-map error.

## Configuring the mapping engine

Both `UseMapsterMapping(...)` and `UseAutoMapper(...)` accept an optional callback to configure the underlying engine globally:

```csharp
// Mapster — receives the shared TypeAdapterConfig
options.UseMapsterMapping(config =>
{
    config.Default.IgnoreNullValues(true);
    config.NewConfig<Order, OrderDto>()
        .Map(dto => dto.Total, order => order.Lines.Sum(l => l.Price));
});

// AutoMapper — receives the IServiceProvider and the IMapperConfigurationExpression
options.UseAutoMapper((sp, cfg) =>
{
    cfg.AddProfile<OrderMappingProfile>();
    cfg.CreateMap<Order, OrderDto>()
        .ForMember(dto => dto.Total, opt => opt.MapFrom(o => o.Lines.Sum(l => l.Price)));
});
```

Defaults applied by Regira (don't re-set these unless you mean to):

- **Mapster** enables `PreserveReference(true)` on the default config to prevent infinite recursion on circular references.
- **AutoMapper** sets `AllowNullCollections = true` and scans **no** profile assemblies automatically — register profiles and maps through this callback or the per-entity `UseMapping<>()` / `AddMapping<>()` statements.

> **Multiple `UseEntities<TContext>()` stacks** each calling `UseMapsterMapping()` share **one** `TypeAdapterConfig`, so every context's entity ↔ DTO mappings apply regardless of registration order. The `configure` delegates run eagerly in call order — the last call wins on conflicting settings.

## Dependency Injection

```csharp   
services
    .UseEntities<MyDbContext>(options => {
        // ...

        options.UseMapsterMapping();
        // or
        options.UseAutoMapper();

        // global AfterMapper — register a class implementing IEntityAfterMapper<TSource, TTarget>
        options.AddAfterMapper<MyAfterMapper>();
        // ...or inline, without a dedicated class:
        options.AfterMap<MyModel, MyDto>((source, dto) => { /*...*/ });
    })
    .For<Order>(e =>
    {
        // ...

        // With Mapster this UseMapping<>() is only needed for the After/AfterInput hooks;
        // the Order <-> OrderDto/OrderInputDto mapping itself works by convention.
        e.UseMapping<OrderDto, OrderInputDto>()
            .After((item, dto) => { /*...*/ })
            .AfterInput((dto, item) => { /*...*/ });
        
        // extra mapping config (only required when convention isn't enough, or for AutoMapper)
        e.AddMapping<OrderItem, OrderItemDto>();
        e.AddMapping<OrderItemInputDto, OrderItem>();
    });

```

## Overview

1. [Index](../README.md) — Overview of Regira Entities
1. [Entity Models](models.md) — Creating and structuring entity models
1. [Services](services.md) — Implementing entity services and repositories
1. **[Mapping](mapping.md)** — Mapping Entities to and from DTOs
1. [Web Endpoints](web-endpoints.md) — Exposing entity operations as HTTP endpoints
1. [Normalizing](normalizing.md) — Data normalization techniques
1. [Attachments](attachments.md) — Managing file attachments
1. [Built-in Features](built-in-features.md) — Ready to use components
1. [Checklist](checklist.md) — Step-by-step guide for common tasks
1. [Practical Examples](examples.md) — Complete implementation examples
