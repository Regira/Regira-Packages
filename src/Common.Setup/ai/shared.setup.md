# Shared Setup for Regira AI Instructions

> **Role:** Load this file for setup rules reused by multiple module guides and for consumer-guide sync or distribution mechanics.
>
> **Boundaries:** Use [`AGENTS.md`](../../../ai/AGENTS.md) for module discovery, top-level routing, and the execution-oriented consumer workflow. Use the project setup guide — `get_package(id: "Regira.Setup", section: "project.setup")` — for project-template choice and baseline app shape.

Use this file when a module guide needs shared setup without repeating it.

## Consumer Local Guides

- **Source repository**: the full `ai/` folder is available locally, so this file can point to adjacent guides.
- **Consumer project**: the normal entrypoint is `./AGENTS.md`; extracted `.regira/instructions/*.md` files are optional local guidance.

Keep these responsibilities separate in consumer projects:

- `./AGENTS.md` decides project template, module selection, package routing, and when extracted local guides must be read before code generation.
- `.regira/instructions/*.md` are optional local guides created by package extraction.
- `Regira.Setup` is the package path for extracting the shared setup guides `project.setup.md` and `shared.setup.md` during build.
- Module packages that ship AI files can extract their own module-specific guides during build through their package targets.
- The `project.setup` section (`get_package(id: "Regira.Setup", section: "project.setup")`) stays focused on template selection and baseline app shape.
- `shared.setup.md` stays focused on setup rules reused across modules and local guide extraction behavior.

Consumers do not need the source-repository `ai/` folder for the normal one-file flow. Install `Regira.Setup` when the shared setup guides should be extracted locally through package restore and build.

## Related Files

- [`AGENTS.md`](../../../ai/AGENTS.md) — canonical downstream bootstrap to copy as `./AGENTS.md`
- **Project setup guide** — `get_package(id: "Regira.Setup", section: "project.setup")` — canonical shared project-template guide; extractable to `.regira/instructions/project.setup.md` through `Regira.Setup`

## Authoring Rules

When adding new instruction files:

1. Put shared setup here if more than one module needs it.
2. Keep module guides focused on routing, decision rules, pitfalls, and package-specific behavior.
3. Put examples, setup walkthroughs, and exact signatures in separate deep-reference files.
4. Do not duplicate the module catalog or project-template tables here.
