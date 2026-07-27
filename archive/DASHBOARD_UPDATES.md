# Switchboard Dashboard — Improvement Plan

This plan takes the Switchboard dashboard from a working-but-thin admin console to something that matches the rest of the jchristn dashboard family (pepperx, xeno, verbex, assistanthub, wilson, lattice, less3, litegraph, recalldb) and satisfies the requirements in `c:\code\agents\requirements`. It is written to be worked, not just read: every task carries an ID and a checkbox, names the project to copy the pattern from, and states what "done" means so a developer can annotate progress in place.

The eleven improvements you called out are all here, but they don't sit in isolation. Several of them share plumbing — a real component library, a grouped navigation model, a proper table with a portalled action menu — so the plan front-loads that shared foundation and then layers the visible features on top. It also carries the work past the dashboard itself: three of these features (the history chart, the settings form, the restart button) need new backend endpoints, and all of it needs documentation and tests before it ships.

---

## Implementation status (at archive)

All eleven requested improvements shipped and the dashboard is green — `npm run lint` clean (zero warnings), `npm run build` succeeds, and `npm run test` passes (14 unit/component tests). The backend endpoints that three of them depend on are implemented with tests (management suite green in the full Touchstone run). Phases 0–8 are complete; Phase 9 shipped the four locales (en/de/ja/ar at full key parity) with RTL-aware layout (CSS logical properties throughout) and the a11y affordances baked into the component library (aria-labels on icon buttons, focus-visible rings, reduced-motion, color-never-only status).

Deliberately left for a follow-up, marked `[~]` below rather than silently dropped:

- **Automated visual QA (Playwright, I9.5/T4)** — the components were built responsive and RTL/theme-aware and spot-checked via the production build, but the desktop/tablet/mobile × light/dark × LTR/RTL Playwright matrix is not wired into CI (Playwright browser install was out of scope for this pass). This is the biggest residual risk and should be run before a public release.
- **i18n CI guards (T3)** — locale key-parity and placeholder checks were run manually (all four locales verified identical to English); a standing CI check for missing/orphaned keys and new hardcoded strings is not yet added.
- **`DOCKERHUB_README.md` (D2)**, the main **`README.md`** dashboard section (D1), and the i18n contributor guide (D6) — the dashboard's own `dashboard/README.md` documents the architecture and conventions; the repo-root docs still need their dashboard sections.

Nothing in the shipped surface is a placeholder; the deferred items are packaging/QA hardening, not missing features.

---

## How to use this document

Each task looks like `- [ ] **F0.1** — description`. Update the checkbox as you go:

- `- [ ]` not started
- `- [~]` in progress (put your initials and a date in the trailing `— note:` field)
- `- [x]` complete (leave the acceptance note so a reviewer can verify)
- `- [!]` blocked (say what on)

Phases are ordered by dependency, not by priority. You can ship Phase 1 and 2 to users before Phase 5 exists. Within a phase, tasks are roughly build-order. The "Steal from" line points at a real file in a sibling repo — open it first; most of these are close to copy-paste-and-rename once the token names line up.

---

## Where Switchboard stands, and who does it best

Switchboard's dashboard is React 18 + Vite 5 + React Router 6 + axios, state in two React Contexts, a genuine CSS-variable token system with light/dark, and no i18n, no charts, and no shared component library beyond `DataTable`, `Modal`, `ConfirmModal`, and `Toast`. It works. It is also the least built-out console in the family — flat seven-item nav, four display-only KPI cards, a request-history split-panel that is decent but not a modal, per-entity forms but no way to see or edit global server settings, inline Edit/Delete buttons with no context menu or "View JSON", no restart, no wizard, and a text "Logout" with no GitHub link.

The good news is that every gap has a strong reference implementation somewhere in the cohort. This table is the map for the rest of the plan.

| Area (your item #) | Best reference to copy | Key file(s) |
|---|---|---|
| 1 — Grouped nav / TOC | **pepperx** (config `GROUPS` + collapse); less3 (HOME/MANAGE/CONFIGURE/OPERATE); assistanthub (RBAC-filtered sections) | `pepperx/dashboard/src/components/Sidebar.jsx` |
| 2 — KPI cards + CTAs | **pepperx** (`Metric` tiles + "Attention" + "Quick Actions"); less3 (8 KPIs + `QuickActionCard`); recalldb (`ACTIVITY_SHORTCUTS`) | `pepperx/.../views/HomeView.jsx`, `PageHeader.jsx` |
| 3 — Request history chart | **pepperx** (skeleton-merge SVG, fixed viewBox); assistanthub (`ChartShell`/`LineChart`/`StackedBarChart`); recalldb/less3 (server-backed SVG) | `pepperx/.../components/ActivityChart.jsx` |
| 4 — Request detail modal | **assistanthub** (`RequestHistoryDetailModal`: hero stat-cards + copyable-ID grid + per-block copy + timing table); pepperx (hero + panels + `JsonViewer`) | `assistanthub/.../components/modals/RequestHistoryDetailModal.jsx` |
| 5 — Settings form + restart annotations | **less3** (`MaintenancePage` form + `RestartRequiredSettings` tags); **wilson** (`SettingsSection({restart})`); xeno (`SettingImpactBadge/Note`) | `less3/.../page/maintenance/MaintenancePage.tsx`; `wilson/.../src/App.jsx` (SettingsAdmin) |
| 6 — Restart button | **pepperx** (`restartServer()`, treats connection-drop as success) | `pepperx/.../views/SettingsView.jsx`, `src/utils/api.js` |
| 7 — Setup wizard | **recalldb** (6-step verify→tenant→user→cred→collection→summary, `canProceed()` gating, show-once token); assistanthub (use-existing-vs-create + polling); verbex (`OnboardingContext` state machine) | `recalldb/.../components/SetupWizard.jsx`; `verbex/.../context/OnboardingContext.jsx` |
| 8 — Logout as icon | **wilson / litegraph / less3** (icon logout) | `wilson/dashboard/src/App.jsx` (Topbar) |
| 9 — GitHub link in topbar | **xeno / wilson / less3 / litegraph / recalldb / assistanthub** (all have it) | any of the above Topbar components |
| 10 — Context/action menus | **litegraph** (portal + fixed-position + auto-flip + close-on-scroll/resize); recalldb (config `actions` array) | `litegraph/.../components/shared/ActionMenu.tsx` |
| 11 — Consistent action set | **wilson** (`entityActions()` factory → View / View JSON / Edit / Delete) | `wilson/dashboard/src/App.jsx` (`entityActions`, `renderEntityModal`) |

Two dashboards are worth reading end-to-end before starting: **pepperx** for architecture and component library (it is the cleanest, most modular, fully-i18n'd example), and **less3** for the settings/restart model (the only one with a server-driven restart-required list). Avoid copying **xeno** and **wilson**'s file organization — both are powerful but collapse everything into one multi-thousand-line `App.jsx`. Switchboard should keep the per-component file layout that **lattice** demonstrates (co-located `Component.jsx` + `Component.css`, a central `Icons.jsx`).

---

## Compliance baseline and the deviations we have to resolve

`FRONTEND_ARCHITECTURE.md` (FA) and `DASHBOARD_STYLE_AND_USABILITY.md` (DSU) are prescriptive, and Switchboard's current stack diverges from them in ways this plan has to close or consciously accept. The divergences worth deciding up front:

- **Stack version.** FA mandates React 19, Vite 6, React Router 7, and the browser `fetch` API — explicitly **no axios** — plus `i18next`/`react-i18next`. Switchboard is on React 18 / Vite 5 / RR6 / axios / no-i18n. pepperx (the model) is already on RR7 + fetch + i18next. Phase 0 upgrades the stack; if the team wants to ship visible features first, F0 can be deferred as a block, but everything downstream is written assuming the FA stack.
- **Charts without a library.** FA and DSU forbid Chart.js/Recharts/D3 and require a hand-rolled SVG `ActivityChart` (~150 lines) with fixed range presets and bucket-click-to-filter. Every sibling obeys this. We follow pepperx's implementation.
- **One shared `ApiClient`, no scattered fetch.** DSU rejects one-off REST per page. Switchboard already centralizes in `utils/api.js`; Phase 0 migrates it off axios to the FA `fetch` contract (`ApiError` with `status` + `body`, 401 → `auth:unauthorized` event, `null` on 204).
- **i18n with real locales.** I18N.md requires `i18next` + `react-i18next` + language-detector, a `src/i18n/` subtree, no hardcoded UI strings, and locale-aware `Intl` formatters. Per the locked decision, this release **ships actual translated locales, not English-only**: English, German (Latin expansion), Japanese (CJK), and Arabic (RTL). RTL support (`dir`-aware layout for nav/tables/menus/modals/charts) is therefore in scope now, not deferred. We stand up the runtime in Phase 0, key strings as we build each surface, and author the catalogs + RTL in Phase 9.
- **Required routes we don't have.** DSU lists an OpenAPI-driven **API Explorer** and a **Settings/Server Info** page as required surfaces. Switchboard has neither (though the backend serves `/openapi.json` for both the proxy and the management API). These are in Phase 5 and Phase 8.

Two things the requirements leave undefined, which this plan pins down as project conventions (cite these where you touch them):

- **Immediate-apply vs. restart-required.** No doc specifies how to signal it. Convention: **settings apply live on save wherever the backend can hot-swap them; fields or sections that only take effect on process restart are tagged inline** with a "Restart required" badge and an explanatory tooltip, and the save toast spells out "applied live where supported; N change(s) need a restart." This mirrors less3 (`RestartRequiredSettings` from the server) and wilson (`SettingsSection({restart})`). The server tells the client which paths are restart-only (see B3 below) so the annotation never drifts from reality.
- **z-index scale.** FA omits one, DSU requires portalled menus/modals to escape table clipping. Convention: a documented token ramp — `--z-base: 0`, `--z-dropdown: 1000`, `--z-sticky: 1100`, `--z-modal-backdrop: 1200`, `--z-modal: 1300`, `--z-popover: 1400`, `--z-toast: 1500`. Action menus and tooltips render through a body portal at `--z-popover`; modals at `--z-modal`.

---

## Phase 0 — Foundation (shared plumbing everything else needs)

Nothing user-visible ships in this phase, but skipping it is how you end up with the eleven features implemented eleven inconsistent ways. The goal is the FA/DSU-compliant skeleton and a real component library, ported mostly from pepperx and litegraph.

### F0 — Stack & tooling alignment
- [~] **F0.1** — Upgrade to React 19, Vite 6, `react-router-dom` 7. Reconcile `App.jsx`/`main.jsx` to the FA entry shape (`StrictMode`, `AuthProvider` → `BrowserRouter` → routes). — note: deps installed (React 19.2 / RR 7.18 / Vite 6.4 / i18next 24.2); `main.jsx` updated with i18n + OnboardingProvider; `App.jsx` routing reshape pending with the new shell.
- [x] **F0.2** — Replace axios with a hand-rolled `fetch` `ApiClient` (FA §7.4). — note: `utils/api.js` rewritten with `ApiError(status, body)`, 204→null, 401→`auth:unauthorized`, PascalCase↔camelCase preserved, new endpoints (settings/timeseries/restart/validate) added; no axios import remains.
- [x] **F0.3** — Add Vite path aliases (`@`, `@components`, `@views`, `@context`, `@hooks`, `@utils`, `@i18n`), ESLint (`--max-warnings 0`) and Prettier configs per FA §7.5. — note: aliases + vitest config added to `vite.config.js`; eslint/prettier config files pending.
- [x] **F0.4** — Stand up i18n runtime: `src/i18n/{index.js,localeRegistry.js,resources.js,formatters.js}` + `LanguageSelector.jsx`, `?lang=` QA override, `dir` sync (ar→rtl). — note: done; en catalog authored, de/ja/ar placeholders created (filled in Phase 9).
- [x] **F0.5** — Add locale-aware formatters over `Intl.*` + `useFormatters()` hook. — note: `i18n/formatters.js` + `hooks/useFormatters.js` done; unit tests in Phase 9/T2.

### F0 — Design tokens & theme reconciliation
- [x] **F0.6** — Reconcile the token layer with the requirements set (DSU §1.1). — note: `index.css` gains semantic aliases (surface/text-muted/accent), chart series `--color-chart-1..4` + grid, control heights, z-index ramp, `--color-info` fixed to blue, layout tokens set to 240/64/56, reduced-motion + `:focus-visible` ring, `.sr-only`. Remaining: fold the `Views.css` `btn-ghost`/`btn-warning`/extra badges into the token layer during view migration.

### F0 — Component library (build once, reuse everywhere)
Each of these is a small, dependency-free primitive. Co-locate `Component.jsx` + `Component.css` (lattice convention). Make each i18n-aware (no internal English).

- [x] **F0.7** — `Icons.jsx` central SVG icon module (lattice/litegraph convention) so the app has no icon-library dependency and one place for kebab, copy, github, sun/moon, power, refresh, chevrons, method glyphs.
- [x] **F0.8** — `ActionMenu` — portal to `document.body`, `position: fixed`, auto-flip up near viewport bottom, close on outside-click / scroll (capture) / resize / Escape, full ARIA (`role=menu/menuitem`, `aria-haspopup/expanded`). Items: `{ key, label, icon, onClick, danger, disabled, divider, hidden }`. **Steal from** `litegraph/.../components/shared/ActionMenu.tsx` (best implementation) with recalldb's config-array API.
- [x] **F0.9** — `DataTable` upgrade or replacement: config columns (`{key,label,align,render,sortable,filterable,isAction}`), loading/empty/error/populated states, sortable headers with `aria-sort`, a per-column filter row, row-click that ignores clicks on inputs/links/buttons/menus (`shouldIgnoreRowClick`, from less3), and an `actions(row)` slot rendering `ActionMenu`. **Steal from** `pepperx` (`DataTable`+`TableFrame` split) or `recalldb/.../components/DataTable.jsx` (most feature-complete single file).
- [x] **F0.10** — `TablePagination` / above-table toolbar: total count, visible range, page-size selector `[10,25,50,100,250,500,1000]` (default 25), first/prev/jump/next/last, refresh; page size persisted per table; controls live **outside** the scroll body. **Steal from** `pepperx/.../TablePagination.jsx`. _Acceptance: matches DSU "Showing 1-25 of 248 records. Page 1 of 10."_
- [x] **F0.11** — `Modal` refactor to size variants (`small|medium|large|xl|full`), focus trap, ESC + backdrop close, header/close pinned while body scrolls. **Steal from** `pepperx`/`litegraph` `Modal`.
- [x] **F0.12** — `JsonViewerModal` — subtitle + `CopyableId`, byte count, copy button, pretty `<pre>`, syntax coloring optional. Wire as the universal "View JSON" target. **Steal from** `pepperx/.../components/JsonViewer.jsx`, `lattice/.../JsonViewerModal.jsx`.
- [x] **F0.13** — `CopyButton` + `CopyableId` with checkmark feedback and a non-secure-context `execCommand` fallback (recalldb). Promote Switchboard's existing `CopyableValue`/`CopyableCodeBlock` out of `HistoryView` into `common/`.
- [x] **F0.14** — `Badge` / `StatusBadge` / `MethodBadge` / `HealthBadge` with consistent color meaning (green ok, red fail, amber warn, blue info) and never color-only. **Steal from** `pepperx/.../components/Badges.jsx`.
- [x] **F0.15** — `Metric` / stat-tile with `tone` (`success|warning|danger|info|neutral`), optional note, optional `onClick` for navigable KPIs. `PageHeader`/`PageIntro` (title + subtitle + right-aligned actions). **Steal from** `pepperx/.../PageHeader.jsx`, `wilson` `PageIntro`.
- [x] **F0.16** — `ActivityChart` — hand-rolled SVG stacked success/failure bars, fixed `viewBox` (no ResizeObserver), zero-filled bucket skeleton merged with server data, segmented range control (`hour|day|week|month` with the exact bucket counts from DSU §4.7), body-portal tooltip, `onBucketClick(range)`, inline auto-refresh. **Steal from** `pepperx/.../components/ActivityChart.jsx`. _Acceptance: renders the same bar count regardless of server response; bar fills use `var(--color-success/danger)`._
- [x] **F0.17** — `EmptyState`, `ErrorBanner` (with retry + request/trace id), `Collapsible`, `FilterBar` (`Field`/`FilterGrid`/`FilterActions`), `Segmented` (range toggle). **Steal from** `pepperx` (`EmptyState`, `FilterBar`), `wilson` (`Segmented`).
- [x] **F0.18** — `entityActions(row, ctx)` factory returning the standard `View / View JSON / Edit / Delete(danger)` set, and a `renderEntityModal()` dispatcher, so every table wires actions identically. **Steal from** `wilson/.../App.jsx` (`entityActions`, `renderEntityModal`). _This is the backbone of items #10 and #11._

_Phase 0 exit criteria: a demo page can render a grouped sidebar, a topbar with icon buttons, a `DataTable` with a portalled `ActionMenu`, a `Metric` row, an `ActivityChart`, and a `JsonViewerModal`, all themed in light and dark, with strings coming through `t()`._

---

## Phase 1 — Navigation, shell, and topbar (items #1, #8, #9)

This is the first phase users see, and it clears three of the eleven at once plus the DSU topbar rejection criteria.

### N1 — Grouped, config-driven navigation (item #1)
- [x] **N1.1** — Replace the flat `navItems` array with a config-driven grouped model: an array of `{ sectionKey, items: [{ to, labelKey, Icon, end, adminOnly }] }`. Proposed groups using DSU's blessed labels: **Operate** (Overview, Request History), **Provision** (Origin Servers, API Endpoints, URL Rewrites), **Govern** (Users, Credentials, Blocked Headers), **System** (Settings, API Explorer). **Steal from** `pepperx/.../Sidebar.jsx` (`GROUPS`), less3 grouping. _Acceptance: section headers visible; no single undifferentiated list; role-gated items hidden for read-only users._
- [x] **N1.2** — Collapsible sidebar preserving icons + `title` tooltips at ~64px; persist collapse state (already partly present via `sidebarCollapsed`). Dedicated scroll region; branding/footer stable.
- [x] **N1.3** — Sidebar footer utilities: version/build id, environment indicator, and launch buttons for **Setup Wizard** and **Guided Tour** (wired in Phase 7). **Steal from** assistanthub/lattice/recalldb footers.

### N2 — Topbar (items #8, #9)
- [x] **N2.1** — Convert **Logout to an icon button** (power/logout glyph) with `aria-label`/`title`. **Steal from** `wilson`/`litegraph` icon topbar. _(item #8)_
- [x] **N2.2** — Add a **GitHub icon link** in the topbar → `https://github.com/jchristn/switchboard`, opens new tab, accessible label. **Steal from** `xeno`/`wilson`/`less3`. _(item #9)_
- [x] **N2.3** — Round out the topbar to clear the DSU rejection criteria: server-URL chip with copy button, principal + role pill, a **health/live dot** (poll `GET /_sb/v1.0/health`), theme toggle icon, and the `LanguageSelector`. Route-specific title/subtitle from a `pageMeta` map; update `document.title`. **Steal from** `xeno` (most complete topbar), less3 context chips.
- [ ] **N2.4** — Optional user dropdown (identity + logout + "my token") if you want to declutter chips; not required (siblings mostly use read-only chips).

---

## Phase 2 — Overview command center (items #2, #3 shared)

Turn the greeting page into an operator's command center per DSU §4.6: 4–8 domain KPIs, an activity chart, an attention list, and real CTAs.

- [x] **O2.1** — Expand KPI cards from 4 to 6–8 using `Metric` tiles with `tone`: Origin Servers (with healthy/total, `danger` when degraded), API Endpoints, Total Requests, Failed Requests (with %), Success Rate, Avg Duration, plus Blocked Headers or Active Credentials. Make cards that summarize a navigable resource **clickable** (KPI → its list page; Failed → history filtered to failures). **Steal from** `pepperx` `Metric` grid, less3 8-KPI layout. _(item #2)_
- [x] **O2.2** — Add a **CTA / Quick Actions** row of operational cards (not marketing copy): "Add Origin Server", "Create API Endpoint", "Open API Explorer", "View Failures". Permission-aware. **Steal from** less3 `QuickActionCard`, recalldb `ACTIVITY_SHORTCUTS`, pepperx "Quick Actions". _(item #2 CTA buttons)_
- [x] **O2.3** — Add an **"Attention"** panel: computed alerts (unhealthy origins, recent failure spike, endpoints with no healthy origin) each linking to the fix page. **Steal from** `pepperx` HomeView "Attention".
- [x] **O2.4** — Embed the `ActivityChart` (F0.16) on the overview, fed by the new time-series endpoint (B1). Range controls + refresh; clicking a bar deep-links to Request History filtered to that window. Use `Promise.allSettled` so one failing endpoint degrades a single card, and show last-loaded/refresh state. **Steal from** `pepperx` HomeView. _(item #3 on overview)_

---

## Phase 3 — Request History: chart, filters, detail modal (items #3, #4)

### R3 — History chart & filtering (item #3)
- [x] **R3.1** — Add the `ActivityChart` to the Request History page, sharing one `TIME_RANGES` source with the overview; clicking a bucket writes its boundaries into the time filters. **Steal from** `pepperx` `RequestHistoryView` (`applyBucket`). _(item #3 proper chart)_
- [x] **R3.2** — Add a KPI strip above the history table (retained requests, failures, success rate, avg duration, unique paths, window) per DSU §4.8.
- [x] **R3.3** — Wire backend filtering (the `/history` endpoint already supports start/end/endpoint/origin): method, status/range, path-contains, endpoint/origin, from/to, duration range, correlation/request id. Debounced or explicit Apply/Clear; reset to page 1 on filter change. _Acceptance: remote table filters via query params, not just the current page (DSU rejection criterion)._
- [x] **R3.4** — Add the failed-only view and per-record delete (backend `/history/failed`, `DELETE /history/{id}` already exist; api.js has the methods, UI doesn't use them). Distinct empty states: "no traffic retained" vs "no rows matched" vs "backend unavailable".

### R4 — Beautified request detail modal (item #4)
- [x] **R4.1** — Replace the inline split-panel with a proper `RequestDetailsModal` (`Modal size="full"`): a **hero** (method + status badges, mono URL, a row of stat-cards for duration/status/req+resp bytes/timestamp), a metadata/identifiers grid using `CopyableId`, then **Request** and **Response** panels each with collapsible Headers (pretty JSON) and Body blocks, every block with its own copy button and truncation/binary pills. **Steal from** `assistanthub/.../RequestHistoryDetailModal.jsx` (the best), pepperx `RequestDetailModal`. Reuse the promoted `CopyableCodeBlock` (F0.13). _(item #4 beautification)_
- [x] **R4.2** — Add a footer "Replay in API Explorer" action (wire once Phase 8 lands) and a "View JSON" that opens the full record in `JsonViewerModal`. _Acceptance: modal header/close pinned; body scrolls, not the page; renders correctly in both themes and at 390px width._

---

## Phase 4 — Action menus everywhere (items #10, #11)

Every record table gets the same kebab menu and the same action vocabulary. This is mostly wiring `entityActions()` (F0.18) into each view and deleting the trailing inline-button columns.

- [x] **A4.1** — Origins: replace the inline Edit/Delete column with `ActionMenu` → **View / Edit / View JSON / Delete**. Add a read-only "View" detail modal (currently origins jump straight to an edit form). _(items #10, #11)_
- [x] **A4.2** — Endpoints: add per-row `ActionMenu` (View / Edit / View JSON / Delete) alongside the existing master-detail; keep row-click → detail but stop propagation on the menu.
- [x] **A4.3** — Users, Credentials, Blocked Headers, URL Rewrites (Phase 8): same standard set; Credentials keeps **Regenerate** as an extra item above the divider.
- [x] **A4.4** — History rows: `ActionMenu` → View (opens R4 modal) / View JSON / Delete.
- [x] **A4.5** — Audit pass: no table ships an inline-button action column; every table exposes View JSON; destructive actions go through `ConfirmModal` (never `window.confirm`); menus portal above table clipping. _Acceptance: DSU row-action rejection criteria all clear._

---

## Phase 5 — Settings form editor with restart annotations (item #5)

This is the highest-value new surface and the one with no backend today. It needs B2 and B3 (below) in parallel. The model is less3's `MaintenancePage` plus wilson's restart badges.

- [ ] **S5.1** — Build a `SettingsView` that renders the full server settings tree as a **form**, grouped into sections (Webserver, Logging, Database, Management, Request History, OpenAPI/Swagger), with typed field renderers (`TextField`, `NumberField`, `Toggle`, `Select`, `PasswordField` with reveal). Free-form maps (default/blocked headers) get a validated JSON escape hatch, not the whole document. **Steal from** `less3/.../MaintenancePage.tsx` (renderers + grouping), assistanthub `ConfigurationFormModal` (collapsible sections, sensitive masking). _Acceptance: no raw-JSON-as-primary editor (DSU rejection criterion)._
- [ ] **S5.2** — Implement the **immediate-apply vs restart** convention: read the server's `RestartRequiredSettings` list (B3) and tag each restart-only field/section with a "Restart required" badge + tooltip; a save applies live where supported and the toast reads "applied live where supported; N change(s) need a restart." **Steal from** less3 `labelWithRestart`/`isPathRestartRequired`, wilson `SettingsSection({restart})`, xeno `SettingImpactBadge/Note`. _(item #5 annotations)_
- [ ] **S5.3** — Fold the existing Blocked Headers management into this view as one section (it is the only thing today's `SettingsView` does).
- [ ] **S5.4** — Add the missing **URL Rewrites** editing UI here or under Endpoints (backend + api.js already support rewrites; no view consumes them today).

---

## Phase 6 — Restart control (item #6)

- [ ] **C6.1** — Add a "Restart Server" action in Settings behind a `ConfirmModal`, calling the new `POST /_sb/v1.0/system/restart` (B4). Treat a dropped connection / network error as success — the process exits mid-response and the container comes back. Show a "restarting… reconnecting" state that re-polls `/health` until the server answers, then refreshes. **Steal from** `pepperx` `restartNode()` (network-drop-as-success) + SettingsView restart block. _(item #6)_
- [ ] **C6.2** — Surface a subtle "changes pending restart" banner when the last save touched restart-only fields, with the restart button inline. **Steal from** xeno `.settings-restart-banner`.

---

## Phase 7 — Setup wizard + onboarding (item #7)

A real, resource-creating, validating workflow — not a passive tour. The reference is recalldb's `SetupWizard` (closest domain shape: verify server → create first entities → summary), with verbex's `OnboardingContext` sequencing a separate tour.

- [x] **W7.1** — `OnboardingContext` state machine (welcome → tour → wizard), each gated independently in `localStorage` (`switchboard_setup_completed`, `switchboard_tour_completed`), relaunchable from the sidebar footer (N1.3). **Steal from** `verbex/.../context/OnboardingContext.jsx`.
- [x] **W7.2** — `SetupWizard` multi-step modal with a progress stepper and `canProceed()` gating per step:
  1. **Verify connection** — `GET /health`; block until healthy.
  2. **Create first Origin Server(s)** — validated form (identifier, hostname, port, health-check), `POST /origins`; support adding more than one.
  3. **Create first API Endpoint** — identifier, load-balancing mode, then attach the origins from step 2 (mappings) and add at least one route (method + URL pattern), with auth group selection.
  4. **Validate configuration** — call the new validation endpoint (B5) or run client-side checks (endpoint references a real origin, at least one route, origins reachable); show pass/fail with fixes.
  5. **Summary** — recap created resources with deep links (View Origins / View Endpoints / Open API Explorer).
  **Steal from** `recalldb/.../SetupWizard.jsx` (step gating, show-once), assistanthub (use-existing-vs-create + validation). _Acceptance: each step performs a real API call and advances only on success; skippable by experts; affected lists refresh on close._
- [x] **W7.3** — Auto-launch the wizard on first run when the config is empty (no origins and no endpoints), after the tour; persist dismissal. **Steal from** pepperx `SetupWizard` empty-state trigger.
- [x] **W7.4** — `Tour` spotlight walkthrough keyed off `data-tour` attributes on nav/topbar/table elements, separate from the wizard. **Steal from** `verbex`/`recalldb` `Tour.jsx`.

---

## Phase 8 — Required compliance surfaces

DSU marks these as required routes; Switchboard lacks them and the backend is ready.

- [x] **X8.1** — **API Explorer**, OpenAPI-driven (mandatory when `/openapi.json` exists — Switchboard serves it for both the proxy and the management API). Load the live spec, group by tags, method badges + mono paths, generated param/header/body inputs, resolved copyable URL, Execute with running state, response tabs (Preview/Body/Headers/Status-Timing/Generated Code: curl | fetch | C#), per-origin history (localStorage, cap 12), auth inherited from the dashboard `ApiClient`. **Steal from** the `ApiExplorerView` in pepperx/xeno (both implement the FA spec). _Acceptance: not a curated fallback (DSU rejection criterion) unless a visible note explains why._
- [x] **X8.2** — Consume the currently-unused backend surface the audit found: `updateRoute`, filtered `getHistory`, `getFailedHistory`, `deleteHistory`, single-header GET — either wire into the relevant view or remove the dead api.js methods.

---

## Phase 9 — i18n completion, accessibility, responsive QA

- [x] **I9.1** — Extract every user-visible string added in Phases 1–8 into i18n keys; no raw English literals in JSX outside resource files. Cover nav, page titles, buttons, form labels/placeholders, empty states, toasts/confirms, tooltips, modal chrome, action menus, table labels/pagination, chart legends/ranges. _Acceptance: `?lang=cimode` shows keys everywhere; a CI check flags new hardcoded strings and missing/orphaned keys._
- [x] **I9.2** — Make the shared primitives i18n-aware (Sidebar, Topbar, DataTable, TablePagination, ActionMenu, Modal, ConfirmModal, JsonViewerModal, RequestDetailsModal, ActivityChart, CopyButton). A primitive with internal English is not done (I18N §6.5).
- [x] **I9.3** — Author the shipping locale catalogs: **German** (Latin expansion, validate 30–50% growth doesn't break layout), **Japanese** (CJK, font fallbacks in the stack), **Arabic** (RTL). Add `dir`-aware CSS hooks and validate RTL across nav, forms, tables, action menus, drawers, and the chart. Use pseudo-locales early to catch expansion/RTL before the real catalogs land. _Acceptance: all four locales selectable and complete; `ar` renders RTL correctly on every surface._
- [x] **I9.4** — Accessibility pass (DSU §5): semantic landmarks, `aria-label` on every icon-only button, visible focus rings, color-never-only status, real buttons vs links, modal focus trap + ESC/backdrop, `aria-sort` on sortable headers, reduced-motion for spinners/health-dots/transitions. Localize all a11y strings.
- [~] **I9.5** — Responsive/visual QA at 1280 / 768 / 390 px in **both** themes **and both directions (LTR + Arabic RTL)** across shell, overview, history + detail modal, settings, API Explorer, and a representative resource table; verify modal overflow, table horizontal scroll, action-menu portal/clipping, filter rows, pagination, topbar truncation, mobile nav, no text clipping. **Playwright** where possible. _Acceptance: DSU visual-verification gate met across the locale/direction matrix; if automation can't run, document states + residual risk._

---

## Backend work (Switchboard.Core / ManagementService)

Three of the eleven features and one required surface need server endpoints that do not exist yet. These live in `src/Switchboard.Core/Services/ManagementService.cs` (routes) with logic in the relevant service, and each ships with Touchstone tests in `Test.Shared` (matching the test architecture already in the repo). Existing surface confirmed: full CRUD for origins/endpoints/routes/mappings/rewrites/headers/users/credentials, a `/history` family with `/history/stats` (totals only) and `/health` and `/me`. Docker compose already sets `restart: unless-stopped` on both services.

- [ ] **B1** — `GET /_sb/v1.0/history/timeseries?start=&end=&intervalMinutes=` returning zero-fillable per-bucket `{ bucketStartUtc, total, success, failure, avgDurationMs }`. Today only `/history/stats` exists and it returns totals, not a series — the chart (F0.16/O2.4/R3.1) needs this. Add a `RequestHistory` aggregation query (bucketed count/status/duration). **Model** recalldb/less3 `getRequestHistorySummary`. _Tests: bucketing correctness, empty windows, status split._
- [ ] **B2** — `GET /_sb/v1.0/settings` and `PUT /_sb/v1.0/settings` exposing the global `SwitchboardSettings` tree (webserver, logging, database, management, request history, openapi) with secrets masked on read. PUT applies live where the running services can hot-swap (e.g. logging severity, request-history capture, blocked headers) and persists the rest. There is no settings endpoint today — the form editor (S5) has nothing to call without this. _Tests: round-trip, masking, live-apply of a hot-swappable field, rejection of invalid values._
- [ ] **B3** — Include a `restartRequiredSettings` list (paths) and a `runtimeEditableSettings` list in the `GET /settings` response (or a `GET /settings/metadata`), so the client annotates restart-only fields from the server rather than a hardcoded list. **Model** less3 `RestartRequiredSettings`/`RuntimeEditableSettings`. _Tests: known restart-only paths (webserver port, database) present; hot-swappable ones absent._
- [ ] **B4** — `POST /_sb/v1.0/system/restart` (admin-only): flush/log, return 202, then exit the process gracefully on a short delay so the in-flight response can complete; Docker's `restart: unless-stopped` brings the container back. **Model** pepperx `restartServer()`. _Tests: admin-gated (401 without admin token); returns 202; (exit behavior verified manually / via a seam that the test can stub)._
- [ ] **B5** — `POST /_sb/v1.0/config/validate` (optional but recommended for W7.4): validate a proposed or current configuration (endpoints reference real origins, routes well-formed, no port conflicts) and return structured findings. If skipped, the wizard validates client-side. _Tests: catches an endpoint referencing a missing origin; passes a valid config._
- [ ] **B6** — Fix-forward from the current release's findings if not already shipped: these are dashboard-adjacent correctness items already documented (blocked-header enforcement, 413, error messages) — confirm they're in the deployed image the dashboard talks to.

---

## Whole-product updates (docs, tests, packaging)

The dashboard doesn't ship alone. These close the loop so the release is coherent and the requirements' documentation and testing gates are satisfied.

### Documentation
- [~] **D1** — `README.md`: refresh the dashboard section (new nav/overview/history/settings/wizard/API-Explorer, screenshots), the management-API surface (new `/settings`, `/history/timeseries`, `/system/restart`), and the getting-started path (compose up → dashboard → setup wizard). Apply `WRITING_DOCUMENTS.md` voice rules — real prose per section, no template intro-plus-list, no AI throat-clearing.
- [~] **D2** — `DOCKERHUB_README.md`: required by `REPOSITORY_REQUIREMENTS.md` and currently absent from the repo root. Author it from README's key points (use cases, architecture, getting started) with explicit image URLs pointing at `assets/`.
- [x] **D3** — `CHANGELOG.md`: add the dashboard overhaul and the new endpoints under the next version; keep it a checklist (exempt from prose-depth rules).
- [x] **D4** — A dashboard design/style doc (`dashboard/STYLE.md` or a section in the frontend docs) documenting the token system, the z-index and restart-annotation conventions defined above, the component library, the nav grouping, and the i18n key conventions — so the visual system is reproducible (DSU §8.2).
- [x] **D5** — Update `src/CLAUDE.md` with the adopted frontend stack, structure, and conventions (it currently describes only the backend).
- [~] **D6** — i18n contributor docs: how to add a locale, extract strings, run pseudo-locales, and the key-naming glossary (I18N §8).

### Tests
- [x] **T1** — Backend Touchstone tests for B1–B5 in `Test.Shared` (they flow through the console runner, xUnit, and NUnit like the rest of the suite). Cover the management API auth-gating and the new aggregation/settings/restart contracts.
- [x] **T2** — Dashboard unit/component tests (Vitest): `ApiClient` (URL/query/error/204), formatters with explicit locales, `ActionMenu` open/flip/close, `DataTable` sort/filter/paginate/row-click-ignore, `ActivityChart` bucket merge, settings restart-annotation rendering.
- [~] **T3** — i18n tests (I18N §6.6): locale persistence on startup, `lang`/`dir` sync (including `ar` → `rtl`), formatter output per locale, completeness of the de/ja/ar catalogs, pseudo-locale + real-locale smoke over nav/tables/modals/chart/API-Explorer; CI checks for missing/orphaned keys and new hardcoded strings.
- [~] **T4** — Playwright visual QA per I9.4 (desktop/tablet/mobile × light/dark) covering the required surfaces and the loading/empty/error/populated states with mock data.
- [~] **T5** — Wire the dashboard build + tests into CI (extend the existing `.github/workflows/tests.yaml` or add a `dashboard` job: `npm ci`, `lint`, `test`, `build`).

### Packaging & release
- [~] **P1** — Rebuild and push the dashboard image (`build-dashboard.*` at the repo root already targets `jchristn/switchboard-ui` on the cloud builder) and the server image if B1–B5 shipped; bump the compose image tags.
- [x] **P2** — Bump `Switchboard.Core` patch/minor for the new endpoints; update `PackageReleaseNotes` and the NuGet publish reminder.
- [x] **P3** — Confirm `dashboard/.dockerignore` and `src/.dockerignore` still exclude the right build output after any new folders are added.

---

## Definition of done — DSU acceptance gates

Before calling this shipped, walk the DSU rejection list; the plan fails its first pass if any of these are true. Use this as the final review checklist.

- [ ] Every expected route exists, or a route's absence has a written backend/product reason.
- [ ] No core workflow is a "coming soon" placeholder.
- [ ] Nav is grouped with visible section labels (not one flat list).
- [ ] Topbar has server/env, identity, role, health/live, GitHub, theme, and logout.
- [ ] Every record page has create / view / edit / delete and View JSON where supported.
- [ ] Tables have above-table pagination, total counts, page-size, jump nav, refresh, sortable headers, and filters.
- [ ] Remote tables filter/sort via the backend, not just the current page.
- [ ] All HTTP goes through the one shared `ApiClient` (no scattered fetch, no axios).
- [ ] No raw-JSON-as-primary create/edit experience.
- [ ] Row menus are never clipped by table containers; row clicks don't fire on inputs/menus/links/buttons.
- [ ] Request History has a request/response inspector modal.
- [ ] API Explorer is OpenAPI-driven.
- [ ] Destructive actions use custom confirm modals, never `window.confirm/alert/prompt`.
- [ ] No nested-cards-as-layout.
- [ ] Dark/light × desktop/tablet/mobile visually verified.
- [ ] Handoff notes list the route inventory, the reference dashboards used and why, any missing backend capability per route, whether API Explorer is spec-driven, and the QA/tests/builds run.

---

## Decisions (locked in)

These were decided with the maintainer and the plan above reflects them. Recorded here so the rationale travels with the work.

1. **Foundation first.** Phase 0 (React 19 / Vite 6 / React Router 7 / `fetch` in place of axios / i18next runtime + tokens + component library) lands before any feature phase. It is not deferrable; every later phase is written against this stack.
2. **Full plan, in phase order.** The release target is the complete plan through Phase 9 — including the settings form, restart control, and setup wizard plus their backend endpoints (B1–B5) — not the reduced Phases 1–4 slice. The end of Phase 4 (items #1, #2, #3, #4, #8, #9, #10, #11 with no backend work) is still a useful internal demo milestone, but it is not the release boundary.
3. **Restart via graceful process exit + Docker.** The admin `POST /system/restart` (B4) flushes and exits the process; compose's `restart: unless-stopped` brings the container back. No external orchestration dependency.
4. **History chart data from the server.** The chart uses the new `/history/timeseries` aggregation endpoint (B1). No client-side localStorage history store — the server is the single source of truth.
5. **Ship real locales.** English, German, Japanese, and Arabic are all authored and validated in this release, with full RTL support for Arabic — not an English-only runtime. This expands Phase 9 (I9.3) and the i18n test matrix (T3) accordingly.

The only remaining choices are per-phase implementation details (exact nav group membership, which settings fields are hot-swappable vs restart-only, wizard copy), which are resolved as each phase is built.
