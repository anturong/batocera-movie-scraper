#!/bin/sh
# Kodi：设为简体中文界面 + 加一条影片来源（默认第三分区）。幂等，改动前备份。
# 来源路径可用环境变量覆盖：MOVIE_SRC=/userdata/roms/movies/ sh setup-kodi.sh
HERE=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
KD=/userdata/system/.kodi/userdata
SRC="${MOVIE_SRC:-/media/sda3/roms/movies/}"

if [ ! -d "$KD" ]; then
  echo "Kodi 未初始化（$KD 不存在），跳过。先启动一次 Kodi 再运行本脚本即可。"
  exit 0
fi

python3 "$HERE/lib/set_kodi.py" "$KD" "$SRC"
echo "Kodi 设置完成（下次启动生效）。"
