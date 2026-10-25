#!/bin/sh
# BUA 风格入口（兼容保留）。
# 真正的安装/更新逻辑只有一份：仓库根目录的 install.sh。
# 推荐用法：bash /userdata/system/movies/install.sh
HERE=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)

ROOT=$(CDPATH= cd -- "$HERE/../.." 2>/dev/null && pwd) || ROOT=""
if [ -n "$ROOT" ] && [ -f "$ROOT/install.sh" ]; then
    exec bash "$ROOT/install.sh" "$@"
fi
if [ -f "$HERE/install.sh" ]; then
    exec bash "$HERE/install.sh" "$@"
fi

# 不在仓库里：拉取最新的一键安装器
URL="https://raw.githubusercontent.com/anturong/batocera-movie-scraper/main/install.sh"
TMP="${TMPDIR:-/tmp}/moviescraper-install.$$.sh"
for u in "$URL" "https://ghfast.top/$URL" "https://gh-proxy.com/$URL"; do
    if curl -fsSL --connect-timeout 10 --max-time 120 -o "$TMP" "$u" && [ -s "$TMP" ]; then
        bash "$TMP" "$@"
        rc=$?
        rm -f "$TMP"
        exit $rc
    fi
done
rm -f "$TMP"
echo "下载安装器失败，请检查网络或改用：bash /userdata/system/movies/install.sh" >&2
exit 1
