# Runtime tests

In-app UI tests on the real page, run by `Uno.UI.RuntimeTests.Engine`. They
compile only when the build is given `-p:RuntimeTests=true`, and only for
`net10.0-desktop`; a normal build contains neither the engine nor these files.

```
dotnet build Exploded/Exploded.csproj -c Release -f net10.0-desktop -p:RuntimeTests=true
UNO_RUNTIME_TESTS_RUN_TESTS='{}' UNO_RUNTIME_TESTS_OUTPUT_PATH=runtime-tests.xml \
  xvfb-run --auto-servernum --server-args='-screen 0 1280x1024x24' \
  dotnet Exploded/bin/Release/net10.0-desktop/Exploded.dll
```

Results are written as NUnit XML. On Windows, run the same build and set the two
environment variables before starting `Exploded.exe`.

Uno's input injector has no keyboard on Skia, so key behaviour is driven through
`MainPage.HandleRowKey`, and focus moves use `FocusState.Keyboard` (what a Tab
produces). Pointer input is injected for real.

## Coverage

Load state; tapping a row selects it on the table, the stage and the button;
Add marks the part and counts the total; an out-of-stock part disables Add;
keyboard focus selects a row and survives the row being re-emitted; Down/Up
walk the table; Enter/Space toggle with focus kept on the row.

The tap tests found a real regression on their first run: the focus manager's
fallback after a re-emit was being treated as a user selection and undid every
click. Rows now select on focus only when the focus came from the keyboard.
