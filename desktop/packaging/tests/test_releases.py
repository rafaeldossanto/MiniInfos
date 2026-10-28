from pathlib import Path
import hashlib
import importlib.util
import tempfile
import unittest

source = Path(__file__).resolve().parents[1] / "check_release.py"
spec = importlib.util.spec_from_file_location("release_boundary", source)
release = importlib.util.module_from_spec(spec)
spec.loader.exec_module(release)


class Release(unittest.TestCase):
    def test_complete_release_checks_hashes_and_rejects_catalog_or_missing_platforms(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            for name in release.PACKAGES:
                data = name.encode()
                (root / name).write_bytes(data)
                (root / (name + ".sha256")).write_text(hashlib.sha256(data).hexdigest() + "  " + name + "\n", encoding="utf-8")
            release.check(root)
            (root / "index.json").write_text("{}")
            with self.assertRaises(ValueError):
                release.check(root)
            (root / "index.json").unlink()
            package = root / release.PACKAGES[1]
            package.write_bytes(b"changed after signing")
            with self.assertRaises(ValueError):
                release.check(root)
            package.unlink()
            with self.assertRaises(ValueError):
                release.check(root)
