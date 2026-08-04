Detect and refresh stale Regira guide files in the current project's `.regira/instructions/` directory.

## Step 1 — Check installed Regira packages

Run:
```bash
dotnet list package --include-transitive
```

Filter results to lines containing `Regira.`. Record each package name and its resolved version.

## Step 2 — Check the latest published AI guide version

Run:
```bash
git ls-remote --tags https://github.com/Regira/Regira-Packages "refs/tags/ai-v*"
```

Identify the latest `ai-v*` tag (e.g. `ai-v5.0.0`). Extract the version number.

## Step 3 — Compare

If the installed Regira package versions match the latest `ai-v*` tag version, guides are up to date. Report that and stop.

If the versions differ (or if no `ai-v*` tags are reachable), treat guides as potentially stale and continue.

## Step 4 — Refresh extracted guides

Delete the existing extracted files:
```bash
Remove-Item -Recurse -Force .regira/instructions
Remove-Item -Recurse -Force .claude/commands
```

Re-trigger extraction by running:
```bash
dotnet build
```

## Step 5 — Report

List the guide files now present in `.regira/instructions/` and command files in `.claude/commands/` and confirm they were refreshed.
