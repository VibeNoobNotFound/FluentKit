# FluentKit maintenance scripts

Regenerate the canonical API reference after changing the library API:

```bash
./scripts/update-api-reference.sh
```

On Windows, run:

```bat
scripts\update-api-reference.bat
```

Pass `Debug` or `Release` as the first argument to select the build configuration. Add
`--verify` as the second argument to check freshness without writing generated files. The script
builds `FluentKit.dll`, reads its public API and XML summaries, and regenerates or verifies
`docs/reference`. The NuGet pack target independently stages the exact same contract from the
just-built assembly into the package.

The script does not install skills, edit package versions, create Git tags, commit files, or push
anything. Use `scripts/install-fluentkit-api-skill.*` once to install the standalone bootstrap.
CI and release jobs run `test-fluentkit-contract.py` against the local package to verify both
successful resolution and expected failures for missing, incomplete, stale, and tampered contracts.
