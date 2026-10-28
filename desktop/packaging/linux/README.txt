MiniInfos for Linux (x64 / ARM64, glibc)

Extract this archive and open ./miniinfos, or run ./install.sh for a user-local installation.
Extraia este arquivo e abra ./miniinfos, ou execute ./install.sh para instalar apenas para seu usuário.

No .NET installation needed. / Não é necessário instalar o .NET.
Dependencies / Dependências: libx11-6 libice6 libsm6 libfontconfig1 + .NET 10 native runtime dependencies (ICU, OpenSSL, zlib, glibc).
Ubuntu/Debian: sudo apt install libx11-6 libice6 libsm6 libfontconfig1 libicu-dev libssl3 zlib1g
X11 or XWayland required. / É necessário X11 ou XWayland.

Data / Dados: ${XDG_DATA_HOME:-~/.local/share}/MiniInfos
Sources / Históricos: ~/.claude, ~/.codex, ~/.gemini, ~/.kimi-code; custom environment homes are discovered automatically.
Claude plan limits require the bundled mod; tokens are read from local transcripts without it.
Os limites do plano Claude precisam do mod incluído; os tokens são lidos dos históricos locais sem ele.

In Claude Code / No Claude Code:
/plugin marketplace add /absolute/path/to/MiniInfos/claude-plugin
/plugin install miniinfos@miniinfos

Source and support / Código e suporte: https://github.com/rafaeldossanto/MiniInfos
