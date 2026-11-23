# Dayleaf: Build Plan

> 🍃 A notebook for everything in your life. You organize it into **spaces** (topics), and each space holds **pages** indexed by **date**. Every page is a printable A4 sheet that looks good from the start.

This is a step-by-step plan to build Dayleaf locally with Claude Code. Work through it **one phase at a time**. Each phase ends with acceptance checks. Don't start the next phase until the current one builds, its tests pass and its checks are met.

Design mockups (the visual source of truth): https://claude.ai/code/artifact/84557956-0ed9-47f4-9a52-12f9eea4073d
Sample images: `D:\source\lifebase\images\`

---

## 0. Key decisions (read first)

### 0.1 Blazor Server or WebAssembly? → **Blazor Web App, Interactive Server**

| | Interactive Server ✅ | WebAssembly |
|---|---|---|
| Talks to SQLite | Directly through EF Core in the same process | Needs a separate Web API layer, DTOs and an HTTP client |
| Amount of code | About half | About double: API, client, shared contracts |
| Startup | Instant | Downloads the .NET runtime first (several MB) |
| Offline use | No (needs the server running) | Possible with a PWA |
| Fits "single user on my machine, SQLite for now" | Yes | Overkill |

**Decision:** use a Blazor Web App with **global Interactive Server** render mode. Keep all data access behind **service interfaces** (`ISpaceService`, `IPageService` and so on) so you can later move to `InteractiveAuto`/WASM, or put an API in front, without rewriting the UI.

The only exceptions are the **print** and **public share** routes. They render as **static SSR** (no circuit) so they load fast and print cleanly.

The one area where Server feels laggy is dragging and resizing (it would mean a server round-trip on every mouse move). **All pointer interactions run in JavaScript**, and .NET is only called once, on `pointerup`, with the final position.

### 0.2 Exporting and importing a notebook as a single file → **`.dayleaf` (a zip container)**

This is the same approach `.docx`, `.epub` and `.sketch` use: a zip file with JSON and the media files inside.

```
my-farm.dayleaf            (zip, Deflate)
├── manifest.json          { format:"dayleaf", version:1, kind:"library|space|page", exportedAt, app:"Dayleaf 1.0.0", counts{...} }
├── data.json              spaces → pages → sections → elements (see §4.4)
└── media/
    ├── 3f2a…e1.jpg
    └── 9b7c…04.png
```

Why this format:
- It's **one file** to back up, email or move between machines.
- It's **human-inspectable**: rename it to `.zip` and open it.
- It's **versioned**. `manifest.version` lets future importers migrate old files.
- Media is stored **once**, de-duplicated by SHA-256, and kept at full quality.
- The same format works for three scopes: the **whole library**, **one space** or **one page**.

### 0.3 Exporting a single page

| Format | How | Use |
|---|---|---|
| **PDF** | Static route `/print/page/{id}` with `@page { size: A4; margin: 0 }`. It opens the browser print dialog, where you choose **Save as PDF**. | Printing and sharing. The output matches the screen exactly because it's the same HTML and CSS. |
| **Self-contained HTML** | Server renders the page with CSS inlined and images embedded as `data:` URIs | A single file that opens anywhere, even offline (fonts fall back if offline) |
| **`.dayleaf` (kind = page)** | Same exporter, limited to one page | Moving a page into another notebook |
| *(later)* **Server-side PDF or PNG** | Headless Chromium (`Microsoft.Playwright`) rendering `/print/page/{id}` | One-click PDF without the print dialog, scheduled backups, API |

You can also print a **date range or a whole space as one book**: `/print/space/{id}?from=2024-08-28&to=2025-06-11`. It's the same component, with one A4 sheet per page.

### 0.4 Rules for locking ("you can add, but you can't change")

- A **page** is never "closed". You can always **add a new section** to it.
- A **section** is a batch of content added at one sitting (Morning, Afternoon, Evening…). Every element on a page belongs to one section.
- Section states:
  1. **Draft**: freely editable.
  2. **Submitted**: the user presses **Submit section**. It stays editable for a **grace window of 6 hours**, with a visible countdown.
  3. **Locked**: after `SubmittedAt + 6h`, the section can never be changed, moved or deleted.
- **Safety net:** a draft that's never submitted **auto-submits 24 hours after it was created**, so its lock happens at `CreatedAt + 24h + 6h`. Both values are configurable, and setting auto-submit to `0` disables it.
- **Page metadata** (title, date, theme, tags) can be edited only while the page's **first section** can be edited. After that it locks too.
- **Backfilling is allowed.** You can create a page for 28 Aug 2024 today. Locking depends on when content was **submitted**, not on the page's date.
- A page can have at most **one open draft** section at a time. New elements always go into that draft.
- Locks are **enforced on the server, in the service layer** (`SectionLockedException`), not only hidden in the UI. Imports **keep the original timestamps**, so locked content stays locked.
- Deleting: an element or section can be deleted only while it's editable. A page can be deleted only if none of its sections are locked. A whole space can be deleted only after **typing its name** to confirm.

Single source of truth:

```csharp
EffectiveSubmittedAt = SubmittedAt
    ?? (AutoSubmitAfter > 0 && now >= CreatedAt + AutoSubmitAfter ? CreatedAt + AutoSubmitAfter : null);
LockAt   = EffectiveSubmittedAt + EditWindow;      // null while still a draft
IsLocked = LockAt != null && now >= LockAt;
```

Use `TimeProvider` everywhere (no `DateTime.Now`) so tests can control time.

---

## 1. Tech stack

- **.NET 10**, Blazor Web App (`dotnet new blazor --interactivity Server --all-interactive --empty`)
- **EF Core 10 + SQLite** (`Microsoft.EntityFrameworkCore.Sqlite`), with migrations
- **HtmlSanitizer** (Ganss.Xss) for rich text
- **No UI component library.** The look is a custom notebook design, and Material or Bootstrap would fight it. Build a small **in-house component library** (§6) with plain CSS custom properties.
- **Vanilla JS module** `wwwroot/js/dayleaf.js` for the canvas (drag, resize, rotate, contenteditable, drop zone, keyboard shortcuts)
- **xUnit** tests, using in-memory SQLite (`DataSource=:memory:` with an open connection)
- **Playwright for .NET** (optional, for later phases) for end-to-end smoke tests and server-side PDF
- Fonts from Google Fonts: **Fraunces**, **Inter**, **Caveat**, **Space Grotesk**. Self-host them in `wwwroot/fonts` in Phase 9 so printing works offline.

---

## 2. Solution layout

```
lifebase/
├── CLAUDE.md                         ← project rules for Claude Code (see §11)
├── DAYLEAF_PLAN.md                   ← this file
├── Dayleaf.slnx
├── src/Dayleaf/
│   ├── Program.cs
│   ├── appsettings.json              ← "Dayleaf": { DataDirectory, EditWindowHours: 6, AutoSubmitDraftAfterHours: 24, MaxUploadMb: 20 }
│   ├── Domain/
│   │   ├── Entities.cs               Space, Page, Section, PageElement, MediaFile, ElementKind
│   │   ├── ElementData.cs            typed payloads per kind + JSON (de)serialization
│   │   ├── Themes.cs                 5 themes (ids, names, swatches)
│   │   ├── SpaceColors.cs
│   │   ├── SectionLockPolicy.cs      the rules in §0.4
│   │   └── DayleafOptions.cs
│   ├── Data/
│   │   ├── DayleafDbContext.cs
│   │   └── Migrations/
│   ├── Services/
│   │   ├── SpaceService.cs           CRUD, slugging, stats
│   │   ├── PageService.cs            pages, sections, elements and ALL lock enforcement
│   │   ├── TimelineService.cs        prev/next by date, month counts, on-this-day
│   │   ├── MediaService.cs           store/serve images, SHA-256 dedup
│   │   ├── SearchService.cs
│   │   ├── LayoutService.cs          "where does a new element go" + sheet overflow
│   │   ├── TextSanitizer.cs
│   │   ├── Export/DayleafExporter.cs
│   │   ├── Export/DayleafImporter.cs
│   │   ├── Export/DayleafFormat.cs   DTOs for manifest.json / data.json
│   │   └── Export/HtmlPageExporter.cs
│   ├── Endpoints/
│   │   ├── MediaEndpoints.cs         GET /media/{id}
│   │   └── ExportEndpoints.cs        GET /export/...  (file downloads)
│   ├── Components/
│   │   ├── App.razor  Routes.razor  _Imports.razor
│   │   ├── Layout/MainLayout.razor   (sidebar shell)   PrintLayout.razor (bare)
│   │   ├── Pages/                    Home, SpaceTimeline, PageScreen, Library, Search, SpaceSettings,
│   │   │                             PrintPage, PrintSpace, SharedPage, NotFound, Error
│   │   ├── Sheet/                    A4Sheet, SheetHeader, SheetFooter, ElementHost, element views per kind
│   │   ├── Editor/                   InsertRail, Inspector, SectionPanel, ThemePicker, StickerTray,
│   │   │                             PhotoStyleBar, PagePickerDialog, DropZone
│   │   └── Shared/                   Button, IconButton, Icon, Modal, Chip, SpaceCard, PageThumb,
│   │                                 MonthStrip, DateNav, EmptyState, Toast, ConfirmDialog
│   └── wwwroot/
│       ├── app.css                   tokens, app shell, components
│       ├── sheet.css                 A4 sheet + 5 themes + element styles + @media print
│       └── js/dayleaf.js
└── tests/Dayleaf.Tests/
    ├── SectionLockPolicyTests.cs
    ├── PageServiceLockTests.cs
    ├── ExportImportRoundTripTests.cs
    ├── ImporterSecurityTests.cs
    ├── TimelineServiceTests.cs
    └── LayoutServiceTests.cs
```

---

## 3. Design system (copy from the mockups)

### 3.1 App UI tokens (`app.css :root`)

```css
--app-bg:#F6F1E7; --app-panel:#FFFDF8; --app-sidebar:#F1EADC; --app-line:#E4DCCB; --app-line-strong:#D8CDB6;
--ink:#1E2320; --ink-2:#3C433E; --muted:#5B625C;            /* muted passes 4.5:1 on --app-bg */
--forest:#2E5A43; --forest-dark:#1F3F2F; --forest-tint:#EEF3EF;
--marigold:#C9861F; --marigold-tint:#F3E3C6; --marigold-ink:#6B4510;
--select:#2F7BEA;                                           /* selection handles only */
--radius-s:10px; --radius-m:14px; --radius-l:18px; --radius-xl:20px;
--shadow-card:0 10px 30px -18px rgba(60,45,20,.35);
--shadow-sheet:0 40px 70px -35px rgba(50,35,10,.6);
--font-display:"Fraunces",serif; --font-ui:"Inter",system-ui,sans-serif; --font-hand:"Caveat",cursive;
```

UI rules: touch targets are at least 44px, labels in small caps use `11px / 700 / .12em`, icons are inline stroke SVG (2px, `currentColor`) and never emoji. Emoji only appear as **user content**: space icons, stickers and chips.

### 3.2 The A4 sheet

- **794 × 1123 CSS px** (A4 at 96 dpi). Every coordinate is stored in these units.
- Content stays at least **40px inside every edge**, with **56px** side margins for text.
- Sheet 1 has a **template header**:
  - `SPACE · PAGE n` running head (11px, 700, .18em) with a 1.5px rule at y=70
  - the **day number**, huge (≈132px display font)
  - the **month** (italic display, 34px) and `YEAR · WEEKDAY` (13px, tracked)
  - **tag chips** at the top right
  - the **title** at y≈244 (display font, 52px, wraps to 2 lines max with `text-wrap: balance`)
- Free content starts around y≈380 on sheet 1 and y≈100 on later sheets.
- **Footer** on every sheet: a rule at y=1048, then `emoji Space` on the left and `Weekday · Mon d, yyyy · n` on the right.
- When a page overflows, add a sheet (`SheetCount++`). Sheet 2 and later show a small running head instead of the big date.

### 3.3 The five page themes (`sheet.css`, `.theme-*` sets CSS variables)

| Id | Paper | Ink | Accent / Accent 2 | Display font | Extras |
|---|---|---|---|---|---|
| `field` (default) | `#FBF7EF` + dot grid `#E3D9C6` every 24px | `#1E2320` | `#2E5A43` / `#C9861F` | Fraunces | tape `rgba(236,214,152,.78)`, note `#F6E7B8` |
| `pastel` | `#FFFFFF` + lilac top band `#ECE8FA` | `#26215C` | `#6E5BD0` / `#F2A3B5` | Space Grotesk 700 | rounded photos (30px), pink tape |
| `sea` | `#F3F8F8` | `#123E46` | `#1F6E7A` / `#E3B04B` | Fraunces italic | |
| `sunset` | `#FBEDE6` | `#3A1E16` | `#C2513A` / `#E8A23A` | Caveat 700 for the title | |
| `night` | `#1F2421` | `#F4EEDF` | `#E0B45A` / `#8FB9A0` | Fraunces | for printing, use a light-paper variant in `@media print` (no big dark floods) |

Each theme defines: `--paper --paper-pattern --ink --muted --accent --accent2 --chip-bg --chip-ink --tape --note-bg --font-title --font-body --font-hand --photo-radius`.

### 3.4 Photo sticker styles

| Style | Look |
|---|---|
| `plain` | No frame |
| `sticker` | 10px white border with a soft shadow (die-cut look) |
| `polaroid` | 9px white padding with a 38px bottom strip and the caption in the handwriting font |
| `taped` | The `sticker` look plus two translucent tape strips at the top corners, at −36° and +30° |
| `round` | Circle with an 8px white border; the aspect ratio is locked to 1:1 |

All photos use `object-fit: cover` and can be tilted from −20° to +20°. Default tilts are random within ±3° so pages look hand-made.

---

## 4. Data model

### 4.1 Entities

```csharp
Space   { Guid Id; string Name; string Slug (unique); string Emoji; string Color; string? Description;
          string DefaultTheme; Guid? CoverMediaId; DateTimeOffset CreatedAt; }
Page    { Guid Id; Guid SpaceId; DateOnly Date; string Title; string Theme; List<string> Tags (JSON);
          int SheetCount = 1; string? ShareToken; DateTimeOffset CreatedAt; }
          UNIQUE(SpaceId, Date)
Section { Guid Id; Guid PageId; int Seq; string Label; DateTimeOffset CreatedAt; DateTimeOffset? SubmittedAt; }
          UNIQUE(PageId, Seq)
PageElement { Guid Id; Guid PageId; Guid SectionId; ElementKind Kind; int Sheet;
          double X, Y, W, H, Rotation; int Z; string DataJson; string? PlainText;
          Guid? MediaId; Guid? LinkTargetPageId; DateTimeOffset CreatedAt, UpdatedAt; }
          INDEX(PageId), INDEX(LinkTargetPageId), INDEX(MediaId)
MediaFile { Guid Id; string ContentType; string Extension; string OriginalName; long Size; string Sha256 (unique); DateTimeOffset CreatedAt; }
```

- Store `DateTimeOffset` as **UTC ticks or ISO strings**, because SQLite can't order `DateTimeOffset` natively. Add a value converter.
- `Page Number` is **computed** (the number of pages in the space with `Date <= this.Date`), never stored. Backfilling renumbers pages naturally, because the date is the real index.
- `ShareToken` is 22 characters of base64url from `RandomNumberGenerator`.
- `PlainText` is kept up to date on every save, for search.

### 4.2 Element kinds and their `DataJson`

| Kind | Payload | Notes |
|---|---|---|
| `Text` | `{ html, size: s/m/l, font: body/display/hand, color: ink/accent/accent2/muted, align }` | Sanitized HTML: `b i u s br p ul ol li a[href]` only |
| `Heading` | `{ text, size }` | |
| `Photo` | `{ style, caption?, focusX, focusY }` | `MediaId` column |
| `Emoji` | `{ char }` | Size follows W |
| `Note` | `{ title?, html, color: yellow/pink/mint/sky/lilac }` | Sticky note, tilted 1.5° |
| `Checklist` | `{ title?, items:[{text, done}], color }` | Ticking boxes is an edit, so it follows the section's lock |
| `PageLink` | `{ }` | `LinkTargetPageId` column. Shows a live card (thumbnail, space, date, title). A missing target shows "Page not available". |
| `Quote` | `{ label, text }` | e.g. "THE DECISION / Replace, don't repair." |
| `Label` | `{ text, color }` | Handwritten caption |
| `Divider` | `{ style: line/dots/wave }` | |

Deserialize with `System.Text.Json` and a **polymorphic `ElementData`** type (`[JsonPolymorphic]`) or one record per kind. Unknown fields are ignored, which keeps the format forward-compatible.

### 4.3 Storage on disk

```
{DataDirectory}/                 default: %LOCALAPPDATA%/Dayleaf  (Windows) or ./App_Data
├── dayleaf.db
└── media/{guid:N}{ext}
```

Run migrations on startup (`db.Database.MigrateAsync()`). Before any migration runs, back up `dayleaf.db` to `backups/dayleaf-{yyyyMMddHHmm}.db`.

### 4.4 `data.json` (export format, version 1)

```json
{
  "spaces": [{
    "id": "…", "name": "Our Farm", "slug": "our-farm", "emoji": "🌳", "color": "marigold",
    "description": "…", "defaultTheme": "field", "coverMediaId": "…", "createdAt": "…",
    "pages": [{
      "id": "…", "date": "2026-11-16", "title": "Starting over, from the roots.", "theme": "field",
      "tags": ["🍂 Late autumn", "📍 Serghaya"], "sheetCount": 1, "createdAt": "…",
      "sections": [{ "id": "…", "seq": 1, "label": "Morning", "createdAt": "…", "submittedAt": "…" }],
      "elements": [{ "id": "…", "sectionId": "…", "kind": "photo", "sheet": 0,
                     "x": 50, "y": 384, "w": 460, "h": 310, "rotation": -2, "z": 3,
                     "mediaId": "…", "linkTargetPageId": null, "data": { "style": "taped" },
                     "createdAt": "…", "updatedAt": "…" }]
    }]
  }],
  "media": [{ "id": "…", "file": "media/….jpg", "contentType": "image/jpeg", "sha256": "…", "size": 123, "originalName": "…" }]
}
```

`ShareToken` is **never exported**: shared links don't travel with the file.

---

## 5. Routes

| Route | Mode | Screen |
|---|---|---|
| `/` | Interactive | Home: greeting, spaces grid, latest pages, **On this day** (same day and month in earlier years) |
| `/s/{slug}` | Interactive | Space timeline: banner, year pills, month strip with counts, date list, preview of the selected page with prev/next |
| `/s/{slug}/{yyyy-MM-dd}` | Interactive | **Page screen**: view and edit in one. The editor appears only if the page has an editable section. |
| `/s/{slug}/settings` | Interactive | Name, emoji, color, cover, default theme, export, delete (type the name to confirm) |
| `/library` | Interactive | Export everything or one space, import a `.dayleaf` file |
| `/search?q=` | Interactive | Search titles and text across spaces |
| `/print/page/{id}` | **Static SSR** | A4 sheets only, then `window.print()` |
| `/print/space/{id}?from&to` | **Static SSR** | Book print: every page in the range |
| `/p/{token}` | **Static SSR** | Public read-only page, with a Download PDF button |
| `GET /media/{id}` | Endpoint | Image bytes, `Cache-Control: public, max-age=31536000, immutable`, ETag = sha256 |
| `GET /export/library.dayleaf`, `/export/space/{id}.dayleaf`, `/export/page/{id}.dayleaf`, `/export/page/{id}.html` | Endpoint | File downloads (streamed with `ZipArchive` straight to the response) |

Mixing render modes in `App.razor`:

```razor
<HeadOutlet @rendermode="PageRenderMode" />
<Routes @rendermode="PageRenderMode" />
@code {
  [CascadingParameter] HttpContext HttpContext { get; set; } = default!;
  IComponentRenderMode? PageRenderMode =>
      HttpContext.AcceptsInteractiveRouting() ? InteractiveServer : null;
}
```

Mark the print and share pages with `[ExcludeFromInteractiveRouting]` and `@layout PrintLayout`.

---

## 6. Component library (build these before the screens)

| Component | Purpose |
|---|---|
| `Button` (variants: primary/secondary/ghost/danger; sizes) and `IconButton` (with a required `AriaLabel`) | |
| `Icon` | A `Name` parameter that maps to inline SVG paths (back, plus, calendar, link, print, photo, text, emoji, note, check, place, divider, undo, trash, rotate, crop, front, share, search, lock, clock) |
| `Modal`, `ConfirmDialog`, `Toast` (a toast service through DI) | |
| `Chip` | Mood, location and status chips |
| `SpaceCard` | Cover image, emoji badge, name, page count and date range, status pill |
| `PageThumb` | Renders the **real** `A4Sheet` inside a box scaled with `transform: scale(w/794)`, with `loading="lazy"` images. One rendering path means thumbnails are always correct. |
| `MonthStrip` | 12 bars sized by page count, highlighted when selected, clickable to filter |
| `DateNav` | ← previous date · current · next date / Today + |
| `A4Sheet` | Paper, theme class, header (sheet 1), footer, elements. A `Mode` parameter: `View`, `Edit` or `Print`. |
| `ElementHost` | Absolute wrapper (`left/top/width/height/transform:rotate/z-index`), `data-el-id`, `data-editable`, `data-kind`, and selection handles while selected and editable |
| `TextElement`, `PhotoElement`, `EmojiElement`, `NoteElement`, `ChecklistElement`, `PageLinkElement`, `QuoteElement`, `LabelElement`, `DividerElement` | |
| `InsertRail` | Left rail: Text, Photo, Emoji, Note, Checklist, Quote, Link page, Divider |
| `Inspector` | Right panel. With nothing selected: page settings (theme, tags) and sections. With an element selected: its properties (size and tilt sliders, photo style, caption, colors). |
| `SectionPanel` | Lists sections. Each shows its label, time, a state badge (Draft / ✏️ 5h 12m left / 🔒 Locked), the **Submit section** button and **+ Add section** |
| `ThemePicker` | Five "Aa" cards plus Shuffle |
| `StickerTray` | Curated emoji grid (nature, farm, work, celebration, weather, feelings) plus search |
| `PhotoStyleBar` | Floating dark toolbar above the selected photo: style buttons, rotate, bring to front, delete |
| `PagePickerDialog` | Search pages by title or date across spaces (also used for @-links) |
| `DropZone` | Drag files from the desktop onto the sheet → upload → photo placed where they were dropped |

---

## 7. JavaScript module (`wwwroot/js/dayleaf.js`)

Import it with `IJSObjectReference` inside the page screen. Export these functions:

- `initCanvas(rootEl, dotNetRef)`: one delegated `pointerdown` listener on the sheets container.
  - **Move**: drag `[data-editable=true]` elements, updating `style.left/top` live.
  - **Resize**: corner handles. Lock the aspect ratio for `Photo(round)`, `Emoji` and with Shift.
  - **Rotate**: top handle using `atan2`, snapping to 0° and ±5° steps with Shift.
  - **Scale**: compute `scale = sheet.getBoundingClientRect().width / 794` so zoom works.
  - **Clamp** elements so at least 24px stays on the sheet.
  - On `pointerup`: `dotNetRef.invokeMethodAsync('CommitTransform', id, sheet, x, y, w, h, rot)`, but **only if something changed**. Swallow the `click` that follows a drag.
  - Dragging onto another sheet changes the `sheet` index.
- `beginTextEdit(el)` and `endTextEdit(el)`: toggle `contenteditable`. Ctrl+B/I/U work natively. Paste as plain text. On blur → `CommitText(id, innerHTML)`.
- `initShortcuts(dotNetRef)`:
  - ← / → for the previous or next page (not while typing)
  - Delete / Backspace to delete the selected element
  - Esc to deselect
  - Ctrl+Z to undo the last transform in this session (keep a small stack in .NET)
  - Arrow keys with an element selected nudge it 1px (10px with Shift), then commit
- `initDropZone(el, inputFileEl)`: show an overlay on `dragover` and forward dropped files to the hidden `InputFile` (via `DataTransfer`) along with the drop coordinates.
- `printNow()` and `copyToClipboard(text)`.

Blazor must **not** re-render an element while it's being dragged or edited. Rendering is keyed by `@key=element.Id`, and the .NET model is updated after the commit, so the next render matches what's on screen.

---

## 8. Services (where the rules live)

### `PageService`: every mutation goes through here

```
Task<Page> GetOrCreatePageAsync(spaceId, DateOnly date, string? title)   // creates section #1 (Draft) with a time-of-day label
Task<Section> AddSectionAsync(pageId, string? label)                    // fails if a draft is already open
Task SubmitSectionAsync(sectionId)
Task<PageElement> AddElementAsync(pageId, ElementKind, ElementData, Guid? mediaId, Guid? linkTarget, (x,y)? at)
Task UpdateElementDataAsync(elementId, ElementData)
Task TransformElementAsync(elementId, sheet, x, y, w, h, rot)
Task BringToFrontAsync(elementId)
Task DeleteElementAsync(elementId)
Task UpdatePageMetaAsync(pageId, title, theme, tags, DateOnly? newDate) // meta lock = first section's lock
Task AddSheetAsync(pageId) / RemoveEmptyLastSheetAsync(pageId)
Task<string> EnableShareAsync(pageId) / DisableShareAsync(pageId)
Task DeletePageAsync(pageId)                                            // only if no section is locked
```

- Every element mutation loads its section and calls `SectionLockPolicy.EnsureEditable(section, now)`. If the section is locked, it throws `SectionLockedException` with a friendly message.
- New elements always go into the page's **open draft**. If there's no draft but a submitted section is still in its grace window, ask the user: "Add to Morning (still editable) or start a new section?"
- Sanitize text on every save and recompute `PlainText`.
- Changing the date checks the `(SpaceId, Date)` uniqueness and shows a friendly error.

### `LayoutService`: placing new elements

1. If the user chose a position (a drop or a click with a tool), use it.
2. Otherwise, place the element 16px under the lowest element on the last sheet, aligned left at x=56, inside the content box.
3. If it doesn't fit above y=1123−90, add a new sheet and place it at y=100.
4. Default sizes: Text 440×120 · Photo 460×310 (use the image's aspect ratio, max 460 wide) · Emoji 64×64 · Note 210×190 · Checklist 220×200 · PageLink 440×150 · Quote 440×80 · Label 200×60 · Divider 682×24.

### `MediaService`

- Upload through Blazor `InputFile`, calling `file.RequestImageFileAsync(sameFormat, 2400, 2400)` (the browser resizes it). Keep PNG and WebP as they are so stickers keep their transparency, and convert everything else to JPEG.
- `OpenReadStream(maxAllowedSize: MaxUploadMb)`, then SHA-256 → if the hash already exists, reuse that record.
- Check the file signature (JPEG, PNG, WebP, GIF magic bytes). Don't trust the extension.
- Orphan cleanup: `CleanupOrphansAsync()` removes media that nothing references (run it from the Library screen).

### `TimelineService`

`GetPrevNext(spaceId, date)`, `GetMonthCounts(spaceId, year)`, `GetYears(spaceId)`, `GetPageNumber(pageId)`, `OnThisDay(today)`, `GetLatest(n)`, `GetBacklinks(pageId)`.

---

## 9. Export and import details

### Exporter

- Writes straight to the response stream with `ZipArchive(Create, leaveOpen)`. Never builds the whole file in memory.
- `manifest.json` goes first, then `data.json`, then `media/*`, using `CompressionLevel.Optimal` for JSON and `NoCompression` for images (they're already compressed).
- File names: `dayleaf-library-2026-11-23.dayleaf`, `our-farm-2026-11-23.dayleaf`, `our-farm-2026-11-16.dayleaf` (page).

### Importer

Treat the file as **untrusted input**:

- Reject the file if it's over 2 GB, has more than 20,000 entries, or any entry's uncompressed-to-compressed ratio is above 100 (zip-bomb protection).
- **Only** read `manifest.json`, `data.json` and the `media/` paths that `data.json` lists, and validate those paths against the pattern `^media/[0-9a-f]{32}\.(jpg|png|webp|gif)$`. **Never** extract by an entry's own name (zip-slip protection).
- Check `manifest.format == "dayleaf"` and `version <= 1`. Newer versions get a clear message.
- Give **every** id a new GUID with a remap table. Remap `sectionId`, `mediaId`, `coverMediaId` and `linkTargetPageId`. A link to a page that isn't in the file stays pointed at the original id if that page exists locally; otherwise it becomes `null`.
- De-duplicate media by SHA-256 against existing media.
- **Re-sanitize** all HTML and clamp all coordinates to the sheet.
- **Keep** `createdAt` and `submittedAt` exactly, so locks survive the round trip.
- Conflicts:
  - **Library or space import**: always creates new spaces. If the name already exists, add the suffix "(imported 2026-11-23)" and use a unique slug.
  - **Page import**: the user picks a target space. If a page already exists on that date, refuse and say which page is in the way.
- Run everything in **one DB transaction**, writing media files to a temp folder and moving them into `media/` only after the commit. If anything fails, roll back and delete the temp files.
- Show a summary: "Imported 1 space, 444 pages, 1,203 photos."

### HTML page export

Render `A4Sheet` in `Print` mode to a string with `HtmlRenderer`. Inline `sheet.css`, turn each `<img src="/media/…">` into a `data:` URI, and keep the Google Fonts `<link>`.

---

## 10. Phases

Each phase lists the prompt to give Claude Code (prefix every prompt with *"Read DAYLEAF_PLAN.md and CLAUDE.md first."*) and its acceptance checks.

### Phase 1: Skeleton and data layer
**Prompt:** "Do Phase 1: create the solution, the projects, the domain entities, the DbContext with the indexes and converters from §4, the first migration, the options binding and the `SectionLockPolicy` with full unit tests."

- [ ] `dotnet build` and `dotnet test` pass
- [ ] Lock tests cover: draft editable; submitted editable at 5h59m; locked at exactly 6h; a draft auto-submits at 24h and locks at 30h; `AutoSubmit=0` keeps a draft editable forever; meta lock follows the first section
- [ ] The app starts and creates `dayleaf.db` and `media/`

### Phase 2: Services
**Prompt:** "Do Phase 2: implement SpaceService, PageService, LayoutService, TimelineService, MediaService, TextSanitizer and SearchService per §8, with tests."

- [ ] Every PageService mutation on a locked section throws `SectionLockedException` (test each one)
- [ ] One page per date per space, and only one open draft per page
- [ ] Page numbers are correct after backfilling an older date
- [ ] Sanitizer strips `<script>`, `on*` attributes and `javascript:` links

### Phase 3: Design system and the A4 sheet
**Prompt:** "Do Phase 3: build app.css tokens, sheet.css with the 5 themes and photo styles, the shared components in §6 and the read-only `A4Sheet` with every element kind. Add a dev-only seed (`--seed`) that creates the Our Farm page 444 and Typing 365 page 201 from the mockups using the images in /images."

- [ ] The seeded farm page matches the mockup's **A4 · Our Farm · Page 444** artboard
- [ ] Every theme can be switched on a page without layout breaking
- [ ] `PageThumb` renders the same sheet, scaled

### Phase 4: Home and space timeline
**Prompt:** "Do Phase 4: MainLayout with sidebar, Home and the Space timeline screens per the mockups (artboards 1 and 2), plus create and edit space dialogs."

- [ ] Clicking a month filters the list. Year pills appear only for years that have pages.
- [ ] ← / → and DateNav move one page at a time by date
- [ ] Empty states: a space with no pages, and an app with no spaces ("Create your first space")

### Phase 5: Page editor
**Prompt:** "Do Phase 5: the page screen editor (mockup artboard 3): InsertRail, Inspector, SectionPanel, PhotoStyleBar, StickerTray, PagePickerDialog, DropZone, and dayleaf.js per §7."

- [ ] Drag, resize and rotate are smooth and stay in place after a reload
- [ ] Dropping 3 photos from the desktop places 3 photo stickers where they were dropped
- [ ] Double-click text to edit it; bold and italic work; pasting strips formatting
- [ ] A locked section's elements show 🔒 on hover, can't be selected for editing, and the inspector explains why
- [ ] The countdown updates live: "Editable for 5h 12m"
- [ ] Overflow adds sheet 2 automatically, and the "+ Add sheet" button works

### Phase 6: Sharing, references and backlinks
**Prompt:** "Do Phase 6: share toggle, public `/p/{token}` page, PageLink element with live card, backlinks list in the inspector, and the mockup artboard 4 share panel."

- [ ] Turning sharing off makes the old link return 404
- [ ] A link to a deleted page renders a "Page not available" card without crashing

### Phase 7: Print and export
**Prompt:** "Do Phase 7: `/print/page/{id}`, `/print/space/{id}` book print, the `.dayleaf` exporter, the importer with every safeguard in §9, the HTML page export, and the Library screen."

- [ ] Chrome → Save as PDF gives exactly one A4 page per sheet, with no margins, scrollbars or UI
- [ ] Round trip: export the library → import it into an empty DB → the data is identical, apart from new ids (a test compares a normalized JSON of both)
- [ ] Locks survive the round trip
- [ ] Security tests: zip-slip entry ignored, zip bomb rejected, unknown version rejected, `<script>` in data.json stripped

### Phase 8: Search, On this day, polish
- [ ] Search highlights matches and supports `2025-06` and `june 2025` style date queries
- [ ] Toasts for saves and errors; a reconnect UI styled like the app
- [ ] Keyboard and screen reader: every control has a label and visible focus rings

### Phase 9: Hardening
- [x] Self-host the fonts. Back up the DB on startup (keep the last 10).
- [x] An optional simple **password gate** (cookie auth with a single user), required before any public hosting. Without auth the app should only listen on `localhost`.
- [x] An optional Playwright test: create a space → page → photo → submit → advance the clock 6h (fake `TimeProvider`) → locked

---

## 11. `CLAUDE.md` to put at the repo root

```markdown
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
```

---

## 12. Later (not in v1)

- **InteractiveAuto or a PWA** for offline use, made possible by keeping services behind interfaces
- **Sync** to a server (Postgres) with multiple devices, using the `.dayleaf` format as the sync unit
- **Server-side PDF or PNG** with Playwright, and scheduled backups to OneDrive or Google Drive
- **Mobile layout** for the editor: a bottom sheet instead of side panels, and quick capture "Add a photo to today's page"
- **Inline @-mentions** inside text (chips) in addition to PageLink cards
- **Templates** per space, e.g. "Release day" and "Harvest log"
