"""Configure an ephemeral CI keychain. Secrets never become command text or error output."""
import base64
import os
from pathlib import Path
import secrets
import subprocess


def secret_run(*arguments):
    result = subprocess.run(arguments, capture_output=True)
    if result.returncode:
        raise SystemExit(f"Apple signing setup failed ({arguments[0]}, exit {result.returncode}). Check the signing credentials.")


certificate = os.environ.get("CERTIFICATE")
if not certificate:
    print("Apple credentials absent: an ad-hoc signed DMG will be built for review; Gatekeeper validation requires Developer ID signing and notarization.")
else:
    identity = os.environ.get("IDENTITY")
    if not identity:
        raise SystemExit("MACOS_SIGN_IDENTITY is required with MACOS_CERTIFICATE_P12.")
    temporary = Path(os.environ["RUNNER_TEMP"])
    keychain = temporary / "miniinfos-signing.keychain-db"
    p12 = temporary / "miniinfos-certificate.p12"
    password = secrets.token_urlsafe(32)
    p12.write_bytes(base64.b64decode(certificate, validate=True))
    p12.chmod(0o600)
    try:
        secret_run("security", "create-keychain", "-p", password, str(keychain))
        secret_run("security", "set-keychain-settings", "-lut", "21600", str(keychain))
        secret_run("security", "unlock-keychain", "-p", password, str(keychain))
        secret_run("security", "import", str(p12), "-k", str(keychain), "-P", os.environ.get("CERT_PASSWORD", ""), "-T", "/usr/bin/codesign")
        secret_run("security", "set-key-partition-list", "-S", "apple-tool:,apple:", "-s", "-k", password, str(keychain))
        secret_run("security", "list-keychains", "-d", "user", "-s", str(keychain))
        with open(os.environ["GITHUB_ENV"], "a", encoding="utf-8") as output:
            if "\n" in identity or "\r" in identity:
                raise SystemExit("Invalid signing identity.")
            output.write("MINIINFOS_SIGN_IDENTITY=" + identity + "\n")
            if all(os.environ.get(key) for key in ("APPLE_ID", "APP_PASSWORD", "TEAM")):
                secret_run("xcrun", "notarytool", "store-credentials", "miniinfos", "--apple-id", os.environ["APPLE_ID"],
                    "--password", os.environ["APP_PASSWORD"], "--team-id", os.environ["TEAM"], "--keychain", str(keychain))
                # The ephemeral keychain is in the user's search list; no credentials enter a file in the repo.
                output.write("MINIINFOS_NOTARY_PROFILE=miniinfos\n")
                output.write("MINIINFOS_NOTARY_KEYCHAIN=" + str(keychain) + "\n")
    finally:
        p12.unlink(missing_ok=True)
