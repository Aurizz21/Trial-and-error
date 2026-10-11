# Design System — Manok ni Rene / PoultryOS

Read this before adding or restyling any page. It describes what the app looks
like **today** (the Dashboard design is the reference) and the rules that keep
the rest of the system consistent while it is brought up to match.

Last updated: 2026-10-11, from the merged project (new Dashboard UI + Restock
and Forecast backend).

## Product context

- **What it is:** inventory forecasting and auto-alert system for a poultry
  (chicken) business in the Philippines. Code name `PoultryOS`; brand shown in
  the UI: **"Manok ni Rene"**.
- **Who uses it:** the Owner (full access: products, audit trail, forecast
  accuracy, reorder and restock) and Employees (sales entry, stock view).
  Mostly laptops, sometimes phones or tablets at the shop.
- **Main jobs:** log a sale fast, spot low stock at a glance, read forecasts,
  restock a product.
- **Tone:** practical, warm, trustworthy, local. Deliberately not
  corporate-SaaS.
- **Stack:** ASP.NET Core MVC, Razor views, plain CSS, vanilla JS, Chart.js,
  EF Core, SQL Server Express.

## Brand

- Name in the UI: **"Manok ni Rene"** (replaced "Sunrise Poultry Farm"). The
  logo's own "Poultry Farm" caption is treated as generic.
- **Logo:** a soft, rounded, grayscale chicken illustration and the source of
  the neutral palette. Never recolor, stretch, redraw or add effects to it.
  Assets: `wwwroot/images/logo.svg`, `logo.png`.
- On the Dashboard the logo sits in a white circular chip (`.logo-chip`) so the
  grayscale mark stays legible on the dark sidebar.

## Theme layers (important)

Three layers of tokens exist. Know which one a page is on.

1. **Light base** — `:root` in `tokens.css`. Used by the Login/landing page,
   which has a plain `<body>` and no app theme.
2. **Dark app theme** — `.dashboard-body` in `tokens.css`. TEMPORARY. It holds
   the original dark palette so the pages not yet migrated keep their exact
   current look. Nothing new may use it; it goes away when the last page moves.
3. **Shared skin** — `dashboard.css` + `shell.css`: the gray-to-black wall, the
   light floating window, the shell bar, the sidebar, and **dark gradient cards**
   as the main card surface.

A page opts in to the shell through `ViewData["BodyClass"]` — the Dashboard uses
`"dashboard-page"`, Stock & Alerts uses `"skin-page"`. Both classes are carried
by the same selectors (`:is(.dashboard-page, .skin-page)`), so they can never
drift apart, and the shell itself applies to every authenticated page. Only the
per-page card styling needs the class.

## Tokens

All tokens live in `wwwroot/css/tokens.css`. Do not define a one-off color in a
page stylesheet; add a token here first and update this file.

### Light base (`:root`)

`--bg #f3f3f3`, `--panel #ffffff`, `--panel-alt #f7f7f7`, `--sidebar #333333`,
`--sidebar-soft #474747`, `--text #292929`, `--muted #626262`,
`--border #dedede`, `--primary #2457a6` (hover `#1d4788`), `--critical #b42318`,
`--warning #8a4b08`, `--healthy #176b3a`, `--info` = primary. Each status color
has `-soft` and `-border` variants.

Spacing `--space-1` to `--space-8` (4px to 32px). Radius: `--radius-control 8px`,
`--radius-card 14px`, `--radius-pill 999px`. Shadows: `--shadow`,
`--shadow-raised`. Focus: `--focus-ring`.

### Dark app theme (`.dashboard-body`)

`--bg #17191D`, `--panel #24272D`, `--panel-alt #2E3239`, `--sidebar #111318`,
`--sidebar-soft #1B2028`, `--primary #6A9FF0` (hover `#82B1F5`),
`--on-primary #142033` (text on blue-filled buttons), `--text #F3F4F6`,
`--muted #B4BCC8`, `--border #78818F`, `--critical #FF7670`,
`--warning #FFC768`, `--healthy #61D394` (each with `-soft` and `-border`),
`--info #74A7F3`, `--scrim #080A0DB8`, `--focus-ring 0 0 0 3px #82B1F5B3`.
Fields and selects set `color-scheme: dark`.

### Dashboard skin tokens (`:root`, prefix `--skin-`, plus `--gray-*`)

These hold exactly what the Dashboard renders. They are on `:root`, so any page
can use them.

| Token | Value | Used for |
|---|---|---|
| `--skin-wall-top` | `#888888` | top of the page background gradient |
| `--skin-black` | `#000000` | bottom of gradients, darkest surface |
| `--skin-window` | `#bebebe` | the floating main window |
| `--skin-ink` | `#1b1b1b` | main text on the window |
| `--skin-window-muted` | `#4c4c4c` | muted text on the window |
| `--skin-window-line` | `#a8a8a8` | lines on the window |
| `--skin-dark-top` | `#8a8a8a` | top of dark cards, tab pills, rail frame |
| `--skin-on-dark-muted` | `#d8d8d8` | muted text on dark cards |
| `--skin-white` | `#ffffff` | text on dark, plotting sheet |
| `--skin-card` / `--skin-card-soft` | `#bababa` / `#ececec` | rail cards |
| `--skin-button` / `--skin-button-hover` | `#4b4b4b` / `#3d3d3d` | action button on light cards |
| `--skin-critical`, `--skin-critical-count` | `#b6211a`, `#c62415` | alert red on light skin surfaces |
| `--skin-banner-bg/-border/-text` | `#fdecec / #f3c9c6 / #a01f18` | depletion banner |
| `--skin-sidebar-width` | `299px` | sidebar width |
| `--skin-radius-window/-card/-inner` | `28px / 24px / 22px` | skin radii |

Neutral ramp for text, borders and fills on light skin surfaces:
`--gray-900 #111111`, `-800 #242424`, `-700 #4a4a4a`, `-600 #6b6b6b`,
`-500 #8c8c8c`, `-400 #9a9a9a`, `-350 #b5b5b5`, `-300 #cfcfcf`, `-250 #e0e0e0`,
`-200 #efefef`, `-150 #f6f6f6`, `-100 #fafafa`.

The old Dashboard-only variable names (`--win`, `--ink-top`, `--rail-card`, and
so on) still exist inside `.dashboard-page` as aliases of these tokens. New code
should use the `--skin-*` and `--gray-*` names.

### Dark card tokens (`:root`)

The card surface is a vertical gradient from `--card-dark-top` to pure black,
with light text. `--card-dark-top` is the **lightest** stop at which every small
text on a card still clears 4.5:1 at its worst spot — the top of the gradient,
which is the lightest point:

| Text | on `#5c5c5c` |
|---|---|
| `#ffffff` | 6.69:1 |
| `#d8d8d8` (muted) | 4.69:1 |

The mockup's own top, `#8a8a8a`, gives only 3.45:1 and 2.42:1, so it cannot be
used for a card that carries text. Large text-free frames keep the lighter
`--skin-dark-top` and stay nearer the mockup.

| Token | Value | Used for |
|---|---|---|
| `--card-dark-top` | `#5c5c5c` | top stop of a text-bearing dark card |
| `--card-dark-gradient` | `linear-gradient(180deg, var(--card-dark-top) 0%, #000000 100%)` | the card fill |
| `--card-ink` | `#ffffff` | headings and values on a dark card |
| `--card-muted` | `#d8d8d8` | secondary text on a dark card |
| `--card-line` | `rgba(255,255,255,0.22)` | 1px card edge and row dividers |

The card fill is a flat shadow (`--shadow-card`) only. **No blur, no
translucency, no backdrop-filter** — the glass direction is retired.

### Status colours on each surface

Red, amber and green mean **stock/alert state only** on every surface. The same
pill shape is used everywhere; only the fill differs, because a tint that works
on one surface is unusable on the other (`#ffb3ae` on the window `#bebebe`
measures 1.09:1).

**On dark cards** (`--cs-*`) — lightened, measured on `#5c5c5c`:

| Token | Value | Ratio |
|---|---|---|
| `--cs-critical` / `--cs-critical-text` | `#ffb3ae` / `#5c1210` | 7.95:1 |
| `--cs-warning` / `--cs-warning-text` | `#ffdca8` / `#4a2a06` | 9.88:1 |
| `--cs-healthy` / `--cs-healthy-text` | `#a8e8c4` / `#0d3a24` | 9.11:1 |
| `--cs-neutral` / `--cs-neutral-text` | `#dcdcdc` / `#3d3d3d` | 7.92:1 |
| `--cs-critical-figure` | `#ff9a96` | 3.28:1 (figures ≥24px bold only) |
| `--cs-warning-figure` | `#ffd08a` | 4.66:1 |
| `--cs-healthy-figure` | `#86e4b1` | 4.38:1 |

**On light cards and light panels** (`--ls-*`) — darkened, measured on the
darkest light surface `#b5b5b5`:

| Token | Value | Ratio |
|---|---|---|
| `--ls-critical` / `--ls-critical-text` | `#c1413c` / `#ffffff` | 5.14:1 |
| `--ls-warning` / `--ls-warning-text` | `#8a4b08` / `#ffffff` | 6.79:1 |
| `--ls-healthy` / `--ls-healthy-text` | `#1f6f45` / `#ffffff` | 6.14:1 |
| `--ls-neutral` / `--ls-neutral-text` | `#cfcfcf` / `#3d3d3d` | 6.97:1 |
| `--ls-critical-soft` / `-edge`, `--ls-warning-*`, `--ls-healthy-*` | alpha tints | banners and badges |

**Constraint worth knowing:** critical as *small* text on a dark card tops out
at 3.92:1 (`#ffb3ae` on `#5c5c5c`), under the 4.5:1 floor. On a dark card,
critical may only appear as a badge (fill + dark ink) or as a figure at 24px+
bold. Every other status colour passes as small text.

### Chart tokens (`:root`, read by `dashboard.js`)

`--chart-primary #b6211a`, `-secondary #8e1a14`, `-tertiary #6d4a47`,
`-quaternary #c9564c`, `-grid #f0d7d5`, `-critical #b6211a`, `-warning #a35a12`,
`-healthy #2c6e49`, `-critical-fill`, `-fill-top`, `-fill-bottom`, `-legend
#dedede`. Only Chart.js reads these.

### Status colors

Red, amber and green mean **stock/alert state only** (`--critical`, `--warning`,
`--healthy`). Never use them as decoration or as ordinary button colors. See
"Known deviations" for where the Dashboard currently breaks this.

## Glass rules retired

The card style was **frosted glass** for one pass; the owner's mockups replaced
it with dark gradient cards. These rules and tokens were retired. They are listed
here so nobody reintroduces them by accident.

**Retired rules** (deleted from `dashboard.css`, no longer anywhere):

- `.glass-card` translucent fill, `@supports (backdrop-filter: …)` blur block,
  and its `prefers-reduced-transparency: reduce` fallback.
- `.glass-card--solid` (the "stay opaque" variant — now everything is opaque).
- `.glass-card:hover` shadow lift.
- The Stock & Alerts `@supports` blur block on `.stat-strip .kpi-card`.
- `.content-area::before`, the tonal backdrop wash that existed only to give the
  blur something to smear.
- The dark glass chart-card overrides (`.dashboard-page .dash-chart-card` and its
  `.dash-card-head h2` / `.chart-empty*` colour rules), superseded by the dark
  card skin.
- **The legacy `:where(.dashboard-page, .skin-page) .topbar` block**
  (`dashboard.css`, was ~line 1581) plus the opaque `7. Final bar precedence`
  block in `shell.css`. The first won on load order over the `:where()` rule and
  the second re-declared an opaque fill, so between them the bar could never be
  translucent and `backdrop-filter` had nothing to show.
- `.topbar { backdrop-filter: none }` at `notifications.css` (was line 76).

**Unused glass tokens, left defined on purpose** (they still resolve, so an older
page or inline reference cannot break; none of them are read by any live
stylesheet now):

`--glass-bg`, `--glass-border`, `--glass-shadow`, `--glass-bg-solid`,
`--glass-card-ink`, `--glass-card-label`, `--glass-card-soft`, `--glass-edge`,
`--glass-bar-sheen`, `--glass-bar-shadow`, `--grad-bar`, `--grad-sidebar`,
`--surface-bar`.

**Still live:** `--glass-blur`, `--glass-bar-bg`, `--glass-bar-blur`,
`--glass-bar-saturate`, `--glass-bar-edge`, `--glass-bar-fallback`,
`--glass-bar-ink`, `--glass-bar-ink-soft`, `--wall-top`.

## Typography

- One family: **Nunito** (variable), self-hosted at
  `wwwroot/fonts/Nunito-Variable.ttf`, loaded in `tokens.css`. Fallback
  `"Nunito", "Segoe UI", sans-serif`.
- No tiny uppercase letter-spaced "eyebrow" labels; use mixed-case text.
- Minimum readable size is 0.8rem (about 13px). The Dashboard currently goes
  below this (see Known deviations); do not copy those sizes.

## Layout and components

### Authenticated shell (all pages)

- **Sidebar** with two groups: **Shop** (Dashboard, Sales Entry, Stock & Alerts,
  Forecast) and **Manage** (Product Management, Sales History, Audit Trail).
  Product Management and Audit Trail are Owner-only (`owner-only` class).
  Every link has a line icon (inline SVG, in the shared layout), and Stock &
  Alerts carries a red count badge.
- **Top bar (glass shell bar):** page title, global search, date, notification
  bell on one **translucent** surface with a real backdrop blur.
  - Surface: `--glass-bar-bg` (`rgba(255,255,255,0.65)`), `backdrop-filter:
    blur(16px) saturate(140%)` with the `-webkit-` prefix, a 1px
    `--glass-bar-edge` bottom border, and `--radius-shell` on the top corners so
    it follows the window with no gap or clipped corner.
  - **Sticky, and it must stay sticky.** `.main-panel` is the scroll container
    (`.dashboard-body` is `overflow:hidden`), and the bar is that container's
    first child, so content scrolls behind it. Adding top padding to
    `.main-panel` would push the bar off the container's top and stop the blur
    ever showing — do not add any.
  - **The fill has to be declared with enough weight.** The bar's surface is
    decided by load order, not specificity: `shell.css` is linked last, so an
    earlier opaque `.topbar` rule (for example a `:where()` rule in
    `dashboard.css`) would win and leave the fill solid, making the
    backdrop-filter invisible. Keep every bar-surface rule in `shell.css`.
  - Ink: title `--glass-bar-ink #111111`, icons `--glass-bar-ink-soft #1b1b1b`,
    search field opaque `--gray-050` with `#111111` text.
  - `@supports not (backdrop-filter: blur(1px))` → opaque
    `--glass-bar-fallback #e4e4e4`.
  - Mobile (≤720px): opaque `--gray-100`, no blur, square corners, existing
    two-row layout unchanged so there is no layout shift.
  - The title, search field, date and bell keep their markup, IDs and behaviour.
- **Wall:** one gradient on `.dashboard-body`
  (`--wall-top #6b6b6b` → `--skin-black`), `background-attachment: fixed`. The
  **sidebar paints nothing** (`background: transparent`), so sidebar and wall are
  one continuous gradient and there is no seam at the sidebar edge. Giving the
  sidebar its own gradient is what produced the seam: at the top of a 100dvh
  column it started at `#5a5a5a` while the wall beside it was `#888888`, a
  ~46-level step at x = `--sidebar-width`.
- **Global search** (`global-search.js`, `GET /Search/Query`): minimum 2
  characters, up to 5 results per group (products Owner-only; active alerts and
  sales for any signed-in user), optional date range, grouped dropdown, "No
  results found." state, results rendered with `textContent`. Empty-panel
  suggestion chips are static and role-specific.
- **Responsive shell:**
  - Below 1240px the window margins and bar padding tighten with the sidebar.
  - Below 768px the sidebar slides in with an overlay, the window goes full width
    and square, and the bar drops its radius, border and shadow (it is flush with
    the top of the page there). The bar becomes two rows: the title and bell on
    the first, search full width on the second.
  - Below 620px the date is hidden to keep that two-row shape.
  - Below 400px the title and bell shrink a step further.
  - Nothing overflows sideways at 360px.

### Dashboard (the reference page)

- **Grid:** main column plus a 223px right rail. Below 1240px it becomes one
  column with the rail first.
- **Top row:** two chart cards (Daily Sales Trend with a Whole Chicken / Cuts &
  Parts switch, and Low Stock Distribution).
- **Tabs:** Overview (default on every load; selection is not remembered),
  Forecasted Stock Depletion, Weekly Sales by Product, Stock Overview, and
  Recent Activity (Owner-only). Tab panels use `role="tabpanel"`; hidden charts
  are resized on the next animation frame after a tab switch.
- **Overview tab:** KPIs (Today's Sales, Forecast Accuracy) and Active Alerts.
- **Right rail:** Critical Alerts count, Current Stock, Quick Sale form.
- **Stock Overview on phones (520px and below):** each row becomes a compact card
  (product, stock, status) with a native `<details>` "More details" for the
  rest.
- Charts animate on first load only; polling refreshes do not replay the
  entrance.

### Dark card surfaces (shared skin)

The card surface of the shared skin, and the current visual target. It applies
to **card surfaces only**: Dashboard chart cards, KPI cards, rail cards
(Critical Alerts, Current Stock, Quick Sale), the Dashboard KPI row, and the
equivalent cards on other pages.

- **Surface:** `background-image: var(--card-dark-gradient)` over
  `background-color: var(--skin-black)`, `1px var(--card-line)`, radius from
  `--skin-radius-card` / `--skin-radius-inner`, one flat `--shadow-card`.
- **No blur, no translucency, no backdrop-filter.** The light edge that reads as
  glass is gone; the card is opaque.
- **Text:** `--card-ink` (white) or `--card-muted` (`#d8d8d8`). Nothing under
  0.8rem. Tables on a dark card use white text with 1px light dividers and the
  last row is **never** faded or masked.
- **Inputs on a dark card:** 1px light border, transparent or slightly lighter
  fill, white text, visible focus ring.
- **Buttons:** primary is the mid-gray `--skin-button`; secondary is light gray
  with dark text.
- **Tabs:** a pill bar holding pill tabs; the active tab is the most filled
  pill (light fill, dark ink).
- **`prefers-reduced-motion: reduce`:** no hover lift, transitions at 0.01ms.
- **One nested level, only where the mockup shows it.** The Stock Alerts summary
  cards are gray cards inside one dark gradient container, and the Dashboard's
  `.rail-frame` is a dark frame holding gray rail cards. Everywhere else is one
  card level.
- **Frames without text stay lighter.** `.rail-frame` and `.dash-sheet` carry no
  text of their own, so they keep `--skin-dark-top` (`#8a8a8a`) and sit closer to
  the mockup than the text-bearing cards can.

**Measured contrast on the dark card** (worst case = the top of the gradient,
`#5c5c5c`, the lightest point; 4.5:1 required for body text, 3:1 for figures
≥24px bold and for graphical objects):

| Text | Background | Ratio |
|---|---|---|
| `--card-ink #ffffff` (titles, values) | card top `#5c5c5c` | 6.69:1 |
| `--card-muted #d8d8d8` (secondary) | card top `#5c5c5c` | 4.69:1 |
| `#ffffff` on `--skin-button #4b4b4b` | button | 8.72:1 |
| `--cs-critical-text #5c1210` | `--cs-critical #ffb3ae` | 7.95:1 |
| `--cs-warning-text #4a2a06` | `--cs-warning #ffdca8` | 9.88:1 |
| `--cs-healthy-text #0d3a24` | `--cs-healthy #a8e8c4` | 9.11:1 |
| `--cs-critical-figure #ff9a96` | card top | 3.28:1 (large only) |
| `--cs-warning-figure #ffd08a` | card top | 4.66:1 |
| `--cs-healthy-figure #86e4b1` | card top | 4.38:1 |
| card top `#5c5c5c` | window `#bebebe` (edge separation) | 3.60:1 |

**Measured contrast on the light surfaces** (worst case = `#b5b5b5`):

| Text | Background | Ratio |
|---|---|---|
| `--text-on-light #242424` | `#b5b5b5` | 7.57:1 |
| `--text-muted-light #3d3d3d` (body/muted) | `#b5b5b5` | 5.30:1 |
| `--ls-warning #8a4b08` text on its fill | `#b5b5b5` | 6.79:1 |
| `--ls-healthy #1f6f45` text on its fill | `#b5b5b5` | 6.14:1 |
| table headers `#242424` | gray panel `#bababa` | 6.99:1 |

**Shell bar** (unchanged for now; worst case = the sheen end, over the window):

| Text | Background | Ratio |
|---|---|---|
| `--win-ink #1b1b1b` (page title) | shell bar, sheen end | 16.9:1 |
| `--gray-800 #242424` (date, search text) | shell bar, sheen end | 15.2:1 |
| `--gray-700 #4a4a4a` (search icon) | shell bar, sheen end | 8.7:1 |
| `--gray-600 #6b6b6b` (search placeholder) | shell bar, sheen end | 4.6:1 |

Text that sits **on the window** rather than on a card cannot use the base
`--muted` (3.3:1) or `--primary` (3.8:1) for small text; the shared skin uses
`--gray-700` there.

### Per-surface chart palettes

`dashboard.js` resolves a chart's colours from **its own canvas**
(`getComputedStyle(canvas)`), not from `:root`, so the two surfaces can carry
different palettes. `paletteFor(canvas)` builds the palette per chart and
`chartBaseOptions(palette, …)` / `chartAxis(palette, …)` take it as their first
argument. `chartColor()` remains only as the `:root` fallback.

This is required, not stylistic: a mid-tone that works on white is only ~1.3:1 on
a dark card, and a light tone that works on a dark card is ~1.4:1 on white. A
single palette cannot serve both.

| Token | Dark cards (`.glass-card`, `.dash-chart-card`) | White sheet (`.dash-sheet-inner`) |
|---|---|---|
| `--chart-tick` | `#dcdcdc` (4.88:1) | `#3d3d3d` (10.86:1) |
| `--chart-legend` | `#dcdcdc` | `#3d3d3d` |
| `--chart-grid` | `#8f8f8f` (decorative) | `#b5b5b5` (decorative) |
| `--chart-primary` | `#ff9a96` (3.28:1) | **`#b41818`** (6.81:1) |
| `--chart-secondary` | `#ffb3ae` | `#8f0d0d` |
| `--chart-tertiary` | `#ffc4c0` | `#6d4a47` |
| `--chart-quaternary` | `#ffd08a` | `#8a4b08` |
| `--chart-critical` / `-warning` / `-healthy` | `#ff9a96` / `#ffd08a` / `#86e4b1` | `#b41818` / `#8a4b08` / `#176b3a` |

**The red family is the primary line colour on both surfaces** — `#b41818` on
white, `#ff9a96` on a dark card. That is a brand requirement, not a free choice;
do not substitute another hue for the primary series.

| Chart | Surface | Tick | Legend | Series | Worst ratio |
|---|---|---|---|---|---|
| Daily Sales Trend | dark card | `#dcdcdc` | `#dcdcdc` | `#ff9a96` / `#ffb3ae` | 3.28:1 (line) |
| Low Stock Distribution | dark card | `#dcdcdc` | `#dcdcdc` | `#ff9a96` / `#ffd08a` / `#86e4b1` | 3.28:1 (slice) |
| Weekly Sales by Product | white sheet | `#3d3d3d` | `#3d3d3d` | `#b41818` / `#8a4b08` | 6.79:1 (bar) |
| Forecasted Stock Depletion | white sheet | `#3d3d3d` | n/a | `#b41818` | 6.81:1 (line) |

`--chart-*` on `:root` is left as it was; the per-canvas values come from the
card scopes. **Next consumer:** the Forecast page's inline script
(`Views/Forecast/Index.cshtml`, reading `--chart-primary` / `--chart-warning` /
`--chart-tick` from `:root`) still needs the same one-line change — read from the
canvas instead of `document.documentElement` — before that page gets its dark
card. It is untouched for now.


### Other patterns

- Sales Entry table is fluid (`.sales-entry`, `min-width: 0`, fixed layout,
  Quantity column protected at 18%) because the global `table { min-width:
  760px }` rule caused sideways scroll on iPad.
- Products page has Edit, **Restock** and Delete actions; Restock opens a modal
  (`#restockModalOverlay`) and posts to `Products/Restock` (Owner-only).
- Icons are inline SVG. No emoji as icons.
- Empty states are designed (small inline SVG plus message).

### Login / landing page

One scrolling page: hero ("Manok ni Rene" wordmark, tagline, **Login** and
**About** buttons that scroll to `#login-panel` and `#about-section`),
illustration panel (a 1080×1080 crop on phones), statement line, the login form
at `#login-panel`, three feature blurbs, and a one-line footer. It stays on the
light base tokens. Sections fade in with an `IntersectionObserver`
(`login-scroll.js`, repeating, staggered fields, disabled under reduced
motion). `login-video-background.css` and `Login.VideoBackground.cshtml` are
an alternate variant kept for rollback; `Login.cshtml` is the live page.

## Motion

- Hover/focus/press and panel entrances: short transitions, about 150–300ms,
  ease-out. Landing-page reveals are longer (about 1.4s).
- **`prefers-reduced-motion` must be respected everywhere.**
- Sales Entry shows a calm success confirmation after a sale.

## Hard constraints (any redesign work)

- **Look only.** Never change controllers, models, services, migrations, Razor
  logic, `asp-` tag helpers, element IDs, class names or `data-` attributes that
  JS uses, purely for styling.
- Must work down to 360px.
- Reuse tokens; no new one-off colors, fonts or radii per page.
- Avoid: purple/blue gradients, decorative concentric circles, tiny uppercase
  letter-spaced labels, cards nested in cards, emoji as icons, cream
  backgrounds by default.
- Contrast: text 4.5:1 (3:1 for 18px+ or 14px bold).

## Known deviations in the current Dashboard skin

These exist today. Fix them when touching the Dashboard; do not copy them to
other pages. Items marked **(fixed — shared skin)** are corrected for every page
on `.skin-page` and, where noted, no longer apply to the Dashboard either.

1. **Contrast:** muted text on the `--skin-card` and window grays is roughly
   3.3:1.
2. **Small text:** table headers about 0.62rem, table body 0.72rem, tab labels
   0.75rem.
3. **Gradients:** the wall, tab pills, sheet and rail frame use gradients.
4. **Nested cards:** rail frame wraps rail cards; the dark sheet wraps the white
   inner sheet.
5. **Tabs:** fixed 126px width wraps long labels; the active tab looks less
   clickable than inactive ones.
6. **Fragile height:** `.dash-sheet-inner` uses `calc(100dvh - 545px)`, which
   squeezes the Stock Overview table on shorter screens.
7. **Red is used for ordinary data:** chart lines use alarm red
   (`--chart-primary`), against the "status colors only" rule. `--skin-critical`
   also differs slightly from `--critical`.
8. **Remaining hardcoded values:** the light-surface variable block in
   `.dashboard-page` (the one that redefines `--text`, `--primary`, status
   colors for light cards) still has literal hex and rgba values that mostly
   duplicate the light base tokens.
9. **Copy changes:** the Dashboard nav label reads "Stocks & Alerts" and
   "Sign Out", while the rest of the app says "Stock & Alerts" and "Sign out".
10. **Count badges do not meet text contrast.** The dark theme's `--critical`
    (`#FF7670`) is a light coral, so the white count on it is 2.6:1 — below the
    4.5:1 floor and even below 3:1. This affects `.nav-badge`,
    `.notification-badge` and `.status-critical` alike, and it predates the glass
    work. Fixing it means changing `--critical` itself, which moves every status
    badge in the app, so it is called out here rather than changed quietly.

### Status after the dark-card pass (Dashboard + Stock & Alerts)

What the shared skin does **not** carry forward, so no other page inherits it:

- **Muted text** on a light surface is `--text-muted-light #3d3d3d` (5.30:1 worst
  case) instead of `--skin-card` grays at ~3.3:1.
- **No text under 0.8rem.** Shared tabs, KPI labels, compact-card labels and
  table headers are all at or above `--text-xs`. The Dashboard's plotting sheet
  no longer uses its 0.62/0.72rem sizes.
- **Cards are opaque.** No translucent fill, no blur, no gradient-over-blur. The
  card gradient itself is deliberate and is the mockup's own treatment.
- **One card level**, except the two places the mockup shows a nested one:
  `.rail-frame` (dark frame, gray rail cards) and the Stock Alerts summary strip
  (dark container, gray stat cards).
- **Tabs** are pills in a pill bar; the active tab is the most filled control in
  the row, and no label wraps.
- **No `calc(100dvh - …)` height** anywhere; sizes come from content. (The shell's
  `calc(100dvh - 42px)`-era fixed panel is a separate, pre-existing item.)
- **Status colour** stays on stock/alert meaning only, in both surface sets. The
  alarm-red chart line is still deviation 7 and is unchanged in hue family.
- `Stock & Alerts` says "Stock & Alerts" in the nav and titles; the sidebar's
  "Sign Out" wording is still deviation 9.

Notes for the next page:

- **Text on the window needs `--gray-700`.** On `#bebebe` the base `--muted` is
  3.3:1 and `--primary` is 3.8:1, so a subtitle or greeting in either fails.
- **Pick the status set by surface, not by taste.** `--cs-*` for dark cards,
  `--ls-*` for light ones. They are not interchangeable.
- **Coming over from modules.css:** those pages style `.stat-strip .kpi-card` and
  their hover shadows with `:has(#productsBody)` / `:has(#forecastProduct)`,
  which outrank the shared `.skin-page` rules. Re-check that block for any page
  moved to the skin, or the new card fill is silently dropped.
- **The chart palette is per canvas.** A page whose chart sits on a new surface
  must set `--chart-*` on the chart's container, or the chart keeps whatever the
  nearest ancestor provides.
- **The shell's fixed-height panel** (`calc(100dvh - 42px)` with an inner scroll)
  predates all of this and still squeezes a long table on a short screen. Worth
  fixing shell-wide rather than per page.

## Verification checklist (repeat for any visual change)

Compiling or a passing diff is **not** verification. Run the app and check:

1. Stop the server, `dotnet clean`, `dotnet build`, `dotnet run` from the
   project folder (a stale build once served old markup).
2. Click through every page at 360px, then at iPad width.
3. Turn on OS "reduce motion", reload, click through again.
4. Full flow: log in, Dashboard, mobile sidebar, Sales Entry, log a sale,
   Notifications, filter, an empty state.
5. As Owner and as Employee (owner-only items must stay hidden for Employee).
6. Wrong password (error state).
7. Products, Restock a product: stock and alert badge update.
8. Forecast page loads and writes its daily snapshot.

## Tooling notes

- Edits are now made with DeepSeek; earlier passes used Codex with the
  Impeccable plugin. Whatever the tool: ask for a plan first, one page at a
  time, and diff against a backup. AI tools usually cannot open a browser, so
  "verified" in their reports does not mean seen on screen.
- `npx impeccable install` hit an upstream bug (bundle verification HTTP 404,
  `pbakaus/impeccable#479`). Do not bypass signature verification.
- Imported "DESIGN.md library" files are reference for layout rhythm and type
  scale only; this file stays the source of truth.

## Open items

- Decide the end state for the rest of the system (move all pages to the
  Dashboard skin, or soften the skin) and then bring pages over one at a time:
  Sales Entry, Sales History, Forecast, Products, Audit Trail, Notifications,
  AccessDenied. (Stock & Alerts and the Dashboard are on the shared dark-card
  skin as of this pass; the Dashboard keeps its own grid and layout.)
- Fix the Known deviations above.
- **Forecast page is the next chart consumer.** Its inline script
  (`Views/Forecast/Index.cshtml`) still reads `--chart-primary`,
  `--chart-warning`, `--chart-tick` and `--chart-grid` from
  `document.documentElement`. It needs the same one-line change `dashboard.js`
  already has — resolve from the canvas instead of `:root` — before that page's
  chart moves onto a dark card. Left untouched in the dark-card pass.
- **Forecast is also still on the old dark theme** (`.dashboard-body` with no
  page class). Its summary cards and panels therefore keep the dark
  `--panel #24272D` with light `--text #F3F4F6`. **Any new dark-card rule must be
  scoped to `:is(.dashboard-page, .skin-page)`** — a bare `.glass-card` or
  `.dash-kpi` rule is what leaks dark ink onto these pages.
- Header search placeholder says "Search Products, Alerts, or Sales…" even for
  Employees, who cannot get product results; make it role-aware.
- Search suggestion chips are static strings and can go stale.
- Confirm you have the rights to `wwwroot/videos/login-video.mp4` before
  submission.
- Retry `npx impeccable install` later.
