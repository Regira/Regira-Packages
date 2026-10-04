# Entities.TestApi

A runnable ASP.NET Core Web API built on [Regira Entities](../../src/Common.Entities/README.md), over the Contoso
model (departments, courses, persons, enrollments) from [Testing.Library](../Testing.Library/README.md). It is not a test suite but a complete
reference wiring: `UseEntities<ContosoContext>(o => o.UseDefaults())`, entity controllers from
[Entities.Web](../../src/Entities.Web/README.md) with search objects, sort and include enums, DTOs mapped by
[Entities.Mapping.AutoMapper](../../src/Entities.Mapping.AutoMapper/README.md), file-system attachments for courses
and persons, and an OpenAPI document with Scalar and Swagger UI. [Entities.Web.Testing](../Entities.Web.Testing/README.md)
hosts it in-process for its HTTP tests.

## Running

```bash
dotnet run --project tests/Entities.TestApi
```

The default `http` launch profile serves `http://localhost:5086` in the Development environment
(`--launch-profile https` adds `https://localhost:7133`). Browse `/scalar` for the Scalar UI or `/swagger` for
Swagger UI; the OpenAPI document is at `/openapi/v1.json`.

Startup does not create the database. `POST /test-data` creates it and seeds 5 departments, 50 courses and 10 course
attachments; `POST /db` creates it empty and `DELETE /db` drops it. The data lives in `regira-testapi.db` (SQLite,
foreign keys enforced) in the system temp directory, and attachments go to a new `testing/{guid}` folder there on
each start.

Routes: `/departments`, `/courses` and `/persons` (courses and persons with `/{id}/attachments`),
`/attachments/typed`, `/domain-actions`, and the minimal-API `/minimal/departments`.

No license key is needed: its 5 simple and 1 complex entity registrations fit the Entities free tier.
