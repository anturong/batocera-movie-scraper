#!/bin/bash
# 电影刮削器 · 卸载（复用项目自带 uninstall.sh）
set -u
HERE="$(cd "$(dirname "$0")" && pwd)"
[ "$(id -u)" = 0 ] || { echo "需要 root 权限运行。"; exit 1; }
if [ -d "$HERE/deploy" ]; then
    sh "$HERE/deploy/uninstall.sh" "$@"
else
    echo "找不到 deploy/，改为手动清理："
    echo "  rm -rf /userdata/system/movies"
fi
MENU=/userdata/roms/ports/bua.sh
if [ -f "$MENU" ] && grep -q "MOVIESCRAPER" "$MENU"; then
    cp -f "$MENU" "$MENU.bak-uninstall"
    python3 - "$MENU" <<'PYEOF'
import io, sys, re
p = sys.argv[1]
s = io.open(p, encoding="utf-8", errors="replace").read()
s = "\n".join(l for l in s.splitlines() if "MOVIESCRAPER" not in l) + "\n"
io.open(p, "w", encoding="utf-8", newline="\n").write(s)
print("   已从 BUA 菜单移除")
PYEOF
    curl -fs http://127.0.0.1:1234/reloadgames >/dev/null 2>&1 || true
fi
