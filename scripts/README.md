# FluentKit maintenance scripts

Regenerate the API reference and synchronize the skill bundle after changing the
library API:

```bash
./scripts/update-api-reference.sh
```

On Windows, run:

```bat
scripts\update-api-reference.bat
```

Pass `Debug` or `Release` as the first argument to select the build configuration. The script
builds `FluentKit.dll`, reads its public API and XML summaries, regenerates `docs/reference`,
copies `api.json` into `fluentkit-api/references`, checks the summary baseline, and verifies
that the package and skill versions match.

The script does not edit `metadata.json`, create Git tags, commit files, or push anything. Update
the skill version intentionally, run the script, review the diff, commit, and then tag the release.
