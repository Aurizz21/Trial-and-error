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

- **Name used throughout the UI:** [confirm and fill in — the conversation
  flagged a conflict between "Manok ni Rene", "Sunrise Poultry Farm", and the
  logo's "Poultry Farm" caption; pick the one that matches the real signage]
- **Logo:** a soft, rounded, grayscale chicken illustration. It is the source
  of the neutral palette. **Do not recolor, stretch, redraw, or add effects
  to the logo artwork.**
- Logo assets live under `wwwroot/images/` (consolidated from the earlier
  split between `wwwroot/image/logo.svg` and `wwwroot/images/logo.png`).
- No dedicated small icon-only logo exists yet. If the full logo becomes
  illegible at 32–40px (sidebar, favicon), crop a simplified icon version
  rather than shrinking the full mark further.

## Palette

- **Neutrals** derived from the logo: charcoal, soft gray, white.
- **One accent color**, chosen to *not* overlap red / amber / green.
- **Status colors — red, amber, green — are reserved for stock/alert
  meaning only.** Never reuse them for ordinary buttons, links, or
  decoration; that's what breaks their signal value.
- All colors are defined as CSS variables in one place and consumed by
  `dashboard.css`, `login.css`, and `modules.css` — no page defines its own
  one-off hex values.

## Typography

- One font pairing (max two families), loaded consistently on **both** the
  login page and `_DashboardLayout.cshtml` — this replaced the earlier split
  where the dashboard silently fell back to system fonts while login used
  Google-Fonts-hosted DM Sans/Space Grotesk.
- Self-hosted in `wwwroot/fonts` where possible, so the app still looks right
  offline.

## Layout & components

- Spacing, radius, and shadow are tokens, not per-component guesses — this
  replaced the earlier mixed 6/10/12px radii and inconsistent soft shadows.
- Icons are inline SVG, not emoji or text glyphs (bell, hamburger, search,
  etc. were the original offenders).
- Dashboard KPIs and charts use visual hierarchy (one primary metric/chart,
  others secondary) instead of a uniform 2×2 grid of equal-weight cards.
- Empty states are designed (small inline illustration + message), not just
  bare text.
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
