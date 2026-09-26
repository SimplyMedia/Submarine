# Submarine contributor rules

## Layout
- `backend/` .NET 10 solution (`Submarine.slnx`): `src/Submarine.Core` (domain, parser, decision engine), `src/Submarine.Infrastructure` (EF Core, indexers, download clients, notifications), `src/Submarine.Api` (main app, serves the UI), `src/Submarine.Metadata` (Skyhook alternative), `src/Submarine.Mappings` (TheXEM alternative), `src/Submarine.Contracts` (shared DTOs), `tests/*`.
- `frontend/` Nuxt 4 app (Vue 3, TypeScript, Tailwind CSS 4, Pinia). Built output is copied into `Submarine.Api/wwwroot` by the Docker build.

## Frozen code
`backend/src/Submarine.Core/{Parser,Quality,Languages,Release,Validator,Util,Attributes,Provider,MediaFile}` and the tests under `backend/tests/Submarine.Core.Tests/{Parser,Quality,Validator,Util}` are the release title parser and its regression suite. They pin BluRay disc vs BDRip vs Remux detection, streaming service detection, languages, release groups and revisions. Do not refactor them. Any parser change must keep every existing test green and add a test for the new case.

## UI rules (apply to every UI change)
- Use the frontend-design skill for all UI work. The design plan (tokens, type, layout, principles) lives in `frontend/DESIGN.md`. Only use colours from `frontend/app/assets/css/tokens.css`.
- The UI is minimalist, simple, clean and modern, and supports light and dark mode.
- Before a UI change is done, verify it with screenshots in light and dark mode at all of these viewports and fix any horizontal overflow, clipped element or console warning:
  - 854x480 (480p)
  - 1280x720 (720p)
  - 1920x1080 (1080p)
  - 2560x1440 (1440p)
  - 3840x2160 (4K)
  - 1470x956 (MacBook Air M5 default)
  - 1440x900 (MacBook Pro M1 13" default)
- The browser console must be free of errors and warnings on every page.

## Verification
- Backend: `cd backend && dotnet build Submarine.slnx && dotnet test Submarine.slnx`.
- Frontend: `cd frontend && pnpm lint && pnpm typecheck && pnpm test`.
- End to end: `cd frontend && pnpm e2e` against a running API (`E2E_BASE_URL`).

## Text
Short sentences, sentence case, no em dashes, no hype words, no emoji.
