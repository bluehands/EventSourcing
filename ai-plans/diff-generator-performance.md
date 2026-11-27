# Diff generator IDE performance

Status: proposed, not started. Do this after `diff-fixes.md`.

## Problem

`DiffGenerator.Initialize` uses `hosts.Collect().Combine(context.CompilationProvider)`. On every edit, the generator:

- rebuilds every comparison class from scratch,
- scans all types of the assembly (`AllTypes`) once per comparison class to find subtypes,
- re-adds every generated source, even when unchanged.

This is fine for small projects but can slow typing in large ones.

## Steps

1. **Measure first.** Add a test that builds a synthetic compilation (about 2,000 types and 50 comparison classes) and times repeated `RunGenerators` calls after small edits. Enable `GeneratorDriverOptions(trackIncrementalGeneratorSteps: true)` to see which steps rerun. Record the baseline.
2. **Cache a comparable model per comparison class.** Change the pipeline to `hosts.Combine(CompilationProvider).Select(BuildModel)`, where the model contains only value-equatable data: host name/namespace/accessibility, generated method bodies as strings, and diagnostics stored as descriptor id, message arguments, file path and text/line spans (no `Location`, `ISymbol` or `Compilation`). Use equatable arrays. `RegisterSourceOutput` then only reports diagnostics and adds sources; when the model is unchanged, Roslyn skips the output step and the generated tree stays identical.
3. **Scan for subtypes once per compilation.** Build one shared base-to-derived index per compilation (`CompilationProvider.Select`) instead of one scan per comparison class.
4. **Only if measurements still justify it:** discover subtypes from syntax (type declarations with base lists via `SyntaxProvider`), so edits inside method bodies trigger no type scan.

## Acceptance

- Editing a method body leaves the output step `Cached`/`Unchanged`.
- Adding a subtype in another file, or changing a property type in another file, regenerates the output.
- The existing `Regenerates_when_a_property_changes_on_the_same_driver` test still passes.
- Timings are recorded against the baseline in this file.

## Risks

An incomplete model equality produces stale generated code. The acceptance tests above guard against it. Runtime allocation tuning (`diff-improvements.md`, item 5) is a separate topic.
