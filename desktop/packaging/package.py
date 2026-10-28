"""Build self-contained native packages. Cross builds produce unsigned .app.zip previews on Windows.
Only dotnet publish, the Claude mod, licenses and explicitly named packaging files enter a package.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import plistlib
import shutil
import subprocess
import sys
import tarfile
import tempfile
import xml.etree.ElementTree as ET
import zipfile
import struct

ROOT = Path(__file__).resolve().parents[2]
DESKTOP = ROOT / "desktop"
RUNTIMES = ("linux-x64", "linux-arm64", "osx-x64", "osx-arm64")


def run(*command: str) -> None:
    subprocess.run(command, cwd=ROOT, check=True)


def version() -> str:
    return ET.parse(ROOT / "widget/MiniInfos.csproj").findtext("PropertyGroup/Version") or "0.0.0"


def validate_payload(folder: Path) -> None:
    forbidden = {"marketplace", "community", "front", "tools", "node_modules", ".git"}
    for path in folder.rglob("*"):
        if forbidden.intersection(path.relative_to(folder).parts) or path.suffix in {".miniinfos", ".pdb"}:
            raise ValueError(f"Non-application file in package: {path}")
        if path.is_symlink():
            raise ValueError(f"Unexpected link in published payload: {path}")
    if not (folder / "MiniInfos.Desktop").is_file():
        raise ValueError("The published native executable is missing.")
    if not (folder / "MiniInfos.Desktop.dll").is_file():
        raise ValueError("The published managed application is missing.")


def validate_architecture(folder: Path, runtime: str) -> None:
    with (folder / "MiniInfos.Desktop").open("rb") as executable:
        header = executable.read(32)
    arm = runtime.endswith("arm64")
    if runtime.startswith("linux"):
        valid = header[:6] == b"\x7fELF\x02\x01" and struct.unpack_from("<H", header, 18)[0] == (183 if arm else 62)
    else:
        valid = header[:4] == b"\xcf\xfa\xed\xfe" and struct.unpack_from("<I", header, 4)[0] == (0x100000C if arm else 0x1000007)
    if not valid:
        raise ValueError(f"The app host is not a native {runtime} executable.")
    if any((folder / name).exists() for name in ("PresentationFramework.dll", "System.Windows.Forms.dll")):
        raise ValueError("Windows UI assemblies cannot ship in a native Unix package.")


def add_mod(destination: Path) -> None:
    shutil.copytree(ROOT / "mod", destination / "mod", ignore=shutil.ignore_patterns("tests", "node_modules", "bin", "obj"))
    source = ROOT / ".claude-plugin/marketplace.json"
    (destination / ".claude-plugin").mkdir(parents=True)
    shutil.copy2(source, destination / ".claude-plugin/marketplace.json")


def executable(path: Path) -> None:
    path.chmod(0o755)


def mac_bundle(payload: Path, stage: Path) -> Path:
    bundle = stage / "MiniInfos.app"
    contents = bundle / "Contents"
    shutil.copytree(payload, contents / "MacOS")
    resources = contents / "Resources"
    resources.mkdir()
    add_mod(resources / "claude-plugin")
    icon = ROOT / "desktop/packaging/MiniInfos.icns"
    if icon.exists():
        shutil.copy2(icon, resources / icon.name)
    info = {
        "CFBundleExecutable": "MiniInfos.Desktop", "CFBundleIdentifier": "com.miniinfos.desktop",
        "CFBundleName": "MiniInfos", "CFBundleDisplayName": "MiniInfos", "CFBundlePackageType": "APPL",
        "CFBundleVersion": version(), "CFBundleShortVersionString": version(),
        "CFBundleIconFile": "MiniInfos.icns", "LSMinimumSystemVersion": "14.0",
        "NSHighResolutionCapable": True, "NSPrincipalClass": "NSApplication",
    }
    with (contents / "Info.plist").open("wb") as file:
        plistlib.dump(info, file)
    executable(contents / "MacOS/MiniInfos.Desktop")
    return bundle


def zip_bundle(bundle: Path, output: Path) -> None:
    with zipfile.ZipFile(output, "w", zipfile.ZIP_DEFLATED) as archive:
        for path in sorted(bundle.rglob("*")):
            if not path.is_file():
                continue
            entry = zipfile.ZipInfo(path.relative_to(bundle.parent).as_posix())
            entry.create_system = 3
            entry.external_attr = (0o100755 if path.name == "MiniInfos.Desktop" else 0o100644) << 16
            archive.writestr(entry, path.read_bytes(), compress_type=zipfile.ZIP_DEFLATED)


def sign_mac(bundle: Path, identity: str) -> None:
    # Sign inner Mach-O code first, then the app host with the JIT entitlement .NET needs.
    magic = {b"\xcf\xfa\xed\xfe", b"\xce\xfa\xed\xfe", b"\xca\xfe\xba\xbe", b"\xbe\xba\xfe\xca"}
    main = bundle / "Contents/MacOS/MiniInfos.Desktop"
    flags = ["--timestamp", "--options", "runtime"] if identity != "-" else []
    for path in sorted((bundle / "Contents/MacOS").rglob("*")):
        if path.is_file() and path != main:
            with path.open("rb") as source:
                if source.read(4) in magic:
                    run("codesign", "--force", "--sign", identity, *flags, str(path))
    run("codesign", "--force", "--sign", identity, *flags, "--entitlements", str(DESKTOP / "packaging/entitlements.plist"), str(bundle))
    run("codesign", "--verify", "--deep", "--strict", str(bundle))


def package(runtime: str, output: Path) -> list[Path]:
    output.mkdir(parents=True, exist_ok=True)
    work = DESKTOP / "out"
    work.mkdir(exist_ok=True)
    results: list[Path] = []
    with tempfile.TemporaryDirectory(prefix=runtime + "-", dir=work) as temporary:
        stage = Path(temporary)
        payload = stage / "publish"
        run("dotnet", "publish", "desktop/MiniInfos.Desktop.csproj", "-c", "Release", "-r", runtime,
            "--self-contained", "true", "-p:PublishTrimmed=false", "-p:DebugType=none", "-p:DebugSymbols=false", "-o", str(payload))
        shutil.copytree(DESKTOP / "packaging/licenses", payload / "Licenses", dirs_exist_ok=True)
        validate_payload(payload)
        validate_architecture(payload, runtime)
        executable(payload / "MiniInfos.Desktop")
        arch = runtime.split("-")[1]
        if runtime.startswith("linux"):
            portable = stage / "MiniInfos"
            shutil.copytree(payload, portable / "app")
            add_mod(portable / "claude-plugin")
            for name in ("miniinfos", "install.sh", "README.txt"):
                shutil.copy2(DESKTOP / "packaging/linux" / name, portable / name)
            for name in ("miniinfos", "install.sh"):
                executable(portable / name)
            icon = DESKTOP / "packaging/MiniInfos.png"
            shutil.copy2(icon, portable / "MiniInfos.png")
            archive = output / f"MiniInfos-linux-{arch}.tar.gz"
            with tarfile.open(archive, "w:gz") as tar:
                def permissions(info: tarfile.TarInfo) -> tarfile.TarInfo:
                    info.uid = info.gid = 0
                    info.uname = info.gname = "root"
                    info.mode = 0o755 if info.isdir() or Path(info.name).name in {"MiniInfos.Desktop", "miniinfos", "install.sh"} else 0o644
                    return info
                tar.add(portable, arcname="MiniInfos", filter=permissions)
            results.append(archive)
        else:
            bundle = mac_bundle(payload, stage / "image")
            if sys.platform == "darwin":
                identity = os.environ.get("MINIINFOS_SIGN_IDENTITY", "-")
                sign_mac(bundle, identity)
                # The signed .app ZIP is the notarization upload. A cross-built ZIP is a preview only.
                archive = output / f"MiniInfos-macos-{arch}.app.zip"
                run("ditto", "-c", "-k", "--keepParent", str(bundle), str(archive))
                if os.environ.get("MINIINFOS_NOTARY_PROFILE"):
                    run("xcrun", "notarytool", "submit", str(archive), "--keychain-profile", os.environ["MINIINFOS_NOTARY_PROFILE"], "--keychain", os.environ["MINIINFOS_NOTARY_KEYCHAIN"], "--wait")
                    run("xcrun", "stapler", "staple", str(bundle))
                    run("spctl", "--assess", "--type", "execute", "--verbose", str(bundle))
                (bundle.parent / "Applications").symlink_to("/Applications", target_is_directory=True)
                dmg = output / f"MiniInfos-macos-{arch}.dmg"
                run("hdiutil", "create", "-volname", "MiniInfos", "-srcfolder", str(bundle.parent), "-ov", "-format", "UDZO", str(dmg))
                if identity != "-":
                    run("codesign", "--force", "--sign", identity, "--timestamp", str(dmg))
                if os.environ.get("MINIINFOS_NOTARY_PROFILE"):
                    run("xcrun", "notarytool", "submit", str(dmg), "--keychain-profile", os.environ["MINIINFOS_NOTARY_PROFILE"], "--keychain", os.environ["MINIINFOS_NOTARY_KEYCHAIN"], "--wait")
                    run("xcrun", "stapler", "staple", str(dmg))
                results.append(dmg)
            else:
                archive = output / f"MiniInfos-macos-{arch}-unsigned.app.zip"
                zip_bundle(bundle, archive)
                results.append(archive)
        for result in results:
            with result.open("rb") as stream:
                digest = hashlib.file_digest(stream, "sha256").hexdigest()
            result.with_name(result.name + ".sha256").write_text(f"{digest}  {result.name}\n", encoding="utf-8")
            print(json.dumps({"file": str(result), "bytes": result.stat().st_size, "sha256": digest}))
    return results


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--runtime", choices=RUNTIMES, required=True)
    parser.add_argument("--output", type=Path, default=DESKTOP / "dist")
    arguments = parser.parse_args()
    package(arguments.runtime, arguments.output.resolve())
