# Server circuit-disposal verification

This runbook verifies the server-side failure mode fixed in FluentKit 0.2.4: a component tree
containing `ThemeProvider` and `FluentMicaPanel` is disposed after a normal HTTP Identity form post
redirects the browser to another page.

The Aula consumer checkout is the reference host. Its
`Aula.ApiService` renders `AccountThemeChrome` and `FluentOverlayHost` with
`InteractiveServerRenderMode(prerender: false)`, while sign-in uses the ordinary
`/account/actions/sign-in` HTTP POST.

## Run with a locally packed FluentKit

From the FluentKit repository:

```bash
FEED_DIR="$(mktemp -d)"
dotnet pack src/FluentKit/FluentKit.csproj -c Release -o "$FEED_DIR"
```

In the local Aula checkout, temporarily set its central `FluentKit.Blazor` package version to
`0.2.4`, then restore using the temporary feed and NuGet.org:

```bash
AULA_DIR="${AULA_DIR:-../Aula}"
dotnet restore "$AULA_DIR/Aula.slnx" \
  --source "$FEED_DIR" \
  --source https://api.nuget.org/v3/index.json
```

Start the existing Aula API host using its normal local configuration and prerequisites. Do not
commit the temporary package-version change to Aula.

## Verification steps

1. Open the Aula sign-in page and confirm the account theme chrome is interactive.
2. Submit valid credentials through the normal HTTP sign-in form.
3. Confirm the response redirects to the requested return URL and the destination loads normally.
4. Inspect the server log around the redirect. There must be no `CircuitHost[111]` aggregate
   exception caused by `JSDisconnectedException` from FluentKit disposal.
5. Repeat after changing the theme and while the Mica panel is rendering, covering both the event
   callback and delayed-render shutdown races.

The expected result is successful sign-in/navigation with no unhandled circuit-disposal exception;
client-side browser cleanup is allowed to be skipped when the circuit is already gone.
