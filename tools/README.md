# FluentKit maintenance tools

## Generate the API reference

Build the library first, then run:

```bash
dotnet run --project tools/FluentKit.ApiReferenceGenerator -c Release -- \
  --assembly src/FluentKit/bin/Release/net10.0/FluentKit.dll \
  --xml src/FluentKit/bin/Release/net10.0/FluentKit.xml \
  --manifest docs/integration/manifest.json \
  --json docs/reference/api.json \
  --markdown docs/reference/api.md \
  --summary-baseline docs/reference/summary-baseline.json
```

Use `--verify --check-summaries` in CI. `--self-test` exercises generic parameters,
bindable callbacks, child content, cascading parameters, and fixture reflection without
modifying the generated files.

The generated JSON must be copied to
`fluentkit-api/references/api.json`; CI compares the two copies byte-for-byte.

When an API change is intentional, update `PublicAPI.Unshipped.txt` during development,
review the compatibility impact, and promote the reviewed entries to
`PublicAPI.Shipped.txt` for the tagged release. CI promotes RS0016/RS0017 to errors so an
unreviewed public API change cannot silently ship.
