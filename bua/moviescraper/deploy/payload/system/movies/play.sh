#!/bin/sh
# 电影分发器：ES 把选中的核心名作为 $2 (%CORE%) 传入。
#   vlc / mpv → 播放
rom="$1"
core="$2"

case "$core" in
  vlc)
    # 原来用 --intf dummy：无界面 → 没有任何窗口/按键能关掉它，ES 只能干等，
    # 于是“退出电影”后 VLC 仍在后台跑。改用 Qt 界面即有窗口可关/可 Ctrl+Q。
    # VM/软件 GL 下 Qt 默认走 EGL 会 abort，强制 X11 平台（QT_QPA_PLATFORM=xcb）即可。
    exec env QT_QPA_PLATFORM=xcb /usr/bin/vlc --fullscreen --no-video-title-show --vout xcb_x11 "$rom"
    ;;
  *)
    exec /usr/bin/mpv --fullscreen --really-quiet "$rom"
    ;;
esac
