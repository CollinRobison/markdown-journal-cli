## Summary

-

## Why

-

## Changes

-

## Testing

- [ ] `dotnet build`
- [ ] `dotnet test`
- [ ] Manual CLI validation (if command behavior changed)

## Checklist

- [ ] Commands remain thin (validation + orchestration only)
- [ ] Business logic is in services
- [ ] New/changed services have interface + DI registration
- [ ] User-facing Spectre output uses `IAnsiConsole.MarkupLine()`
- [ ] User-provided strings rendered in markup use `.EscapeMarkup()`
- [ ] Tests added/updated for behavior changes
- [ ] Docs updated (README / docs / changelog) if needed

## Related

- Closes #
