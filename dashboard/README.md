# Switchboard Dashboard

The management console for a Switchboard server. It talks to the management API under
`/_sb/v1.0` with a bearer token you provide on the connect screen, and it never persists anything
server-side of its own — everything it shows comes from the API.

## Running it

```bash
npm install
npm run dev        # Vite dev server on :5173, proxying /_sb to http://localhost:8000
npm run build      # production build into dist/
npm run preview    # serve the production build
npm run lint       # eslint, zero-warning gate
npm run test       # vitest unit/component tests
```

The dev server proxies the management API, so point a Switchboard server at `localhost:8000` (or
change the proxy target in `vite.config.js`) and connect with an admin token — `sbadmin` on a
fresh install.

## Stack

React 19, Vite 6, React Router 7, and the browser `fetch` API — no axios, no UI kit, no charting
library. State lives in three React contexts (`AuthContext`, `AppContext`, `OnboardingContext`).
Internationalization is `i18next` + `react-i18next`, and the request-activity chart is hand-rolled
SVG rather than a dependency.

## Layout

```
src/
  main.jsx, App.jsx            # entry + routes (PrivateRoute guards session restore)
  index.css                    # design tokens (light/dark) + base reset
  version.js
  i18n/                        # i18next setup, locale registry, formatters, en/de/ja/ar catalogs
  context/                     # AuthContext, AppContext, OnboardingContext
  hooks/                       # useFormatters
  utils/api.js                 # the single ApiClient (fetch), ApiError
  components/
    ui/                        # reusable library (see below) + co-located CSS
    common/                    # Sidebar, Topbar, navConfig, LanguageSelector, Toast, ...
    onboarding/                # SetupWizard, Tour
    views/                     # one file per route (Overview, History, Origins, ...)
  test/setup.js
```

## The component library (`src/components/ui`)

One import surface (`import { ... } from '../ui'`) for every reusable primitive: `DataTable` +
`TablePagination`, a portalled `ActionMenu` fed by the `entityActions` factory, `Modal` /
`ConfirmModal` / `JsonViewerModal`, `ActivityChart`, `Metric` / `PageHeader`, the `Badge` family,
`CopyButton` / `CopyableId`, `FilterBar`, `Segmented`, `Collapsible`, `EmptyState` / `ErrorBanner`,
and the `Icons` namespace. Build page-level views out of these rather than one-off markup.

## Conventions worth knowing

- **Tokens, not literals.** Colors, spacing, radii, shadows, control heights, chart series, and a
  z-index ramp are CSS variables in `index.css`, with a `[data-theme="dark"]` override block.
  Components reference the semantic aliases (`--color-surface`, `--color-text-muted`,
  `--color-accent`, …). Layout uses logical properties (`margin-inline`, `inset-inline-start`) so
  the Arabic RTL locale lays out correctly.
- **Every string is a key.** No hardcoded UI text — components call `t('...')` and format numbers,
  dates, durations, and bytes through `useFormatters()` so output follows the active locale. Add
  new strings to `src/i18n/locales/en/translation.json`; the other catalogs mirror its key set.
- **One client.** All HTTP goes through `ApiClient` in `utils/api.js`. A 401 anywhere dispatches
  `auth:unauthorized`, which drops the session.
- **Restart-required vs. live.** The settings editor tags each field the server reports in
  `restartRequiredSettings`; everything else applies on save. The restart control exits the server
  process and waits for it to come back (the container's restart policy brings it up).

## Internationalization

English, German, Japanese, and Arabic ship complete. The language selector is on the login screen
and in the topbar; the choice persists and sets `document.documentElement` `lang`/`dir`. Append
`?lang=cimode` to see raw keys, or `?lang=de` to force a locale. To add a language, add an entry to
`src/i18n/localeRegistry.js` and a catalog under `src/i18n/locales/<code>/`.
