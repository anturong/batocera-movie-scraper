#!/bin/sh
# BUA 风格卸载入口（兼容保留）：调用仓库根目录 install.sh --uninstall
HERE=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)

ROOT=$(CDPATH= cd -- "$HERE/../.." 2>/dev/null && pwd) || ROOT=""
if [ -n "$ROOT" ] && [ -f "$ROOT/install.sh" ]; then
    exec bash "$ROOT/install.sh" --uninstall "$@"
fi
if [ -f "$HERE/install.sh" ]; then
    exec bash "$HERE/install.sh" --uninstall "$@"
fi

exec sh "$HERE/moviescraper.sh" --uninstall "$@"
