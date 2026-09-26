# Submarine frontend

Nuxt 4 single page app for Submarine, a self-hosted library manager for series and movies. It talks to the Submarine API (`/api/v1`) and the events hub (`/hubs/events`).

## Commands

| Command | What it does |
| --- | --- |
| `pnpm dev` | Dev server on port 3000. API and hub requests proxy to `http://localhost:8989`. |
| `pnpm generate` | Static build into `.output/public`. |
| `pnpm lint` | ESLint (flat config, stylistic). |
| `pnpm typecheck` | vue-tsc across the project. |
| `pnpm test` | Vitest unit tests. |
| `pnpm e2e` | Playwright smoke suite. Set `E2E_BASE_URL` to a running API; without it the suite skips. |
| `pnpm generate:api` | Regenerates `app/types/api.d.ts` from `../backend/openapi.json`. |
| `pnpm screenshots` | Dev server plus mocked API, screenshots of 4 pages in 7 viewports and both themes into `.screenshots/`. |

## Structure

- `app/assets/css/tokens.css` is the only place colours exist. Light theme on `:root`, dark on `.dark` on `<html>`.
- `app/assets/css/main.css` maps the tokens into Tailwind 4 theme values.
- `app/components/ui/` holds the design system (`SButton`, `STable`, `SDialog`, and so on).
- `app/navigation.ts` is the single navigation registry used by the rail, the tab bar and the More sheet.
- Pages not implemented yet render through the `app/pages/[...slug].vue` placeholder until their feature slice lands.
- `DESIGN.md` is the binding design plan.
