# Exploded

An exploded-view parts catalog for a 60% mechanical keyboard, built with Uno Platform. A slider pulls the five layers of the build (case, PCB, plate, switches, keycaps) apart along the build axis; each layer has a numbered callout, a row in the parts table with SKU, price and stock status, and can be added to a build with a running total.

![Assembled plate](Exploded/shots/plate-assembled.png)

## What's in it

| Path | Purpose |
|---|---|
| `Exploded/Exploded/` | The app (Uno single project, one page) |
| `Exploded/Exploded/Catalog/` | `Part` record, `IPartsCatalog` and a hardcoded in-memory kit of five parts |
| `Exploded/Exploded/Stage/` | Explode math (`SheetMatrix`, callout fade, leader lines), key layout table, and the SkiaSharp plate renderer |
| `Exploded/Exploded/Presentation/PartRow.cs` | Observable row object for the parts table (selected / in build) |
| `Exploded/Exploded/Themes/Tokens.xaml` | Color, type, spacing and shape tokens |
| `Exploded/EXPLODED-SPEC.md` | Architecture, design and interaction brief plus implementation plan |
| `Exploded/tools/Capture-Window.ps1` | Windows script that captures a running app window to PNG |
| `Exploded/shots/` | Screenshots |

## Tech

- Uno Platform single project, `Uno.Sdk` 6.8.0-dev.12 (`global.json`), target `net10.0-desktop` only
- `UnoFeatures`: `SkiaRenderer; Mvux`
- The stage is a single `SKCanvasElement` (`PlateCanvas`). Each sheet is recorded once into an `SKPicture` and replayed under its own matrix, so moving the slider is one invalidation. The spec explains why XAML `Path` layers were dropped (per-frame cost on the UI thread).
- Separation drives the leader lines, callout fade and stage hint through `x:Bind` function bindings on `MainPage`.
- Selection and build state are plain code-behind over five `PartRow` objects. The spec plans an MVUX `BuildModel`; it is not in the code yet.
- Fonts: static IBM Plex Sans, Sans Condensed and Mono TTFs in `Assets/Fonts`.
- Central package management is on; `Directory.Packages.props` is empty (Uno implicit packages).

## Run it

Requires the .NET 10 SDK.

```
dotnet build Exploded/Exploded.sln
dotnet run --project Exploded/Exploded/Exploded.csproj -f net10.0-desktop
```

The spec notes that with a dev-channel Uno.Sdk a Debug build launched without the dev server may show no window.

## Status

Prototype. Implemented:

- The plate, explode slider, callouts, two-way selection between stage and table, add/remove with a build total.
- Narrow layout below 1000 px: stage on top at a capped height, slider and title block under it, parts table filling the rest with the total and Add pinned at the bottom. On desktop the window floor is 900 px (a Win32 render-thread workaround in `App.xaml.cs`), so narrow shows between 900 and 999 px.
- Keyboard: rows are tab stops, focusing a row selects it, Up/Down move between rows, Enter/Space add or remove the focused part. Rows announce number, name, spec, price, stock and build state to screen readers.

Not yet: the MVUX `BuildModel` and `FeedView` states from the spec.

![Narrow layout](Exploded/shots/narrow.png)
