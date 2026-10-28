"""Fail a release before upload if a platform, checksum or payload is missing/unexpected."""
import hashlib
from pathlib import Path
import sys

PACKAGES = ("MiniInfos-Setup.exe", "MiniInfos-macos-arm64.dmg", "MiniInfos-macos-x64.dmg",
            "MiniInfos-linux-arm64.tar.gz", "MiniInfos-linux-x64.tar.gz")


def check(root: Path):
    expected = set(PACKAGES) | {name + ".sha256" for name in PACKAGES}
    actual = {path.name for path in root.iterdir() if path.is_file()}
    if expected != actual:
        raise ValueError(f"Unexpected release assets. Missing: {expected - actual}; extra: {actual - expected}")
    for name in PACKAGES:
        file = root / name
        if file.stat().st_size == 0:
            raise ValueError(f"Empty package: {name}")
        with file.open("rb") as stream:
            digest = hashlib.file_digest(stream, "sha256").hexdigest()
        if (root / (name + ".sha256")).read_text(encoding="utf-8").strip().lower() != f"{digest}  {name}".lower():
            raise ValueError(f"Checksum mismatch: {name}")


if __name__ == "__main__":
    check(Path(sys.argv[1]))
    print("Release: all five native packages and SHA-256 files verified; no catalog assets.")
