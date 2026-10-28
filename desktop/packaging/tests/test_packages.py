from pathlib import Path
import importlib.util
import plistlib
import tempfile
import unittest
import zipfile

module = Path(__file__).resolve().parents[1] / "package.py"
spec = importlib.util.spec_from_file_location("packaging_native", module)
pack = importlib.util.module_from_spec(spec)
spec.loader.exec_module(pack)


class Packages(unittest.TestCase):
    def test_publish_boundary_rejects_community_and_front(self):
        with tempfile.TemporaryDirectory() as root:
            root = Path(root)
            for name in ("MiniInfos.Desktop", "MiniInfos.Desktop.dll"):
                (root / name).write_bytes(b"test")
            pack.validate_payload(root)
            for name in ("marketplace/package.json", "front/index.html", "tools/tool.dll", "pet.miniinfos"):
                path = root / name
                path.parent.mkdir(exist_ok=True)
                path.write_bytes(b"not application code")
                with self.assertRaises(ValueError):
                    pack.validate_payload(root)
                path.unlink()

    def test_mac_bundle_identity_version_and_unix_permissions_survive_windows_zip(self):
        with tempfile.TemporaryDirectory() as root:
            root = Path(root)
            payload = root / "publish"
            payload.mkdir()
            (payload / "MiniInfos.Desktop").write_bytes(b"test")
            (payload / "MiniInfos.Desktop.dll").write_bytes(b"test")
            bundle = pack.mac_bundle(payload, root / "image")
            info = plistlib.loads((bundle / "Contents/Info.plist").read_bytes())
            self.assertEqual("MiniInfos.Desktop", info["CFBundleExecutable"])
            self.assertEqual(pack.version(), info["CFBundleVersion"])
            archive = root / "app.zip"
            pack.zip_bundle(bundle, archive)
            with zipfile.ZipFile(archive) as file:
                main = file.getinfo("MiniInfos.app/Contents/MacOS/MiniInfos.Desktop")
                self.assertEqual(0o755, (main.external_attr >> 16) & 0o777)
                names = file.namelist()
                self.assertTrue(any("claude-plugin/mod/hooks/register.ts" in name for name in names))
                self.assertFalse(any("marketplace/packages" in name or "front/" in name or "/tests/" in name for name in names))


if __name__ == "__main__":
    unittest.main()
