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
  --summary-baseline docs/reference/summary-baseline.json \
  --check-summaries
```

Use `--verify --check-summaries` in CI. `--self-test` exercises generic parameters,
bindable callbacks, child content, cascading parameters, and fixture reflection without
modifying the generated files. `scripts/test-fluentkit-contract.py` exercises resolver and
package-verifier failure scenarios against a freshly packed local package.

Use `--contract-output` with `--package-id`, `--package-version`, `--skill-source`, and
`--references-source` to stage the package-local agent contract. Use `--verify-package PATH`
to inspect a completed `.nupkg`, including its manifest, generated API, hashes, and
`buildTransitive` discovery props. `dotnet pack` invokes this staging flow automatically.

When an API change is intentional, update `PublicAPI.Unshipped.txt` during development,
review the compatibility impact, and promote the reviewed entries to
`PublicAPI.Shipped.txt` for the tagged release. CI promotes RS0016/RS0017 to errors so an
unreviewed public API change cannot silently ship.
