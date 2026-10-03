#!/usr/bin/env python3

from pathlib import Path
import re
import sys


README_NAMES = {
    "readme",
    "readme.txt",
    "readme.md",
}

URL_PATTERN = re.compile(r"https?://[^\s<>\[\]()]+")


def format_links(text):
    """Wrap plain HTTP(S) URLs in RichTextLabel [url] tags."""
    return URL_PATTERN.sub(
        lambda match: f"[url]{match.group(0)}[/url]",
        text,
    )


def main():
    if len(sys.argv) != 2:
        print(f"Usage: {sys.argv[0]} <project-directory>", file=sys.stderr)
        return 1

    project_dir = Path(sys.argv[1])
    assets_dir = project_dir / "Assets"
    output_file = project_dir / "Text" / "asset_credits.txt"
    notice_file = assets_dir / "NOTICE.txt"

    if not assets_dir.is_dir():
        raise RuntimeError(f"Assets directory not found: {assets_dir}")

    if not notice_file.is_file():
        raise RuntimeError(f"NOTICE.txt not found: {notice_file}")

    # Find README files below the Assets root, grouped by their
    # containing directory. The root README is intentionally excluded.
    readmes_by_directory = {}

    for path in assets_dir.rglob("*"):
        if not path.is_file():
            continue

        if path.parent == assets_dir:
            continue

        if path.name.lower() not in README_NAMES:
            continue

        relative_directory = path.parent.relative_to(assets_dir)
        readmes_by_directory.setdefault(relative_directory, []).append(path)

    lines = [
        "[font_size=24][b]Asset Credits[/b][/font_size]",
        "",
        format_links(notice_file.read_text(encoding="utf-8").rstrip()),
        "",
    ]

    # Only directories containing README files get a heading.
    for directory in sorted(readmes_by_directory):
        lines.extend([
            f"[font_size=16][b]{directory}[/b][/font_size]",
            "",
        ])

        for readme in sorted(readmes_by_directory[directory]):
            lines.extend([
                format_links(readme.read_text(encoding="utf-8").rstrip()),
                "",
            ])

    output_file.write_text(
        "\n".join(lines).rstrip() + "\n",
        encoding="utf-8",
    )

    readme_count = sum(
        len(readmes) for readmes in readmes_by_directory.values()
    )

    print(f"Generated {output_file}")
    print(f"Included {readme_count} README files")

    return 0


if __name__ == "__main__":
    sys.exit(main())
