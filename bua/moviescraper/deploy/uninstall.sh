#!/bin/sh
# 电影刮削器 · 卸载
# 移除：刮削器自带文件、F1 菜单入口、ES「电影」分页注册。
# 不动：影片库与海报（/userdata/roms/movies 下的影片）、Kodi 与 VLC 的设置。
#     sh uninstall.sh                 # 保留 scraper.json
#     sh uninstall.sh --purge-config  # 连 scraper.json（含你的 key）一起删
set -e

HERE=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
UD=/userdata
MV="$UD/system/movies"
ESDIR="$UD/system/configs/emulationstation"
APPDIR="$UD/system/.local/share/applications"
PURGE=0
[ "$1" = "--purge-config" ] && PURGE=1

[ "$(id -u)" = 0 ] || { echo "需要 root 权限运行。"; exit 1; }

echo "将删除："
echo "  $MV/{batoscrape,play.sh,scrape-gui.sh,settings.sh,scraper.example.json} + lib/ icons/"
echo "  $APPDIR/{batoscrape,scraper-settings}.desktop"
echo "  从 es_systems_movies.cfg 移除 movies 系统"
[ "$PURGE" = 1 ] && echo "  $MV/scraper.json（含你的 TMDB key）"
echo
echo "不会动：影片库/海报（$UD/roms/movies）、Kodi 与 VLC 的设置。"
printf "确认卸载？(y/N) "
read a
case "$a" in y|Y) ;; *) echo "已取消。"; exit 0;; esac

rm -f "$MV/batoscrape" "$MV/play.sh" "$MV/scrape-gui.sh" "$MV/settings.sh" \
      "$MV/scraper.example.json"
rm -rf "$MV/lib" "$MV/icons"
[ "$PURGE" = 1 ] && rm -f "$MV/scraper.json"
rm -f "$APPDIR/batoscrape.desktop" "$APPDIR/scraper-settings.desktop"
python3 "$HERE/lib/es_remove.py" "$ESDIR/es_systems_movies.cfg"
# 兼容旧版残留：旧文件名（整份替换语义）如还在，一并清掉，避免顶掉内置系统
python3 "$HERE/lib/es_remove.py" "$ESDIR/es_systems_custom.cfg"
rmdir "$MV" 2>/dev/null || true

echo "卸载完成。Kodi/VLC 设置与影片库保持原样。"
