Bootstrap a new Regira consumer project from scratch. Follow these steps precisely and do not skip the checkpoint.

## Step 1 — Understand what is being built

If the user has not described the project, ask:
- What does this project do?
- Does it need authentication, and who signs in? (none / API key for machine callers / JWT for a SPA against your
  own user table / cookie session for a server-rendered app / Microsoft Entra ID or another OpenID Connect provider)
  Ask this explicitly — silence is not "none". The account surface (sign in, forgot, reset, change password) is
  first-class in both halves of the stack, and retrofitting it means re-scaffolding the SPA shell and every slice.
- Does it need to be deployable as a Windows Service?

Three more that are cheap to answer now and expensive to reverse later. Ask them together, and state the
default you will use if the user does not care:

- **The API route prefix** (`/api` or none). Four owners have to agree on it — the server convention, the
  SPA's `config.json`, its axios base and the dev proxy — so decide it before the first endpoint is
  proven, not after.
- **Which entity is the primary one**, if sample data is wanted, and roughly how many rows. "500 rows"
  without a named entity is a guess you will have to make on the user's behalf.
- **Whether a front end is in scope**, and at which tier (the default is the full reference scaffold).

## Step 2 — Choose the project template

Select exactly one template. Confirm the choice with the user before proceeding.

| Requirement | Template |
|---|---|
| Script, batch job, CLI utility | `ConsoleWithLogging` |
| Standard hosted API, no auth | `BasicApi` |
| Lightweight internal API, no auth | `SelfHostingApi` |
| Must run as a Windows Service | `SelfHostingApi` |
| API or app with any authentication (API key, JWT, cookie, Entra ID, OpenID Connect) | `SelfHostingApiWithAuth` |

The scheme is a registration choice on top of `SelfHostingApiWithAuth`, not a separate template.

## Step 3 — Select the minimum Regira package set

Inspect any existing `*.csproj` files first. Then choose the smallest package set that covers the user's stated needs. Use the package routing tables in `.regira/instructions/project.setup.md` if available, or the table in `.github/copilot-instructions.md`.

## Step 4 — Add packages and restore

Add the chosen `PackageReference` items to the `.csproj`. Then run:

```bash
dotnet restore
dotnet build
```

Report the outcome. If restore or build fails, diagnose and fix before continuing.

## Step 5 — Checkpoint (mandatory stop)

Stop here. Do not write any application code yet.

Report:
- Template chosen
- Packages added
- Whether restore and build succeeded
- Which guide files were extracted to `.regira/instructions/` (list them)

Then explicitly ask: **"Ready to continue and generate application code?"**

Only proceed to application code after the user confirms.

## Step 6 — Load extracted guides and generate code

Read every applicable primary guide in `.regira/instructions/` in full before writing any entity models, services, controllers, DI registrations, or infrastructure code. Skipping this step is a workflow violation.
