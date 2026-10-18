Refresh the Regira guides extracted into the solution's `.regira/instructions/` and the Regira commands in `.claude/commands/`.

A solution build copies only the files that are missing, so a file extracted from an older package stays until it is
deleted. This command deletes Regira's own files and builds the solution again, which extracts the installed packages'
versions. A Regira command you edited is replaced by the package's copy.

## Step 1 — Check installed Regira packages

Run from the solution root:
```bash
dotnet list package --include-transitive
```

Filter results to lines containing `Regira.`. Record each package name and its resolved version. With none installed,
report that and stop.

## Step 2 — Delete Regira's extracted files

`.regira/instructions/` holds only extracted guides. `.claude/commands/` also holds the solution's own commands, so only
the four Regira commands go.

PowerShell:
```powershell
Remove-Item -Recurse -Force .regira/instructions -ErrorAction SilentlyContinue
Remove-Item -Force .claude/commands/new-project.md, .claude/commands/new-entity.md, .claude/commands/sync-guides.md, .claude/commands/evaluate.md -ErrorAction SilentlyContinue
```

bash:
```bash
rm -rf .regira/instructions
rm -f .claude/commands/new-project.md .claude/commands/new-entity.md .claude/commands/sync-guides.md .claude/commands/evaluate.md
```

## Step 3 — Extract again

Build the solution file, from the solution root. The packages extract only in a solution build; a build of a single
project extracts nothing.
```bash
dotnet build <name>.slnx
```

Use `<name>.sln` for a classic solution file.

## Step 4 — Report

List the guide files now present in `.regira/instructions/` and the Regira command files in `.claude/commands/`, with
the package versions from Step 1, and confirm they were refreshed.
