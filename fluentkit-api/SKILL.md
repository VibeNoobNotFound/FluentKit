---
name: fluentkit-api
description: Use the exact FluentKit.Blazor API and integration guidance shipped by the package referenced by the current Blazor or MAUI Blazor application.
---

# FluentKit package contract bootstrap

This is a permanent bootstrap skill. It is installed once and resolves the version-specific
agent contract from the `FluentKit.Blazor` package restored by the consumer project. It must
never use a release ZIP, online "latest" API snapshot, or a different package version.

## Resolve the exact package contract

1. If the user names a project, use that project. Otherwise inspect the current directory for the nearest `.csproj`, `.sln`, or `.slnx` containing a `FluentKit.Blazor` reference. If more than one valid project remains, ask which project is in scope.
2. Restore the selected project. If its directory contains `packages.lock.json`, use locked
   restore so the resolved dependency graph cannot change silently.
3. Run the resolver script shipped beside this `SKILL.md`, matching the host operating system.
   Substitute the installed skill directory for `<skill-dir>`:

   ```bash
   bash <skill-dir>/scripts/resolve-fluentkit.sh /absolute/path/to/Consumer.csproj
   ```

   ```powershell
   <skill-dir>\scripts\resolve-fluentkit.ps1 -Project C:\path\to\Consumer.csproj
   ```

4. If the project requires MSBuild properties or a private/local NuGet source to evaluate its
   package references, forward them with `--property Name=Value` / `-Property Name=Value` or
   `--source PATH` / `-Source PATH`.

5. The resolver queries `FluentKitAgentManifestPath` and `FluentKitAgentSkillPath` through
   `dotnet msbuild`. Read the returned package-local `SKILL.md` completely before acting.
6. If either property is absent, explain that the package predates the agent contract and ask
   the user to upgrade to the first contract-bearing FluentKit release. Do not fall back to
   reflection or a downloaded historical bundle.

The package-local contract contains the exact `api.json`, `api.md`, and integration references.
Follow those instructions for the remainder of the task.

## Safety and scope

- Never write into the user's skill directory during package restore or application builds.
- Do not assume the package's latest published version is the version used by the app.
- Keep the fixed static asset base `_content/FluentKit`, not `_content/FluentKit.Blazor`.
