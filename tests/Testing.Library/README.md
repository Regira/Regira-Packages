# Testing.Library

Shared test models and data, not a test suite. It holds the Contoso school model (`Course`, `Department`, `Person`,
`Student`, `Instructor`, `Enrollment` and more, with attachments and normalized fields) built on
[Common.Entities](../../src/Common.Entities/README.md), its EF Core `ContosoContext`, and `TestData.Generate()`,
which returns a linked set of people, departments and courses. Smaller `Blogs` and `Farm` model sets sit beside it.

Referenced by `DAL.EFcore.Testing`, `Entities.DependencyInjection.Testing`, `Entities.Testing`, `Entities.TestApi`
and `Normalizing.Testing`. The project file is `_Testing.Library.csproj`; it targets net8.0 and net10.0 because
`Entities.Testing` runs on both. It builds with the solution and has nothing to run on its own.
