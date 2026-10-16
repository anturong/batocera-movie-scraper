#!/bin/bash
###############################################################################
#  电影刮削器 · Batocera 附加组件（BUA 风格，离线安装）
#
#  用法（在 Batocera 上、root）：
#      bash moviescraper.sh                # 全装（含 Kodi/VLC 播放器设置）
#      bash moviescraper.sh --no-players   # 只装刮削器与 EmulationStation「电影」分页
#
#  ★本包不含任何 API key★：payload 里放的是 scraper.example.json（空 key）。
#   安装后到 F1 菜单 →「电影刮削」→ 设置里填自己的 TMDB / 射手网 / OpenSubtitles 凭据；
#   也可以直接把你自己已有的 /userdata/system/movies/scraper.json 放好——install.sh
#   检测到已存在就不会覆盖你的 key。
###############################################################################
set -u

HERE="$(cd "$(dirname "$0")" && pwd)"
BUILD="$HERE/deploy"

echo "=============================================="
echo " 电影刮削器 · 安装"
echo "=============================================="

[ "$(id -u)" = 0 ] || { echo "需要 root 权限运行。"; exit 1; }
[ -d "$BUILD" ] || { echo "找不到 deploy/ 目录：$BUILD"; exit 1; }
command -v python3 >/dev/null || { echo "缺 python3（install.sh 要用它合并 ES 配置）。"; exit 1; }

# 安全自检：包里绝不该出现真配置
for bad in "payload/system/movies/scraper.json" "windows/config.json" "../config.json"; do
    if [ -f "$BUILD/$bad" ]; then
        echo "⚠ 包内发现疑似含 key 的文件：$bad"
        echo "  已中止，避免把你的 key 装到别的机器上。请先删掉它再装。"
        exit 1
    fi
done

# 直接复用项目自带的安装器（拷 binary、写「电影」分页、装 F1 入口、按需设置播放器）
echo "== 调用项目自带 install.sh $* =="
sh "$BUILD/install.sh" "$@" || { echo "install.sh 失败。"; exit 1; }

# 本机 BUA 菜单登记（可选；别人机器上若没装 BUA 会自动跳过）
# BUA 的菜单脚本可能叫 add-ons/bua/menu.sh（新版）或 roms/ports/bua.sh（旧版），
# 找那个真正含 apps=( 的文件来登记。
MENU=""
for c in /userdata/system/add-ons/bua/menu.sh /userdata/roms/ports/bua.sh \
         /userdata/roms/ports/BatoceraUnofficialAddOns.sh; do
    if [ -f "$c" ] && grep -q "apps=(" "$c"; then MENU="$c"; break; fi
done
if [ -n "$MENU" ] && ! grep -q "MOVIESCRAPER" "$MENU"; then
    echo "== 登记到本机 BUA 菜单（$MENU） =="
    cp -f "$MENU" "$MENU.bak-addon"
    python3 - "$MENU" <<'PYEOF'
import io, sys
p = sys.argv[1]
s = io.open(p, encoding="utf-8", errors="replace").read()
line = '  ["MOVIESCRAPER"]="bash __HERE__/moviescraper.sh"\n'
line = line.replace("__HERE__", os.path.dirname(os.path.abspath(sys.argv[0])) if len(sys.argv) > 2 else "")
if '"MOVIESCRAPER"' not in s and 'apps=(' in s:
    s = s.replace('apps=(\n', 'apps=(\n' + line, 1)
    s = s.replace('descriptions=(\n',
                  'descriptions=(\n  ["MOVIESCRAPER"]="电影刮削器（豆瓣/TMDB + 字幕）"\n', 1)
    io.open(p, "w", encoding="utf-8", newline="\n").write(s)
    print("   已写入 BUA 菜单")
PYEOF
    curl -fs http://127.0.0.1:1234/reloadgames >/dev/null 2>&1 || true
fi

echo
echo "安装完成 ✓"
echo "  1) F1 菜单 →「电影刮削」→ 设置：填 TMDB / 射手网 / OpenSubtitles 凭据（本包不含 key）"
echo "  2) EmulationStation 里会多出「电影」分页，把片子放进 /userdata/roms/movies 即可"
echo "  3) 卸载：bash $(basename "$HERE")_uninstall.sh"
