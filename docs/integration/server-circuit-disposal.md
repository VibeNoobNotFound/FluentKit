# Server circuit-disposal verification

This runbook verifies the server-side failure mode fixed in FluentKit 0.2.4: a component tree
containing `ThemeProvider` and `FluentMicaPanel` is disposed after a normal HTTP form post redirects
the browser to another page.

Use any Blazor Server consumer that renders the components with
`InteractiveServerRenderMode(prerender: false)` and has a normal HTTP form/navigation flow.

## Run with a locally packed FluentKit

From the FluentKit repository:

```bash
FEED_DIR="$(mktemp -d)"
dotnet pack src/FluentKit/FluentKit.csproj -c Release -o "$FEED_DIR"
```

In the consumer checkout, temporarily set its central `FluentKit.Blazor` package version to `0.2.4`,
then restore using the temporary feed and NuGet.org:

```bash
CONSUMER_DIR="${CONSUMER_DIR:-../consumer}"
dotnet restore "$CONSUMER_DIR/Consumer.slnx" \
  --source "$FEED_DIR" \
  --source https://api.nuget.org/v3/index.json
```

Start the consumer host using its normal local configuration and prerequisites. Do not commit the
temporary package-version change.

## Verification steps

1. Open an interactive page containing the theme chrome and confirm it is interactive.
2. Submit the page's normal HTTP form.
3. Confirm the response redirects to the requested return URL and the destination loads normally.
4. Inspect the server log around the redirect. There must be no `CircuitHost[111]` aggregate
   exception caused by `JSDisconnectedException` from FluentKit disposal.
5. Repeat after changing the theme and while the Mica panel is rendering, covering both the event
   callback and delayed-render shutdown races.

The expected result is successful sign-in/navigation with no unhandled circuit-disposal exception;
client-side browser cleanup is allowed to be skipped when the circuit is already gone.
