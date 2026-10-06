# EXPLODED — Spec

An exploded-view parts catalog for custom mechanical keyboards. The user scrubs an
assembly apart along its build axis, every layer stays labelled and priced, and the
selected layer is a real SKU that can be added to a build.

Derived from the StrataApp composition study (`C:\Users\Platform006\UnoProjects\strata\StrataApp`),
which proved the tilt-and-explode mechanic on three flat sheets. This spec takes that
mechanic into a real-world scenario.

---

## Decision: new project, not a fourth section in StrataApp

**Decision:** scaffold a separate Uno single project, `Exploded`, at
`C:\Users\Platform006\UnoProjects\exploded\Exploded`. Lift `SheetMatrix` from
`StrataApp/MainPage.xaml.cs` by copy, not by shared library.

**Reason:** StrataApp teaches one thing — a card built from three nested templated
controls. Grafting a commerce screen onto it destroys that narrative and forces
navigation into a page that has none. The two also want opposite visual identities:
Strata is a dark specimen bench, Exploded is a printed service manual.

**Tradeoff:** the matrix math is duplicated in two repos. Accepted — it is 30 lines
with no dependencies, and the two apps will tune tilt/gap differently anyway (3 flat
sheets vs 5 physical parts). Extracting a shared package before there is a second
consumer that actually needs to stay in sync would be premature.

---

## Architecture Brief

### App / module structure

Uno Platform Single Project, Skia renderer.

```
Exploded/
  App.xaml(.cs)
  MainPage.xaml(.cs)          the whole app; one screen
  Catalog/
    Part.cs                   record: id, no, name, spec, sku, price, stock
    Build.cs                  record: name, form factor, revision, parts
    IPartsCatalog.cs
    InMemoryPartsCatalog.cs   the 60% tray-mount build, hardcoded
  Stage/
    Explode.cs                SheetMatrix / TagFade / stage hints (ported)
    KeyLayout.cs              key positions for the caps + switches layers
  Presentation/
    BuildModel.cs             MVUX model
  Themes/
    Tokens.xaml               colors, type, spacing, shape
    Manual.xaml               callout, title block, parts-row styles
  Assets/Fonts/               static per-weight TTFs
```

### Versions (pinned, known-good from StrataApp)

| Thing | Value |
|---|---|
| `Uno.Sdk` (global.json) | `6.8.0-dev.12` |
| TFM | `net10.0-desktop` only, to start |
| `UnoFeatures` | `SkiaRenderer; Mvux;` |
| Central package management | on, `Directory.Packages.props` empty (Uno implicit packages) |
| `Nullable` / `ImplicitUsings` | enable / enable |

Dev-channel Uno.Sdk means the **hidden-window gotcha applies**: a Debug build launched
without the devserver shows no window. Always launch through `uno_app_start`.

### State model — MVUX

**Decision:** MVUX for the catalog surface.

**Reason:** the screen is a loaded list, a selection derived from it, and a mutable
build list with a computed total. That is `IFeed` / `IState` / `IListState` shaped
exactly, and `FeedView` supplies loading, empty and error states without a hand-rolled
state machine. A real parts catalog is network-backed, so the demo should be honest
about that shape.

**Tradeoff:** the explode amount must stay **out** of MVUX. It changes continuously
while dragging, and pushing per-frame values through a feed pipeline to recompute five
matrices is the wrong tool. It stays a plain `x:Bind` function binding against a
code-behind method, exactly as StrataApp does it. The page therefore runs two state
systems side by side and needs a comment saying why.

**Rejected:** MVVM with CommunityToolkit. It fits the "single-page tool" clause of the
scaffolding rule, but the catalog-load and build-list shape would end up reimplementing
what `IListState` and `FeedView` already do.

```
BuildModel (partial record)            as built
  IFeed<PartsKit>       Kit            catalog load
  IState<int>           SelectedLayer  0..4, the sheet index; drives stage + list
  IState<BuildList>     Build          what the user added, with Total / Hint
  IListFeed<PartLine>   Lines          Kit x SelectedLayer x Build; FeedView owns its states
  IFeed<string>         KitName, FormFactor, Revision, TotalLabel, BuildHint
  IFeed<PartAction>     Action         primary button label + enabled
  ValueTask ToggleSelected()           command, reads SelectedLayer from state
```

As-built deviations:

- The build is `IState<BuildList>`, not `IListState<Part>`. An empty list state reads
  as None, and an empty build is a real value with a $0.00 total.
- Add and Remove are one `ToggleSelected` command; the button label says which.
- Rows are immutable `PartLine` records re-projected on every selection or build
  change. The table replaces the changed rows, which throws away a focused row, so the
  page holds the focused row's key and re-focuses its replacement on `Loaded`.
- The generated view model is held in a private page property. A public member of an
  MVUX-generated type fails the XAML bindable-metadata generator.

Rule already burned in and to be respected: **do not pass the bound item as a
`CommandParameter` from inside a FeedView template** — it silently no-ops. `AddSelectedAsync`
reads `SelectedLayer` from state instead.

### Navigation model

None. One page, no regions, no routes. If a part-detail sheet is added later it is a
`ContentDialog` subclass, and the implicit-style trap applies (an implicit
`Style TargetType="ContentDialog"` never reaches a subclass — key the style and assign
it at construction).

### Services / dependencies

`IPartsCatalog` with an in-memory implementation, constructed by the model. **No DI
container for the prototype** — one service with one implementation does not need
`IHostBuilder`. If a second service appears, add `Hosting` to `UnoFeatures` then.

No Toolkit, no Material. The visual identity is hand-built from tokens; pulling in
Material only to override all of it costs more than it saves.

### Data flow

```
InMemoryPartsCatalog ──async──> IFeed<Build> ──> FeedView ──> parts list
                                     │
                                     └──> IListState<Part> ──> row selection
                                                  ▲   │
                            stage sheet tapped ───┘   └──> IState<int> SelectedLayer
                                                             │
                    x:Bind SheetMatrix(Sep.Value, n) ────────┴──> 5 MatrixTransforms
```

Selection is bidirectional: tapping a sheet on the stage selects the parts-list row,
and selecting a row highlights the sheet. Both write `SelectedLayer`.

### Rendering approach

Each layer is a `Grid` holding flat plan-view geometry, carrying one `MatrixTransform`
fed by `SheetMatrix(separation, index)`. Five layers, back to front:

| # | Layer | Geometry | Element count |
|---|---|---|---|
| 5 | Case | one rounded slab + port cutout | ~4 |
| 4 | Plate | slab with 61 switch cutouts | ~62 |
| 3 | PCB | board outline, traces, 61 sockets | ~70 |
| 2 | Switches | 61 housings | 61 |
| 1 | Keycaps | 61 caps, legends off | 61 |

Key positions come from a single `KeyLayout` table (unit-based: 1u = 19.05 units of
design space) and are stamped into a `Canvas` per layer in code. One table, five
consumers.

**Constraint, not a risk:** every layer is a flat plan view. `SheetMatrix` is affine
per sheet, so a rectangle stays a parallelogram and real part thickness is not
modelled. That is exactly what an exploded plate in a service manual does, so the
constraint and the archetype agree. No part gets extruded.

**As built (Hairline port):** the tilt-and-spin sheet matrix is replaced by an
orthographic isometric camera (azimuth 30, elevation sin 0.5) and real solids:
a tray case, PCB with sockets and USB-C port, plate with cutouts, tapered switch
housings with stems, and sculpted keycaps tapered from foot to top. The geometry
is a C# port of Hairline's `core/iso.ts` (MIT). Every layer rises straight up, a
pure screen translation in this camera, so the one-picture-per-layer replay is
kept. Azimuth is 30 rather than Hairline's 45 because this stage is a 2.2:1
strip and 45 left the drawing height-bound at a third of the width.

### Platform constraints to design around

- `TextBlock.CharacterSpacing` is a **no-op** on the Uno Skia text stack. Do not plan
  letterspaced drafting labels; pick type that reads without tracking, or lay out one
  character per `TextBlock` in a `StackPanel` with `Spacing` where it truly matters.
- Variable-font TTFs render only their default instance. Ship **static per-weight
  TTFs**, one `FontFamily` resource per file, and verify each file's internal family
  name before wiring it.
- `Path.Data` rejects **elliptical** arcs at parse time and takes the app down before
  first frame. Callout bubbles are `Ellipse` elements or circular arcs only.
- No `RepeatBehavior="Forever"` storyboards and no held `CompositionTarget.Rendering`
  subscription. Both pin the compositor at display refresh for the life of the process
  (~30% of a core for one trivial animation, measured). Any continuous motion runs off
  a shared `DispatcherTimer` and is `Stop()`ped on state exit.
- `AppWindow.MoveAndResize` is a silent no-op on desktop. Do not size the window in
  code; let the layout fit whatever the window is.

### Testing / validation

Runtime verification through the uno-app MCP, no screenshot requests to the user:

1. Visual tree: five sheet `Grid`s present, five callout bubbles, parts list row count
   equals part count.
2. `uno_app_element_peer_action setRangeValue` on the separation slider at 0 / 50 / 100,
   screenshot each; assert the stage hint text changes and callouts fade in.
3. Peer-select a parts row, assert `SelectedLayer` changed by reading the selected
   sheet's visual state.
4. Tap a sheet on the stage (pointer click at client coordinates from a `detail=full`
   snapshot), assert the matching row highlights.
5. Invoke Add, assert the build total text changed.

---

## Design Brief

### Subject grounding

**Subject:** custom mechanical keyboards. **Audience:** people who buy parts, not
finished products. **The surface's single job:** show what a build is made of, in
order, with a price on every layer.

The vernacular is not e-commerce. It is the **exploded plate from a service manual**:
line art, numbered callouts on leader lines, a parts table keyed to those numbers, and
a title block in the corner carrying drawing name, revision and scale.

### Anti-default calibration

First plan was paper-cream ground, condensed serif, terracotta accent. That is the
named default cluster, so it was thrown out. Second pass drew the palette from the
**materials the parts are actually made of** — anodized aluminium, PBT plastic, FR4
fibreglass, steel — which produces a studio-sweep grey and an unmistakable FR4 green
that nobody arrives at by default. Safety orange survived the cull, but only because
orange leader lines are the drafting convention this whole archetype is borrowed from;
it is confined to the callout system and the selected state and decorates nothing.

Out-of-stock is deliberately **not** a colour. It is a diagonal hatch over the swatch
plus a text label, which also satisfies "statuses never colour-alone" for free.

### Color

| Token | Hex | Role |
|---|---|---|
| `Sweep` | `#D9D7D2` | stage ground, the product-shoot sweep |
| `Paper` | `#F0EEE9` | parts list panel, title block |
| `Ink` | `#1B1E1C` | line art, primary text |
| `InkDim` | `#6C6F6B` | specs, secondary text |
| `InkFaint` | `#A3A6A1` | rules, hairlines, disabled |
| `Callout` | `#D2481E` | leader lines, callout bubbles, selected sheet edge |
| `Board` | `#2E6B4F` | the PCB layer's own colour; also "in your build" |
| `Hatch` | `Ink` @ 12% | out-of-stock diagonal fill |

One filled-`Callout` button per view. Everything else is hairline and ink.

### Typography

One superfamily, three roles. IBM Plex is engineering vernacular and avoids the
default-sans cluster.

| Role | Face | Use |
|---|---|---|
| Display / labels | IBM Plex Sans Condensed SemiBold | callout numbers, section labels, title block |
| Body | IBM Plex Sans Regular | part names, spec lines |
| Data | IBM Plex Mono Regular / Medium | SKUs, prices, dimensions, quantities, totals |

Static per-weight TTFs in `Assets/Fonts`, one `FontFamily` resource per file. Prices
and totals are mono so digits stay in columns.

### Spacing and shape

Same discipline as Strata, different numbers: an 8-based scale (8 / 16 / 24 / 40 / 64)
because drafting sheets are gridded, not fibonacci. Shape is nearly square —
`Shape.Panel` 4, `Shape.Swatch` 2. Rounded corners are a product-UI tell; a technical
plate has none to spare.

### Layout archetype: the plate and the parts table

Wide (>= 1000):

```
┌────────────────────────────────────────────────┬──────────────────────────┐
│  # EXPLODED · 60% TRAY MOUNT           REV B   │  PARTS LIST              │
│                                                │  ──────────────────────  │
│                                                │  1  Keycaps         ●    │
│              ▁▁▁▁▁▁▁▁▁ ───────── (1)           │     PBT dye-sub          │
│             ▁▁▁▁▁▁▁▁▁ ───────── (2)            │     KC-PBT-60   $139.00  │
│            ▁▁▁▁▁▁▁▁▁ ───────── (3)             │  ──────────────────────  │
│           ▁▁▁▁▁▁▁▁▁ ───────── (4)              │  2  Switches    $ 62.00  │
│          ▁▁▁▁▁▁▁▁▁ ───────── (5)               │  3  PCB         ▨ o/s    │
│                                                │  4  Plate       $ 48.00  │
│  ┌────────────────────┐                        │  5  Case        $163.00  │
│  │ EXPLODED VIEW      │ SEPARATION ●━━━━━━━━━  │  ──────────────────────  │
│  │ 60% TRAY MOUNT     │ ────────────────────   │  BUILD TOTAL    $412.00  │
│  │ SCALE 1:1   REV B  │ SHEET 1 OF 1           │  [ Add keycaps to build ]│
│  └────────────────────┘                        │                          │
└────────────────────────────────────────────────┴──────────────────────────┘
```

Narrow (< 1000): stage on top at reduced height, separation slider directly under it,
parts list scrolling below, build total pinned as a bottom bar. The title block moves
under the stage and loses the scale line.

**Not** an identity band with a filled primary top-right over a card grid. The two
columns are a drawing and its parts table, which is the archetype's own geometry.

### Component hierarchy

```
Page
  Grid (2 col: stage 1.5*, table 1*)
    Border  Stage
      Canvas  Perspective host (fixed design size, wrapped in a Viewbox)
        Grid  Layer 5..1, each with MatrixTransform + Canvas of parts
        Path  leader line per layer
        Ellipse + TextBlock  callout bubble per layer
      Border  Title block (bottom-left, absolute)
      Slider  Separation (bottom, absolute)
    Border  Parts panel
      FeedView  Build
        ItemsRepeater  parts rows
      Border  Total + Add
```

The perspective host sits in a `Viewbox` at a fixed design size, so every measurement
in the stage is written at its real number and scales as one body. That pattern is
validated — a `Viewbox` does carry child transforms and keeps XAML and geometry
pixel-registered.

### Signature element

**The callout system.** Numbered orange bubbles ride a leader line out of each layer's
right edge, stay horizontal while the layer tilts away underneath, and light the
matching row in the parts table. It is the one thing this app is remembered by, so
everything else stays hairline-quiet. The title block is the supporting note, not a
second signature.

---

## Interaction Brief

### Primary flow

1. Land assembled. The stage reads as a product photo: five layers stacked flat, no
   callouts, title block present, separation at 0.
2. Drag separation. Layers tip back and separate; callouts fade in past a third open
   (same `TagFade` gate as Strata) so an assembled view is never cluttered.
3. Tap a layer, or a parts row. Both select. The sheet gets a `Callout` edge, the row
   highlights, the Add button relabels to name the part.
4. Add to build. The part joins the build list, the row shows an "in build" mark in
   `Board`, the total updates.
5. Remove from the build list to reverse it.

### Input behavior

- Separation: `Slider`, plus horizontal drag anywhere on the stage as a shortcut.
  Keyboard arrows step 5, Home/End snap to assembled / fully exploded.
- Selection: pointer tap on a sheet, tap on a row, or Up/Down through rows.
- Focus: visible focus ring on rows and on the Add button, drawn in `Callout`.
- Hit-testing on tilted sheets goes through the `RenderTransform` (see Risks).

### States

| State | Treatment |
|---|---|
| Loading | `FeedView` progress: the stage draws layer outlines in `InkFaint` hairline, the table shows five skeleton rows. The plate is always present, only the data is missing. |
| Empty | Not reachable — the catalog is a fixed build. If it ever loads remotely: "No parts in this drawing." plus a Reload. |
| Error | Title block reads `REV —`, table shows the message and a Retry. **Retry must be `Command="{Binding Refresh, ElementName=TheFeed}"`** — inside `ErrorTemplate` the DataContext is the thrown Exception, so `{Binding Refresh}` and `{Binding Parent.X}` are both silently dead buttons. |
| Out of stock | Diagonal hatch over the row swatch, `o/s` label, Add disabled with the reason in text. |
| Empty build | "Nothing added yet. Tap a layer to price it." |

### Motion

Numbers come from `xaml-design-polish` at build time; the brief here is only what
moves:

- Separation follows the drag with no easing — it is a direct manipulation, and
  interpolation would feel like lag.
- Callout fade is a short opacity ramp gated on separation, not a timed animation.
- Selection edge and row highlight cross-fade fast.
- Add-to-build: the row's swatch marks in `Board`; the total counts rather than
  snapping.
- Nothing loops. No idle animation anywhere on this screen, for the compositor reason
  in the Architecture Brief.
- Everything gates on `UISettings.AnimationsEnabled`.

### Accessibility

- Every layer carries `AutomationProperties.Name` ("Layer 1, keycaps, PBT dye-sub, 139 dollars").
- Callout numbers are real text, not drawn glyphs, so they are readable and scalable.
- Stock status is hatch plus label, never colour alone.
- The full flow is keyboard-operable: tab to the list, arrow to a part, Enter to add.
- Stage targets are large; the smallest tap target is a whole layer.
- Contrast checked on `Sweep` and `Paper`, both light grounds.

### Runtime verification steps

As listed in the Architecture Brief's Testing section — driven from the uno-app MCP,
not delegated to the user.

---

## Implementation Plan

1. **Scaffold.** `dotnet new unoapp -preset recommended` into
   `C:\Users\Platform006\UnoProjects\exploded`. Copy `~/.claude/templates/uno/.mcp.json`
   beside the `.sln` **before the first build**. Set TFM, `UnoFeatures`, global.json to
   the pinned versions. `dotnet build` and confirm it compiles before anything else.
2. **Tokens and fonts.** `Themes/Tokens.xaml` with the palette, type styles, 8-scale
   spacing, shape. Source and verify the static Plex TTFs. Prove one screen of type
   before any geometry.
3. **Static assembled plate.** `KeyLayout` table, five layers drawn flat and stacked,
   title block. No motion, no data. This is the make-or-break visual step — if the
   plate does not read as a keyboard here, nothing later saves it.
4. **Explode.** Port `SheetMatrix` / `TagFade`, add the separation slider, retune
   `TiltDegrees` / `SpinDegrees` / `SheetGap` for five layers rather than three.
5. **Callouts.** Leader lines, numbered bubbles, fade gate, selection edge.
6. **Catalog.** `IPartsCatalog`, `BuildModel`, `FeedView` with all four states, parts
   table bound and selection wired both directions.
7. **Build list.** Add / remove, in-build mark, running total.
8. **Narrow layout**, accessibility pass, reduced-motion gate.
9. **uno-verify pass**, then commit.

Steps 1 to 4 are the prototype. Stopping there still answers "does this mechanic work
for a real product," which is the actual question behind the build.

---

## Risks

| # | Risk | Time box | If it blows |
|---|---|---|---|
| 1 | Hit-testing a `MatrixTransform`-tilted sheet may not map pointer coordinates back correctly on Skia desktop, which would break tap-to-select on the stage. | 10 min spike in StrataApp itself — put a handler on a tilted sheet and click it at full separation | Fall back to selection from the parts table only, and make the callout bubble the stage-side target since it is untransformed. |
| 2 | ~260 shapes across five layers, each under its own transform, may cost more per frame than expected while dragging. | 10 min: build step 3 with the full key count and drag | Draw the caps and switch layers as a single `Path` geometry each instead of per-key elements. |
| 3 | Static IBM Plex Condensed / Sans / Mono TTFs must be sourced as real static instances, not variable fonts, with internal family names verified. | 15 min | Fall back to Segoe UI for body and keep mono for data only; the identity leans on the callout system, not the type. |
| 4 | MVUX `{Binding}` surface and `x:Bind` function bindings coexisting on one page. | 5 min | Low risk: `x:Bind` compiles against the Page and never touches the MVUX proxy, so they do not collide. Documented here so the build session does not "fix" it. |

Two failed cycles on any of these trips the Debug Checkpoint rule rather than another
blind attempt.

---

## Unresolved Questions

- **Form factor.** Assumed a 60% tray-mount build, five layers, 61 keys. Confirm, or
  name a different product (a gasket-mount build adds a gasket and weight layer, which
  is a better explode but a harder draw).
- **Camera.** Fixed tilt and spin, as in StrataApp, or drag-to-orbit? Fixed is cheaper
  and matches the service-manual archetype. Recommend fixed for the prototype.
- **How far does commerce go?** Selection plus a running total, or a real build list
  with remove, quantity and a checkout affordance? Spec covers the former plus add and
  remove; anything past that is a second session.
- **Repo.** Separate git repo, or a sibling folder under the existing `UnoProjects`
  tree with its own repo? Assumed the latter.
- **Does the keycaps layer show legends?** Legends make the plate read as a keyboard
  instantly but add 61 text elements under a transform. Recommend legends off for the
  prototype, decided by risk 2's measurement.
