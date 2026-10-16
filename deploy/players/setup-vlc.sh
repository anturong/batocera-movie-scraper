#!/bin/sh
# VLC：写一份最小 vlcrc，指定中文字幕字体（Noto Sans SC）。已有配置则不动。
HERE=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
VD=/userdata/system/.config/vlc
RC=$VD/vlcrc

mkdir -p "$VD"
if [ -f "$RC" ]; then
  echo "已有 VLC 配置，未覆盖：$RC"
else
  cp "$HERE/files/vlcrc" "$RC"
  echo "已写入 VLC 配置（中文字幕字体 Noto Sans SC）：$RC"
fi
