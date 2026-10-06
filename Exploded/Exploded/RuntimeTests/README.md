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

## Status

5 of 8 pass. The three tests that tap a row with injected pointer input fail:
the press, release and Tapped events reach the page at the row's centre, but the
selection does not change. Under investigation; see the commit that added these
tests.
