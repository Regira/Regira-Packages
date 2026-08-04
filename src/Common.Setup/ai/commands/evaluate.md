Run a structured code quality evaluation for a Regira module used in this project and write the report to `evaluations/`.

## Step 1 — Identify the module

Ask the user which Regira module to evaluate (e.g. `Entities`, `IO.Storage`, `Security`, `Office.PDF`).

## Step 2 — Load the module guide

Read the following files in full from `.regira/instructions/` before inspecting any code:
- `{module}.instructions.md`
- `{module}.signatures.md` (if it exists)

If the guide files do not exist, run `dotnet build` to trigger extraction first.

## Step 3 — Read the relevant application code

Read the parts of the application that use the module. Focus on:
- Service registrations and DI configuration
- Usage of Regira service interfaces
- Controller or endpoint implementations using Regira base classes
- Any custom extensions of Regira types

## Step 4 — Evaluate against these dimensions

### Convention adherence
- Does the application follow the patterns in the module's instructions guide?
- Are the correct base classes and interfaces used?
- Is DI registration following the established extension method pattern?

### Correctness
- Async-over-sync anti-patterns (`.Result`, `.Wait()`, `GetAwaiter().GetResult()`)
- Resource disposal (streams, connections, transactions)
- Edge cases in null handling or empty collections

### Security
- Input validation at system boundaries
- File handling risks if using IO or Office modules

### Guide accuracy
- Are there usage patterns in the code that the guide doesn't document?
- Are there guide claims that the application's usage contradicts?

### Development experience
The most actionable findings come from what the build got *wrong on the way to being right*, so record them
even where the final code is correct:
- **Every correction made during the build**, whether the user asked for it or the author caught it. An
  unattended run has no user corrections; its self-corrections are the equivalent signal, and each one is
  worth a line on how it could have been prevented (framework, documentation, MCP, prompt, or plain error).
- **Decisions taken at low confidence** — where the answer was settled by experiment or assumption rather
  than by reading. A question answered by a rebuild-and-curl is usually one sentence of documentation away
  from not being a question.
- **Documentation that was skipped, and what that cost.** Sections flagged as mandatory get skipped under
  context pressure; if something was re-derived that a section already covered, say which section.
- Keep this log **as the work proceeds**. Reconstructing it afterwards loses the hesitations, which are the
  part worth reading.

## Step 5 — Write the report

Write the evaluation to `evaluations/{module}-evaluation.md` using this structure:

```markdown
# {Module} Evaluation

**Date:** {date}
**Guide version:** {ai-v* tag or package version}

## Summary

{2-3 sentence overview}

## Findings

### Convention issues
{Numbered list}

### Correctness issues
{Numbered list}

### Security issues
{Numbered list}

### Guide gaps
{Patterns present in code not documented in the guide}

### Development experience
{Corrections made during the build and how each could have been prevented; decisions taken at low
confidence; documentation skipped and what it cost}
```

Report the file path when done.
