# Regira Consumer Project

Read `ai/AGENTS.md` (or `.regira/instructions/` if guides have been extracted) before taking any action. It is the authoritative routing guide for all code generation, package selection, and project scaffolding.

## MCP server

For full package discovery (including packages not yet installed), the Regira MCP server provides all guides at runtime:

```json
{ "mcpServers": { "regira": { "url": "https://mcp.regira.com/mcp" } } }
```

Add this to your AI tool's settings once. Then use `recommend_packages`, `search_packages`, or `get_package` to explore the full catalog before installing anything.

## Non-negotiable rules

1. **Never guess** — do not invent namespaces, method signatures, or package names. Stop and ask.
2. **Run the pre-flight checklist** in `ai/AGENTS.md` before generating any code.
3. **Load the relevant guide first** — do not write application code before reading the applicable `*.instructions.md` file.

## Slash commands

If Regira slash commands have been extracted to `.claude/commands/`, the following are available:

| Command | Purpose |
|---|---|
| `/new-project` | Bootstrap a new consumer project (template → packages → restore/build → checkpoint) |
| `/new-entity` | Scaffold a complete Regira entity with full CRUD |
| `/sync-guides` | Detect and refresh stale extracted guides in `.regira/instructions/` |
| `/evaluate` | Run a structured code quality evaluation for a module |
