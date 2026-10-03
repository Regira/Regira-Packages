# Regira.Entities.Validation.FluentValidation

[FluentValidation](https://www.nuget.org/packages/FluentValidation) rules in the write pipeline of [Regira Entities](https://regira.github.io/Regira-Packages/src/Common.Entities/). `options.UseFluentValidation(assemblies)` in the `UseEntities()` callback registers every `AbstractValidator<T>` in the given assemblies (scoped, so a validator can take the `DbContext`) and adds `FluentEntityValidator`, an `IEntityValidator` that runs them when an entity is saved or removed. `EntityRuleSets` names the rule sets for add, modify and remove, and `GetOriginal()` / `GetOperation()` read the write from within a rule. A failure's message — a text or a translation key — goes out in the 400 with its placeholder values as args.

## Installation

```xml
<PackageReference Include="Regira.Entities.Validation.FluentValidation" Version="6.*" />
```

## Documentation

- [Validators](https://regira.github.io/Regira-Packages/src/Common.Entities/docs/built-in-features.html#validators) — registering FluentValidation, rule sets per operation, reading the stored row, and messages a client translates
- [Entity Validators](https://regira.github.io/Regira-Packages/src/Common.Entities/docs/services.html#entity-validators) — where the validator stage runs, which validators apply to an entity, and what a refusal leaves behind

## License

Regira Commercial License, with a free tier that applies automatically — no key is needed within its limits. A license key removes the limits and is validated fully offline. See the [license text](https://github.com/Regira/Regira-Packages/blob/main/legal/REGIRA-COMMERCIAL-LICENSE.md) and the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html); keys are available at [regira.com/licensing](https://regira.com/licensing).
