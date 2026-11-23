# Dayleaf: rules for Claude Code
- Plan: DAYLEAF_PLAN.md is the spec. Do one phase at a time, and stop at the end of a phase.
- Stack: .NET 10 Blazor Web App, Interactive Server (global); static SSR only for /print and /p.
- All data access goes through services in /Services. Components never touch DbContext.
- Lock rules (§0.4) are enforced in PageService. Never bypass them, not even in the UI or the importer.
- Time: always inject TimeProvider. Never use DateTime.Now or UtcNow directly.
- Canvas units are A4 CSS px (794×1123). Pointer interactions live in wwwroot/js/dayleaf.js and commit once on pointerup.
- Styling: CSS variables from app.css and sheet.css only. No UI framework, no Bootstrap. Icons are inline SVG; emoji only as user content.
- Accessibility: real <button>/<a>/<label>, aria-label on icon-only buttons, 44px targets, 4.5:1 contrast.
- After each change, run: dotnet build && dotnet test. Fix warnings in files you touched.
- Never commit dayleaf.db, media/ or *.dayleaf files (add them to .gitignore).

## Where things live
- `src/Dayleaf/Domain` — entities, typed element payloads (`ElementData.cs`), themes, `SectionLockPolicy` (single source of truth for locks).
- `src/Dayleaf/Services` — `PageService` (every mutation + lock enforcement), `LayoutService`, `TimelineService`, `SearchService`, `MediaService`, `SeedService`, `Export/*` (.dayleaf exporter/importer, HTML export).
- `src/Dayleaf/Components/Sheet/A4Sheet.razor` — the one rendering path (editor, thumbnails, print, share, HTML export). It has no Blazor event handlers; the editor talks to it through `dayleaf.js`.
- `src/Dayleaf/Components/Pages/PageScreen.razor` — the editor; JS calls back via `[JSInvokable]` methods (`CommitTransform`, `CommitText`, `CommitTitle`, `ToggleCheck`, `DropSticker`, …).
- Auth: `PasswordGate` + cookie auth, on only when `Dayleaf:Auth:PasswordHash` is set (fallback policy = signed-in user). New public routes need `[AllowAnonymous]`; without a password, non-loopback requests get 403.
- E2E: `tests/Dayleaf.E2E` (Playwright + `WebApplicationFactory.UseKestrel` + `FakeTimeProvider`). Wait for interactivity before clicking prerendered buttons.
- Razor gotcha: never put a string literal with double quotes inside a double-quoted Razor attribute (`@onclick="() => F("x")"` breaks); use a constant or a helper.
