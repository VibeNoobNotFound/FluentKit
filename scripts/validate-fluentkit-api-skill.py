#!/usr/bin/env python3
"""Validate the standalone bootstrap and the package-contract source layout."""

from pathlib import Path
import re
import sys


def fail(message: str) -> None:
    print(f"skill validation failed: {message}", file=sys.stderr)
    raise SystemExit(1)


def require_file(path: Path) -> None:
    if not path.is_file():
        fail(f"missing file: {path}")


def validate_frontmatter(path: Path, expected_name: str) -> None:
    text = path.read_text(encoding="utf-8-sig")
    if not text.startswith("---\n") or "\n---\n" not in text[4:]:
        fail(f"{path} has no valid YAML frontmatter")
    frontmatter = text[4:text.index("\n---\n", 4)]
    name = re.search(r"^name:\s*([^\s]+)\s*$", frontmatter, re.MULTILINE)
    description = re.search(r"^description:\s*(\S.*)$", frontmatter, re.MULTILINE)
    if not name or name.group(1) != expected_name:
        fail(f"{path} must declare name: {expected_name}")
    if not description or len(description.group(1)) < 20:
        fail(f"{path} needs a useful description")


def main() -> None:
    root = Path(__file__).resolve().parent.parent
    bootstrap = root / "fluentkit-api"
    contract_skill = root / "docs" / "integration" / "agent-skill.md"

    bootstrap_skill = bootstrap / "SKILL.md"
    require_file(bootstrap_skill)
    validate_frontmatter(bootstrap_skill, "fluentkit-api")
    bootstrap_text = bootstrap_skill.read_text(encoding="utf-8-sig")
    for required in (
        "FluentKitAgentSkillPath",
        "FluentKitAgentManifestPath",
        "dotnet msbuild",
        "more than one valid project",
        "locked",
        "predates the agent contract",
        "Do not fall back",
    ):
        if required not in bootstrap_text:
            fail(f"bootstrap does not describe {required}")
    for script in (bootstrap / "scripts" / "resolve-fluentkit.sh", bootstrap / "scripts" / "resolve-fluentkit.ps1"):
        require_file(script)
    require_file(bootstrap / "agents" / "openai.yaml")
    if (bootstrap / "metadata.json").exists() or (bootstrap / "references" / "api.json").exists():
        fail("legacy versioned metadata/API snapshot still exists")

    require_file(contract_skill)
    validate_frontmatter(contract_skill, "fluentkit-api")
    for reference in ("README.md", "setup.md", "component-selection.md", "theming-and-tokens.md", "overlays.md", "icons.md", "troubleshooting.md", "sample-routes.md"):
        require_file(root / "docs" / "integration" / reference)

    print("FluentKit bootstrap and contract source are valid.")


if __name__ == "__main__":
    main()
