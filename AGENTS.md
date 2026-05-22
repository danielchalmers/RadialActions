# AGENTS.md

This file is the root harness for coding agents working on Radial Actions. Keep it short, accurate, and operational. When a repeated mistake shows up, prefer adding a test, script, workflow check, or a tighter instruction here over adding broad prose.

## Project Map

- `RadialActions.sln` is the entry point.
- `RadialActions/` contains the WPF desktop app targeting `net10.0-windows`.
- `RadialActions.Tests/` contains xUnit v3 tests for deterministic behavior.
- `.github/actions/full-build/action.yml` defines the CI build: `dotnet build`, `dotnet test`, publish x64/arm64 binaries, zip them, and build MSI installers.
- `Package.wxs` defines the MSI packaging shape.
- `Settings.XamlStyler` is the XAML style configuration.

## Harness Principles

- Treat this file as a map, not an encyclopedia. Put durable behavior in code, tests, project files, README updates, or future focused docs when the repo grows.
- Make project knowledge repository-local. If an issue, review comment, release failure, or UI decision should guide future work, capture it in a versioned artifact.
- Prefer enforceable invariants. If an instruction becomes important enough to repeat, look for a test, analyzer, CI step, or small helper API that can enforce it.
- Work in small, reviewable changes. Keep unrelated cleanup out of feature and fix PRs unless it is necessary to complete the task safely.
- Optimize for agent legibility and human maintainability: clear names, predictable file locations, narrow helpers, and deterministic tests.

## Local Commands

Run commands from the repository root unless noted.

- Restore: `dotnet restore RadialActions.sln`
- Build: `dotnet build RadialActions.sln`
- Test: `dotnet test RadialActions.sln`
- Release publish example: `dotnet publish ./RadialActions/RadialActions.csproj -o publish/x64 -c Release -f net10.0-windows --os win --arch x64 --self-contained -p:PublishSingleFile=true -p:DebugType=embedded`

For documentation-only changes, explain why build or test commands were skipped. For code, project, packaging, workflow, or XAML changes, run the closest relevant command and then `dotnet test RadialActions.sln` before handing off.

## Working Loop

1. Start with `git status --short --branch` and inspect existing changes before editing.
2. Turn the request into concrete acceptance criteria. Include UI behavior, persistence behavior, startup behavior, and packaging behavior when relevant.
3. Search with `rg` before changing code so existing helpers and patterns are reused.
4. Make the smallest coherent edit. Avoid reformatting untouched files.
5. Add or update tests for changed deterministic behavior.
6. Run the relevant verification commands and record exactly what passed or why a command could not be run.
7. In PRs, describe the problem, the design choice, validation, risks, and follow-up work.

## Architecture Boundaries

- Keep app code in the `RadialActions` namespace unless there is a strong reason to introduce a narrower namespace.
- `RadialActions/Data/` holds action, hotkey, conversion, and Windows interop helpers. Keep parsing and validation logic pure where possible so it can be tested without global hooks or shell execution.
- `RadialActions/Services/` holds application services such as tray, hotkey, menu, and updates. Keep OS side effects isolated here or behind small helpers.
- `RadialActions/Pie/` owns radial menu layout, rendering state, visual construction, and theme snapshots. Deterministic geometry belongs in `PieLayoutCalculator` or a similarly testable helper.
- `RadialActions/Properties/Settings*.cs` owns persisted settings. When adding persisted fields, handle missing, null, or old values in normalization and cover serialization behavior in tests.
- XAML code-behind is acceptable in this app. Keep UI event handlers thin and move reusable calculations, parsing, and state transitions into testable C# helpers.

## Product Invariants

- Radial Actions is a lightweight Windows utility opened by a global hotkey. Startup, hotkey registration, tray behavior, and menu display should stay fast and predictable.
- User-configured actions must not execute during tests, settings load, preview rendering, or validation.
- Invalid or stale settings should normalize to safe defaults instead of crashing on startup.
- Shell actions must preserve explicit target, arguments, and working directory behavior.
- Key actions must accept known media/volume actions and validated custom shortcuts; invalid shortcuts should fail clearly.
- Update checks must remain controlled by settings and should not introduce surprise network work in deterministic tests.

## Testing Guidance

- Put tests in `RadialActions.Tests/` and follow the existing xUnit style.
- Prefer testing pure helpers and observable state over WPF window automation.
- Cover edge cases for hotkey parsing, settings normalization, action execution validation, and pie geometry.
- When fixing a UI bug, first ask whether the failing behavior can be represented as deterministic state, layout math, command selection, or serialization. If yes, test that layer.
- Do not write tests that require registering global hotkeys, launching external processes, opening URLs, or depending on the local desktop state.

## WPF And UI Guidance

- Preserve existing XAML names and bindings used by code-behind.
- Use the existing theme and layout patterns before introducing new resources.
- Keep visual changes inspectable: describe the expected before/after behavior and, when practical, manually run the app on Windows.
- Avoid hiding failures in broad `catch` blocks. If user-facing recovery is needed, log the exception and keep the fallback narrow.

## Dependencies And Packaging

- Be conservative with new dependencies. Prefer the .NET/WPF platform and existing packages unless a dependency removes meaningful complexity.
- If package versions change, verify restore, build, and tests.
- Packaging changes should consider both `x64` and `arm64` because CI publishes both.
- Do not change release tag behavior unless the release workflow is part of the task.

## PR Expectations

- Use polished branch names and PR titles that describe the work rather than the tool that produced it.
- Include a concise summary, validation results, and any residual risk.
- If follow-up work is recommended, explain what signal would justify it and what check or artifact should enforce it.
- Never claim a command passed unless it was run successfully in the current branch.

## Future Harness Upgrades

Keep this file as the only agent instruction file for now. If the repository grows enough that this file becomes crowded, split durable knowledge into focused docs and leave this file as the table of contents. Good candidates would be architecture notes, release mechanics, UI verification, and quality gates.
