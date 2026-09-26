# Submarine UI design plan

Subject: Submarine, a self-hosted PVR that automates TV, anime and movie collecting (replaces Sonarr, Radarr and Prowlarr). Audience: home-server owners, technical, daily use on desktop, occasional phone. Primary job of the UI: show what is happening right now (downloads, upcoming episodes, what is missing) and let the owner change library and settings fast without reading docs.

Tone of the visual identity: a calm instrument panel below the surface. Deep blue-black water in dark mode, pale surface light in light mode, one teal "sonar" accent that marks live state and primary actions. Nothing glows, nothing floats; structure comes from rules and spacing.

## Tokens (`frontend/app/assets/css/tokens.css`, CSS variables, switched by `.dark` on `<html>`)
Light (default when system prefers light):
- `--bg: #F3F6F8` page, `--surface: #FFFFFF` raised panels/inputs, `--surface-2: #E9EEF2` hover/selected, `--line: #D9E1E8`, `--line-strong: #B9C6D2`
- `--fg: #132030`, `--fg-muted: #5B6C7D`, `--fg-faint: #8A98A6`
- `--accent: #137F73`, `--accent-fg: #FFFFFF`, `--accent-soft: #D7F0EC`
- `--ok: #137F73`, `--warn: #B77A12`, `--danger: #C7423E`, `--info: #2F6FD1`
Dark:
- `--bg: #0E1620`, `--surface: #152030`, `--surface-2: #1C2A3B`, `--line: #22334A`, `--line-strong: #33475F`
- `--fg: #DCE6F0`, `--fg-muted: #8CA0B5`, `--fg-faint: #5F7288`
- `--accent: #2FB7A8`, `--accent-fg: #06201C`, `--accent-soft: #163D3A`
- `--ok: #2FB7A8`, `--warn: #E0A636`, `--danger: #E5605B`, `--info: #6AA1F5`
Radii: `--r-control: 6px`, `--r-panel: 10px`, `--r-dialog: 14px`. Shadows: none in dark; light only `--shadow-pop: 0 8px 24px rgba(19,32,48,.12)` for popovers/dialogs. Focus ring: `0 0 0 2px var(--bg), 0 0 0 4px var(--accent)`.
Spacing scale 4px base: 4, 8, 12, 16, 24, 32, 48. Control height 36px; table row 44px; page gutter 24px (16px below 768px).

## Type
One family: IBM Plex Sans Variable (self-hosted via `@fontsource-variable/ibm-plex-sans`). Scale: 12 / 13 / 14 (body) / 16 / 19 / 23 / 28. Weights: 400 body, 500 controls and table headers, 600 page titles and section titles. Titles letter-spacing -0.01em. Numbers `font-variant-numeric: tabular-nums`. Line-height 1.5 body, 1.2 titles. Max line length 72ch for prose (overviews). No monospace anywhere. No all-caps labels. Sentence case for everything.

## Layout
```
≥1280px                                  768–1279px            <768px
┌──────────┬───────────────────────┐    ┌───┬─────────────┐    ┌───────────────┐
│ Submarine│ Page title   [action] │    │ ◉ │ title  [act]│    │ title   [act] │
│ ● live   ├───────────────────────┤    │ ◉ ├─────────────┤    ├───────────────┤
│ Series   │                       │    │ ◉ │             │    │               │
│ Movies   │  content, fluid       │    │ ◉ │  content    │    │  content      │
│ Calendar │  max-width 1920px     │    │ ◉ │             │    │               │
│ Activity │  centered on ≥2560px  │    │ ◉ │             │    ├───────────────┤
│ Wanted   │                       │    │   │             │    │ ◉  ◉  ◉  ◉  ⋯ │
│ Indexers │                       │    └───┴─────────────┘    └───────────────┘
│ Settings │                       │    rail 64px icons          bottom tab bar
│ System   │                       │
│──────────│                       │
│ health ● │                       │
└──────────┴───────────────────────┘
rail 232px
```
- Everything left-aligned. Page header: title (23/600) left, primary action right. Section titles 16/600 with a rule beneath; settings pages are rule-separated sections, not cards.
- Library pages: poster grid (auto-fill, min 150px, 2:3 ratio) or table view toggle; detail pages: backdrop strip (max 240px tall, muted 40% overlay) with poster + facts, then seasons/episodes table.
- Tables: full width, 44px rows, hairline row separators, sticky header, responsive: below 768px switch to stacked rows (label/value pairs), never horizontal scroll of the page.
- Dashboard hero (the one memorable element): a 7-day schedule strip (yesterday, today, +5) as columns; posters placed in the day they air; today column carries a 2px accent left rule; below it the live queue with slim (4px) accent progress bars and the health line. No stat tiles.
- Dialogs: 14px radius, max-width 560px (forms) / 960px (interactive search), full-screen sheet below 640px.

## Principles
1. Structure is information: rules and spacing group content; borders never decorate. One radius per hierarchy level.
2. Live state is the only thing that moves: progress bars, the hub connection dot, command progress. Menus/dialogs open in 120ms; respect `prefers-reduced-motion`.
3. Copy is directions: buttons name the outcome ("Add series", "Save changes", "Search"), empty states say what to do next, errors say what happened and how to fix it.
4. Density without clutter: 36px controls, 44px rows, 24px gutters; hide secondary metadata behind hover on desktop and behind expand on mobile.
5. Colour means status: teal = ok/live/primary, amber = attention, coral = failed/danger, slate = info/neutral. Never colour for decoration.
6. Quality floor: keyboard focus visible everywhere, WCAG AA contrast on both themes, no horizontal overflow from 480p to 4K, zero console warnings.

## Review against generic defaults (done before build)
- Rejected Inter/system-ui → Plex Sans is chosen for its instrument-panel character. Rejected cream+terracotta, black+acid green, card-kit with uniform shadows, eyebrow caps labels, middle-dot meta strings, arrows on buttons, monospace data labels.
- The memorable element is the schedule strip because the subject is time-based (air dates, release dates), not a big-number hero.
