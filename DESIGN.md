# Design System — Manok ni Rene / Poultry Farm App

This document exists so the next person (or future you) editing this app keeps
it consistent instead of drifting back to generic AI-dashboard styling. If you
add a new page or component, read this first.

## Product context

- **What it is:** inventory forecasting and auto-alert system for a poultry
  (chicken) business in the Philippines. Project name in code: `PoultryOS`.
- **Who uses it:** the Owner (full access — forecast accuracy, reorder
  suggestions) and Employees (sales entry, stock view). Mostly on laptops,
  sometimes phones or tablets at the shop.
- **Main jobs to be done:** log a sale fast, spot low stock at a glance, read
  forecasts.
- **Tone:** practical, warm, trustworthy, local. Deliberately not
  corporate-SaaS.

## Brand

- **Name used throughout the UI: "Manok ni Rene."** Confirmed — replaces
  "Sunrise Poultry Farm" everywhere. The logo's own "Poultry Farm" caption is
  treated as generic and not used as the displayed brand name.
- **Logo:** a soft, rounded, grayscale chicken illustration. It is the source
  of the neutral palette. **Do not recolor, stretch, redraw, or add effects
  to the logo artwork.**
- Logo assets live under `wwwroot/images/` (consolidated from the earlier
  split between `wwwroot/image/logo.svg` and `wwwroot/images/logo.png`).
- No dedicated small icon-only logo exists yet. If the full logo becomes
  illegible at 32–40px (sidebar, favicon), crop a simplified icon version
  rather than shrinking the full mark further.

## Palette

All color values live in `wwwroot/css/tokens.css` as CSS variables and are
consumed by every other stylesheet — no page defines its own one-off hex
value. Current tokens:

- **Neutrals (from the logo):** `--bg: #f3f3f3`, `--panel: #ffffff`,
  `--panel-alt: #f7f7f7`, `--sidebar: #333333`, `--sidebar-soft: #474747`,
  `--text: #292929`, `--muted: #626262`, `--border: #dedede`.
- **Accent:** `--primary: #2457a6` (a blue — sometimes called "indigo" in
  conversation, but it's this one token, `var(--primary)`, not a separate
  color). Variants: `--primary-hover: #1d4788`, `--primary-soft`,
  `--primary-border`, `--focus-ring`.
- **Status colors — reserved for stock/alert meaning only, never reused for
  ordinary buttons, links, or decoration:** `--critical: #b42318` (+
  `-soft`/`-border`), `--warning: #8a4b08` (+ `-soft`/`-border`),
  `--healthy: #176b3a` (+ `-soft`/`-border`). `--info` currently equals
  `--primary` (`#2457a6`).
- **Spacing:** `--space-1` through `--space-8` (4px–32px scale).
- **Radius:** `--radius-control: 8px`, `--radius-card: 14px`,
  `--radius-pill: 999px`.
- **Shadow:** `--shadow` (resting), `--shadow-raised` (hover/lift).

If a future change needs a new accent or status color, add it as a token in
`tokens.css` first and update this file — don't hardcode a new color in a
page-level stylesheet.

## Typography

- Single typeface: **Nunito** (variable font), self-hosted at
  `wwwroot/fonts/Nunito-Variable.ttf`, loaded via `@font-face` in
  `tokens.css` and used on **both** login and the dashboard shell — this
  replaced the earlier split where the dashboard silently fell back to
  system fonts while login used Google-Fonts-hosted DM Sans/Space Grotesk.
  Fallback stack: `"Nunito", "Segoe UI", sans-serif`.
- Avoid tiny uppercase letter-spaced "eyebrow" labels — replaced with
  readable mixed-case text (first cleaned up on the Login page).

## Layout & components

- Spacing, radius, and shadow are tokens, not per-component guesses — this
  replaced the earlier mixed 6/10/12px radii and inconsistent soft shadows.
- Icons are inline SVG, not emoji or text glyphs (bell, hamburger, search,
  etc. were the original offenders).
- Dashboard KPIs and charts use visual hierarchy (one primary metric/chart,
  others secondary) instead of a uniform 2×2 grid of equal-weight cards.
- Empty states are designed (small inline illustration + message), not just
  bare text.
- **Login page** uses an asymmetric two-column layout: a spacious left brand
  column (logo, large "Manok ni Rene" wordmark, a short human line about the
  shop) paired with a quiet, easy-to-scan form on the right — not a centered
  box on a plain background. On narrow screens (≤360px) the brand column
  collapses into a compact header above the full-width form.
- Login's left column has a muted, looping, grayscale background video
  (`wwwroot/videos/login-video.mp4`) beneath the logo/wordmark. It pauses on
  phone widths and when `prefers-reduced-motion` is set; the original still
  background remains as the fallback/poster.
- Avoid, unless a future brief deliberately revisits it: purple/blue
  gradients, decorative concentric circles, tiny uppercase letter-spaced
  "eyebrow" labels, cards nested in cards, cream backgrounds by default —
  these are the generic-AI-UI patterns this redesign was built to avoid.

## Motion

- Page/panel entrances and control feedback (hover/focus/press) use short
  transitions, roughly 150–300ms, ease-out.
- Chart.js entrance animation (count-ups, bars/lines drawing in) plays on
  **first load only** — background polling/refreshes update data without
  replaying the entrance.
- **`prefers-reduced-motion` must be respected everywhere** — transitions
  and animations shorten or disable for users with that OS setting on.
- Sales Entry has a deliberate, non-jarring success confirmation after
  logging a sale, since it's the highest-frequency action in the app.

## Hard constraints (apply to any future redesign work too)

- Never change controllers, models, Razor logic, `asp-` tag helpers,
  element IDs, class names, or `data-` attributes that JS depends on
  (charts, notifications, sidebar, forms) purely for styling reasons.
- Must stay usable and non-broken down to 360px width.
- Reuse existing tokens/components; don't invent new one-off styles per page.

## Verification checklist (repeat for any future visual change)

Run the app for real and check — code compiling or a diff passing a
whitespace check is **not** the same as this:

1. Click through every page at 360px width (dev tools → device toolbar).
2. Toggle OS-level "reduce motion" on, reload, click through again.
3. Full flow: log in → Dashboard → open mobile sidebar → Sales Entry → log a
   sale → check confirmation → Notifications → filter → check an empty state.
4. Hit real empty states (no low stock, no notifications) and a real error
   state (wrong password).

## Tooling notes

- Design guidance comes from the **Impeccable** skill (project-level design
  context + audit) plugged into Codex, with **Taste Skill** used specifically
  for the Login page (its scope is landing-page-style pages, not dashboards).
- `npx impeccable install` has had an open server-side bug (bundle signature
  verification returning HTTP 404 — tracked upstream as
  `pbakaus/impeccable#479`). If it recurs, don't bypass signature
  verification or install an unverified bundle; retry later, and in the
  meantime apply the skill's guidance by having the agent read the project
  files directly.
- Considering a "DESIGN.md library" skill (several near-identical repos
  exist, e.g. `VoltAgent/awesome-claude-design`, `rohitg00/awesome-claude-design`)
  to borrow layout/type direction from an established brand for a broader
  visual refresh. If used: review whatever file is pasted into agent context
  before applying it, and treat it as a *reference for layout rhythm and
  type scale only* — the brand, palette, and tokens in this file stay as the
  source of truth, they are not replaced wholesale by an imported system.

## Open items

- A broader visual refresh (module/panel positions, overall layout, page
  backgrounds) is under discussion but no specific reference direction has
  been chosen yet — do not apply a full-system import until one is picked
  and approved.
- `wwwroot/videos/login-video.mp4`: confirm the filename no longer has a
  space (an earlier version was `login video.mp4`) and that `Login.cshtml`'s
  reference matches.