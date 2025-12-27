# Agent Guidelines

These instructions apply to Codex and other coding agents working in this repo.

## Scope
- This repo is a WPF desktop app (XAML + C#).
- Preserve existing MVVM patterns, bindings, and XAML style conventions unless asked to refactor.
- Media playback uses `FFME.Windows`; logging uses `Serilog`.

## XAML
- Keep XAML readable: group related properties and follow `Settings.XamlStyler`.
- Use existing resources/styles before adding new ones.
- Prefer bindings and converters over code-behind for UI state.
- Avoid introducing third-party UI frameworks unless explicitly requested.

## C#
- Follow `.editorconfig` naming and formatting rules.
- Prefer `ObservableProperty`/`RelayCommand` (CommunityToolkit.Mvvm) where already used.

## WPF-specific considerations
- Be mindful of dispatcher usage when touching UI from background tasks.
- Prefer `INotifyPropertyChanged` patterns over manual UI updates.
- Use `IValueConverter`/`IMultiValueConverter` for presentation logic.

## Testing/Validation
- Always run `dotnet format`.
- If you change UI behavior, mention how to verify it (e.g., which view to open, what to click).
