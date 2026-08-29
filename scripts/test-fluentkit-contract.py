#!/usr/bin/env python3
"""Exercise the package contract's success and failure boundaries in a clean temp area."""

from __future__ import annotations

import argparse
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import zipfile


def run(command: list[str], cwd: Path, expect_success: bool = True, env: dict[str, str] | None = None) -> subprocess.CompletedProcess[str]:
    result = subprocess.run(command, cwd=cwd, env=env, text=True, capture_output=True)
    if expect_success and result.returncode != 0:
        raise RuntimeError(f"command failed ({result.returncode}): {' '.join(command)}\n{result.stdout}\n{result.stderr}")
    if not expect_success and result.returncode == 0:
        raise RuntimeError(f"command unexpectedly succeeded: {' '.join(command)}\n{result.stdout}\n{result.stderr}")
    return result


def resolver_command(resolver: Path, project: Path, version: str | None = None, source: Path | None = None) -> list[str]:
    if resolver.suffix.lower() == ".ps1":
        command = ["pwsh", "-NoProfile", "-File", str(resolver), "-Project", str(project)]
        if version:
            command += ["-Property", f"FluentKitSmokeVersion={version}"]
        if source:
            command += ["-Source", str(source)]
        return command
    command = ["bash", str(resolver), str(project)]
    if version:
        command += ["--property", f"FluentKitSmokeVersion={version}"]
    if source:
        command += ["--source", str(source)]
    return command


def write_project(directory: Path, package_reference: bool = False) -> Path:
    project = directory / "Contract Fixture.csproj"
    package = (
        '  <ItemGroup>\n'
        '    <PackageReference Include="FluentKit.Blazor" Version="$(FluentKitSmokeVersion)" />\n'
        '  </ItemGroup>\n'
        if package_reference else ""
    )
    project.write_text(
        '<Project Sdk="Microsoft.NET.Sdk">\n'
        '  <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>\n'
        f"{package}"
        '</Project>\n',
        encoding="utf-8",
    )
    return project


def rewrite_package(source: Path, destination: Path, replacements: dict[str, bytes], omit: set[str] | None = None) -> None:
    omit = omit or set()
    with zipfile.ZipFile(source) as input_zip, zipfile.ZipFile(destination, "w", zipfile.ZIP_DEFLATED) as output_zip:
        for entry in input_zip.infolist():
            if entry.filename in omit:
                continue
            output_zip.writestr(entry, replacements.get(entry.filename, input_zip.read(entry.filename)))


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", type=Path, required=True)
    parser.add_argument("--package", type=Path, required=True)
    parser.add_argument("--version", required=True)
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--smoke-project", type=Path, required=True)
    parser.add_argument("--resolver", type=Path, required=True)
    parser.add_argument("--generator", type=Path, required=True)
    parser.add_argument("--assembly", type=Path, required=True)
    parser.add_argument("--xml", type=Path, required=True)
    parser.add_argument("--manifest", type=Path, required=True)
    args = parser.parse_args()

    root = args.root.resolve()
    resolver = args.resolver.resolve()
    package = args.package.resolve()
    source = args.source.resolve()
    smoke_project = args.smoke_project.resolve()
    generator = args.generator.resolve()
    assembly = args.assembly.resolve()
    xml = args.xml.resolve()
    manifest = args.manifest.resolve()
    base_env = os.environ.copy()

    with tempfile.TemporaryDirectory(prefix="fluentkit contract ") as temporary:
        temp = Path(temporary)

        # No package reference (including a package from before the contract release) is a
        # supported resolver failure, not an opportunity to use a fallback API snapshot.
        no_package_project = write_project(temp / "no-package") if (temp / "no-package").mkdir() is None else None
        result = run(resolver_command(resolver, no_package_project), root, expect_success=False, env=base_env)
        if result.returncode != 3:
            raise RuntimeError(f"missing contract should return 3, got {result.returncode}: {result.stderr}")

        # Properties can exist while the package payload is incomplete; this must be reported
        # separately so a consumer never receives a false successful resolution.
        missing_contract = temp / "missing contract"
        missing_contract.mkdir()
        missing_props = missing_contract / "Directory.Build.props"
        missing_props.write_text(
            '<Project><PropertyGroup>\n'
            '  <FluentKitAgentSkillPath>missing/SKILL.md</FluentKitAgentSkillPath>\n'
            '  <FluentKitAgentManifestPath>missing/manifest.json</FluentKitAgentManifestPath>\n'
            '</PropertyGroup></Project>\n',
            encoding="utf-8",
        )
        missing_project = write_project(missing_contract)
        result = run(resolver_command(resolver, missing_project), root, expect_success=False, env=base_env)
        if result.returncode != 4:
            raise RuntimeError(f"incomplete contract should return 4, got {result.returncode}: {result.stderr}")

        # Restore the real smoke project from scratch in a directory containing spaces. This also
        # proves the resolver performs the initial restore rather than depending on assets files.
        spaced_directory = temp / "consumer with spaces"
        spaced_directory.mkdir()
        spaced_project = spaced_directory / smoke_project.name
        shutil.copy2(smoke_project, spaced_project)
        run(resolver_command(resolver, spaced_project, args.version, source), root, env=base_env)

        # Generate a lock file, then invalidate it. The resolver must use locked mode and fail
        # rather than silently changing the dependency graph.
        locked_directory = temp / "locked consumer"
        locked_directory.mkdir()
        locked_project = locked_directory / smoke_project.name
        shutil.copy2(smoke_project, locked_project)
        locked_env = base_env.copy()
        run(
            ["dotnet", "restore", str(locked_project), "--use-lock-file", "--source", str(source), "-p:FluentKitSmokeVersion=" + args.version],
            root,
            env=locked_env,
        )
        lock_path = locked_directory / "packages.lock.json"
        lock = json.loads(lock_path.read_text(encoding="utf-8"))
        dependency = lock["dependencies"]["net10.0"]["FluentKit.Blazor"]
        dependency["requested"] = "99.99.99"
        lock_path.write_text(json.dumps(lock, indent=2) + "\n", encoding="utf-8")
        run(resolver_command(resolver, locked_project, args.version, source), root, expect_success=False, env=locked_env)

        generator_command = [
            "dotnet", "run", "--project", str(generator), "-c", "Release", "--no-build", "--no-restore", "--",
            "--verify-package", str(package), "--assembly", str(assembly), "--xml", str(xml), "--manifest", str(manifest),
        ]
        run(generator_command, root, env=base_env)

        # A package with a missing contract file must fail before any API is consumed.
        missing_package = temp / "missing-contract.nupkg"
        rewrite_package(package, missing_package, {}, {"fluentkit/agent/v1/SKILL.md"})
        missing_command = generator_command.copy()
        missing_command[missing_command.index(str(package))] = str(missing_package)
        run(missing_command, root, expect_success=False, env=base_env)

        with zipfile.ZipFile(package) as archive:
            package_manifest = json.loads(archive.read("fluentkit/agent/v1/manifest.json"))
            package_api = json.loads(archive.read("fluentkit/agent/v1/api.json"))

        package_manifest["packageVersion"] = "99.99.99"
        mismatched_manifest = temp / "mismatched-manifest.nupkg"
        rewrite_package(
            package,
            mismatched_manifest,
            {"fluentkit/agent/v1/manifest.json": (json.dumps(package_manifest, indent=2) + "\n").encode()},
        )
        mismatch_command = generator_command.copy()
        mismatch_command[mismatch_command.index(str(package))] = str(mismatched_manifest)
        run(mismatch_command, root, expect_success=False, env=base_env)

        package_api["version"] = "99.99.99"
        mismatched_api = temp / "mismatched-api.nupkg"
        rewrite_package(
            package,
            mismatched_api,
            {"fluentkit/agent/v1/api.json": (json.dumps(package_api, indent=2) + "\n").encode()},
        )
        mismatch_command = generator_command.copy()
        mismatch_command[mismatch_command.index(str(package))] = str(mismatched_api)
        run(mismatch_command, root, expect_success=False, env=base_env)

    print("FluentKit package contract success and failure scenarios passed.")


if __name__ == "__main__":
    try:
        main()
    except (OSError, RuntimeError, KeyError, json.JSONDecodeError) as error:
        print(f"contract scenario test failed: {error}", file=sys.stderr)
        raise SystemExit(1)
