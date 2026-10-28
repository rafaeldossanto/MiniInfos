#!/bin/sh
# User-local install. The token database and preferences are outside this application folder.
set -eu
source=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
data=${XDG_DATA_HOME:-"$HOME/.local/share"}
case "$data" in /*) ;; *) printf '%s\n' 'XDG_DATA_HOME must be an absolute path.' >&2; exit 1 ;; esac
destination="$data/miniinfos-application"
mkdir -p "$destination" "$data/applications"
cp -R "$source/app" "$source/claude-plugin" "$destination/"
cp "$source/miniinfos" "$source/MiniInfos.png" "$destination/"
chmod +x "$destination/miniinfos" "$destination/app/MiniInfos.Desktop"
escaped=$(printf '%s' "$destination" | sed 's/\\/\\\\/g; s/"/\\"/g; s/`/\\`/g; s/\$/\\$/g; s/%/%%/g')
cat > "$data/applications/com.miniinfos.desktop" <<EOF
[Desktop Entry]
Type=Application
Name=MiniInfos
Comment=Local AI token usage
Exec="$escaped/miniinfos"
Icon=$destination/MiniInfos.png
Terminal=false
Categories=Utility;Development;
StartupWMClass=MiniInfos.Desktop
EOF
printf 'MiniInfos: %s\n' "$destination"
printf '%s\n' 'Open MiniInfos from the applications menu. / Abra MiniInfos no menu de aplicativos.'
