# tools

- `Capture-Window.ps1` — Windows: captures the running app's window to PNG.
- `IsoParity/` — proves `Stage/Iso.cs` is an exact port of Hairline's `core/iso.ts`.
  Runs the same cases through the C# port and through the Hairline kernel in Node
  and compares every number (tolerance 1e-9):

  ```sh
  cd tools/IsoParity
  dotnet run -c Release | tail -1 > cs.json
  node node-side.mjs ~/.claude/skills/hairline-create/kernel.js > js.json
  node compare.mjs
  ```

  The kernel path is the `hairline-create` skill's `kernel.js` (installed with
  `npx skills add lucasmarkes/hairline`); it defaults to the user-level skill folder.
- `StageRender/` — renders the stage off-screen with SkiaSharp at separation 0, 50 and
  100, with the same `PlateRenderer` the app uses, so a change to the drawing can be
  looked at on any OS without launching the app. Writes `stage-*.png` to the folder
  given as its argument (`dotnet run -c Release -- ../../shots`). The WinUI types the
  stage classes touch are shimmed in `Shims.cs`.
