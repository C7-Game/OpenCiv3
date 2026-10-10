#!/usr/bin/env python

from pathlib import Path
import re
import sys


README_NAMES = {
    "readme",
    "readme.txt",
    "readme.md",
}

URL_PATTERN = re.compile(r"https?://[^\s<>\[\]()]+")
MARKDOWN_LINK_URL_PATTERN = re.compile(r"\]\((https?://[^)\s]+)\)")


def format_links(text):
    """Wrap plain HTTP(S) URLs in Markdown links."""
    # Don't modify URLs that are already Markdown link destinations.
    markdown_link_ranges = [
        match.span(1) for match in MARKDOWN_LINK_URL_PATTERN.finditer(text)
    ]

    def replace_url(match):
        start, end = match.span()

        if any(
            range_start <= start < range_end
            for range_start, range_end in markdown_link_ranges
        ):
            return match.group(0)

        url = match.group(0)
        return f"[{url}]({url})"

    return URL_PATTERN.sub(replace_url, text)


def main():
    if len(sys.argv) != 2:
        print(f"Usage: {sys.argv[0]} <project-directory>", file=sys.stderr)
        return 1

    project_dir = Path(sys.argv[1])
    assets_dir = project_dir / "Assets"
    output_file = project_dir / "Text" / "asset_credits.md"
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
        "## Asset Credits",
        "",
        format_links(notice_file.read_text(encoding="utf-8").rstrip()),
        "",
    ]

    # Only directories containing README files get a heading.
    for directory in sorted(readmes_by_directory):
        lines.extend([
            f"#### {directory}",
            "",
        ])

        for readme in sorted(readmes_by_directory[directory]):
            lines.extend([
                format_links(readme.read_text(encoding="utf-8").rstrip()),
                "",
            ])

    output_file.write_text(
        "\n".join(lines).rstrip() + "\n\n\n",
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
