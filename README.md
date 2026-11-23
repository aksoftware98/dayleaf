# 🍃 Dayleaf

A notebook for everything in your life. Organize it into **spaces**; each space holds one **page per date**. Every page is a printable A4 sheet you design freely — photo stickers, notes, checklists, washi tape, handwriting, page links — in fifteen paper themes (Field, Pastel, Sea, Sunset, Night, Kraft, Graph, Blossom, Matcha, Noir, Retro, Lavender, Ocean, Vintage, Citrus), with 16 space colours, 12 extra inks, 13 note papers, 12 washi tapes and 7 tape patterns.

Built with .NET 10 Blazor (Interactive Server), EF Core + SQLite, and a small vanilla-JS canvas engine. See [DAYLEAF_PLAN.md](DAYLEAF_PLAN.md) for the full spec.

## العربية · Arabic

Dayleaf speaks English and Arabic. Switch with **English / العربية** at the bottom of the sidebar (or on the sign-in page); your browser's language is used until you choose.

- The whole interface is translated and mirrors right-to-left.
- Each space and each page has its own **page language**. Arabic pages read right to left, print Arabic month and weekday names (Gregorian calendar, Western digits), and place new items on the right. An Arabic page stays Arabic in PDFs, shares and exports, whatever language the viewer uses.
- Arabic text anywhere (even on an English page) flows right to left on its own.
- Arabic fonts are built in and matched to each theme: IBM Plex Sans Arabic (body), Amiri (serif titles), Cairo (bold titles), Aref Ruqaa (handwriting).
- Search ignores hamza, taa marbuta, alef maqsura and tashkeel differences (احمد finds أحمد), and understands Arabic months in both naming styles: "يونيو 2025", "حزيران 2025".
- Spaces with Arabic names get Arabic addresses, e.g. `/s/يومياتي`.

## Run it

Open `Dayleaf.slnx` in Visual Studio 2022 (17.13+) or later and press F5, or:

```powershell
dotnet run --project src/Dayleaf
```

Then open http://localhost:5045. On first run the home screen offers **Open the sample notebook** (the Our Farm and Typing 365 pages from the mockups). You can also seed it from the command line:

```powershell
dotnet run --project src/Dayleaf -- --seed
```

Data lives in `%LOCALAPPDATA%\Dayleaf` (`dayleaf.db`, `media/`, `backups/`). Override with `Dayleaf:DataDirectory` in `appsettings.json` or `--Dayleaf:DataDirectory=<path>`. The database is backed up on every start (last 10 kept). Fonts are self-hosted in `wwwroot/fonts`, so pages print correctly offline.

## Password (needed before hosting anywhere but your own PC)

Without a password, Dayleaf only answers requests from this computer (anything else gets 403). To turn on the single-user password gate, make a hash:

```powershell
dotnet run --project src/Dayleaf -- --hash-password
```

and put it in `appsettings.json` (or an environment variable `Dayleaf__Auth__PasswordHash`):

```json
"Dayleaf": { "Auth": { "PasswordHash": "AQAAAA…", "SessionDays": 30 } }
```

With the gate on, everything needs sign-in except the login page, public share links (`/p/{token}`) and the photos on shared pages. Five wrong passwords lock sign-in for five minutes. Put it behind HTTPS (a reverse proxy) when it's on the internet.

## Test

```powershell
dotnet test
```

`tests/Dayleaf.E2E` drives the real app with Playwright and a fake clock (create space → page → photo → submit → +6 h → locked). It uses the installed Edge or Chrome; if neither can be launched it logs why and passes without running.

## The rules of the notebook

- A page is never closed: you can always **add a section**.
- A section is a **Draft** until you press **Submit section**. It then stays editable for **6 hours**, and after that it's **locked forever**. Drafts auto-submit after 24 hours.
- The page's title, date, theme and tags lock with its first section.
- Locks are enforced on the server and survive export/import.

## Editor cheat-sheet

| | |
|---|---|
| Move / resize / tilt | drag · corner handles (Shift keeps ratio) · round handle (Shift snaps 5°) |
| Write | double-click any text · click the page title |
| Photos | drop files on the paper, paste from clipboard, or use the Photo button |
| Keys | ← → previous/next day · arrows nudge (Shift = 10px) · Del deletes · Ctrl+Z undo · Ctrl+D duplicate · Ctrl+P print · Esc deselect |

## Export

- **PDF**: the **PDF** button makes the file on your computer (headless Edge or Chrome): exactly one full-bleed A4 page per sheet, no white borders. Whole spaces download as a book PDF from Space settings or the Library. Printing from the browser instead? Set margins to *None* and scale to *100%*.
- **HTML**: a single self-contained file per page.
- **.dayleaf**: a zip (`manifest.json`, `data.json`, `media/`) for the whole library, a space, or one page. Import from the Library screen.
