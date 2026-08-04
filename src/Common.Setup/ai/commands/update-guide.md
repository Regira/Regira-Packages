Identify undocumented patterns introduced by recent code changes and propose the appropriate guide update.

## Step 1 — Inspect recent changes

Run:
```bash
git diff HEAD
```

If files are staged, also check:
```bash
git diff --staged
```

Identify which module or area of the project the changed files belong to.

## Step 2 — Load the relevant guide

Check `.regira/instructions/` for a guide matching the changed area (e.g. `entities.instructions.md` for entity changes). Read it in full.

If no matching guide exists in `.regira/instructions/`, the change may not relate to a Regira module — note this and stop.

## Step 3 — Identify the gap

Compare the code changes against the loaded guide. Look for:
- New patterns or conventions the guide doesn't mention
- Existing guidance that the changes contradict
- New interfaces, methods, or types that aren't in the signatures file
- Workflow steps that are now out of order or incomplete

## Step 4 — Propose the update

**If the gap is substantial** (a new pattern, a corrected workflow step, a new type):
- Propose an exact diff/patch to the relevant `*.instructions.md` file in `.regira/instructions/`
- If method signatures changed, also propose a patch to `*.signatures.md`
- Present the proposal for human review before making any edits

**If the gap is small** (a one-line behavioral note, a caveat, a known pitfall):
- Note it inline as a comment or TODO in the relevant guide file
- Present it for human review before making any edits

## Rules

- Do not modify guide files without explicit user confirmation
- Do not remove existing guide content — only add or correct
