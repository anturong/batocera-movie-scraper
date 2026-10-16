#!/bin/sh
# 电影刮削器 · Batocera 便携安装器
# ---------------------------------------------------------------------------
# 用法（在盘上，root）：
#     把整个 deploy/ 目录拷到盘上任意位置，然后：
#         sh install.sh              # 全装（含 Kodi/VLC 设置）
#         sh install.sh --no-players # 只装刮削器与电影分页，不动 Kodi/VLC
# 幂等：可重复运行。已有 scraper.json（含你的 key）不会被覆盖。
# ---------------------------------------------------------------------------
set -e

HERE=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
PAY="$HERE/payload"
UD=/userdata
MV="$UD/system/movies"
ESDIR="$UD/system/configs/emulationstation"
APPDIR="$UD/system/.local/share/applications"
DO_PLAYERS=1
[ "$1" = "--no-players" ] && DO_PLAYERS=0

[ "$(id -u)" = 0 ] || { echo "需要 root 权限运行。"; exit 1; }
[ -d "$PAY" ] || { echo "找不到 payload 目录：$PAY"; exit 1; }

echo "== 1/5 创建目录 =="
mkdir -p "$MV/lib" "$MV/icons" "$ESDIR" "$APPDIR"

echo "== 2/5 刮削器与脚本 =="
cp -f "$PAY/system/movies/batoscrape"        "$MV/batoscrape"
cp -f "$PAY/system/movies/play.sh"           "$MV/play.sh"
cp -f "$PAY/system/movies/scrape-gui.sh"     "$MV/scrape-gui.sh"
cp -f "$PAY/system/movies/settings.sh"       "$MV/settings.sh"
cp -f "$PAY/system/movies/lib/config_io.py"  "$MV/lib/config_io.py"
[ -f "$PAY/system/movies/icons/scraper.png" ] && cp -f "$PAY/system/movies/icons/scraper.png" "$MV/icons/scraper.png"
chmod +x "$MV/batoscrape" "$MV/play.sh" "$MV/scrape-gui.sh" "$MV/settings.sh"
cp -f "$PAY/system/movies/scraper.example.json" "$MV/scraper.example.json"

echo "== 3/5 配置 =="
if [ -f "$MV/scraper.json" ]; then
  echo "  已存在 scraper.json，保留不动（你的 key 不会丢）。"
elif [ -f "$PAY/system/movies/scraper.json" ]; then
  cp "$PAY/system/movies/scraper.json" "$MV/scraper.json"
  echo "  已写入预填 scraper.json（含 key，开箱即用）。"
else
  cp "$PAY/system/movies/scraper.example.json" "$MV/scraper.json"
  echo "  已生成 scraper.json（空 key，待你在「电影刮削」的设置里填写）。"
fi

echo "== 4/5 EmulationStation「电影」分页 =="
# 迁移：旧版本把「电影」系统写成 es_systems_custom.cfg。该文件名在 ES 里是「整份替换」
# 语义，会顶掉全部内置系统（实测：只剩 电影/All Games/Favorites）。新版用
# es_systems_movies.cfg —— 普通 es_systems_*.cfg 是叠加合并语义，内置系统不受影响。
if [ -f "$ESDIR/es_systems_custom.cfg" ]; then
  python3 "$HERE/lib/es_remove.py" "$ESDIR/es_systems_custom.cfg" || true
  if grep -q "<system>" "$ESDIR/es_systems_custom.cfg" 2>/dev/null; then
    echo "  ⚠ es_systems_custom.cfg 里还有其它系统，未删；请确认内置系统未被子集顶掉。"
  else
    rm -f "$ESDIR/es_systems_custom.cfg"
    echo "  已清理旧的 es_systems_custom.cfg（该文件名会顶掉内置系统）。"
  fi
fi
python3 "$HERE/lib/es_merge.py" "$ESDIR/es_systems_movies.cfg" \
        "$PAY/system/configs/emulationstation/es_systems_movies.cfg"

echo "== 5/5 F1 菜单快速启动项 =="
# 设置已并入「电影刮削」入口，清掉旧版单独安装的「刮削设置」项
rm -f "$APPDIR/scraper-settings.desktop"
for d in batoscrape kodi vlc; do
  src="$PAY/system/.local/share/applications/$d.desktop"
  dst="$APPDIR/$d.desktop"
  [ -f "$src" ] || continue
  if [ -f "$dst" ]; then
    echo "  已存在，保留：$d.desktop"
  else
    cp "$src" "$dst"
    echo "  安装：$d.desktop"
  fi
done

if [ "$DO_PLAYERS" = 1 ]; then
  echo "== 媒体播放器设置 =="
  sh "$HERE/players/setup-kodi.sh" || echo "  (Kodi 设置跳过)"
  sh "$HERE/players/setup-vlc.sh"  || echo "  (VLC 设置跳过)"
fi

cat <<'EOF'

安装完成。下一步：
  1) F1 菜单 → 「电影刮削」→ 首次进入会自动弹设置，填 TMDB API Key 或 Read Token（并选好代理）
  2) 设置里点「保存」后回到菜单，选「开始刮削」跑一次
  3) 重启 EmulationStation（或整机重启）→ 首页出现「电影」分页
注：影片文件请放在片库目录（默认 /userdata/roms/movies，实体在第三分区）。
EOF
