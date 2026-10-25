#!/bin/sh
# 电影刮削器 —— F1 应用程序菜单唯一入口（GTK 窗口版，Pango 自带中文回退字体）
# 「设置」与「刮削」在这个入口里二合一：先弹菜单，选设置就进表单、选刮削就跑。
# 关键：Batocera 默认 locale 是 POSIX（非 UTF-8），GTK 会拒绝含中文的命令行参数
export LANG=zh_CN.utf8 LC_ALL=zh_CN.utf8
DIR=/userdata/system/movies
CONF=$DIR/scraper.json
BIN=$DIR/batoscrape
PY=$DIR/lib/config_io.py
LOG=/tmp/batoscrape_menu.log

if [ ! -x "$BIN" ]; then
  yad --error --title="Movie Scraper" --width=420 --text="找不到刮削器: $BIN"
  exit 1
fi

field() { python3 "$PY" "$CONF" get | cut -d'|' -f"$1"; }
needs_key() { python3 "$PY" "$CONF" needs-key; }

# 当前核心要 key 又没填：先弹设置界面（豆瓣核心免 key，直接进菜单）
if needs_key; then
  "$DIR/settings.sh"
  if needs_key; then
    yad --error --title="电影刮削" --width=440 \
        --text="当前核心（TMDB）还没填 API Key 或 Read Token，无法刮削。\n申请地址：https://www.themoviedb.org/settings/api\n请先在设置里填写，或改用豆瓣核心。"
    exit 1
  fi
fi

# 主菜单：设置与刮削合并在同一个入口（不再单独占一个 F1 项）
while :; do
  yad --title="电影刮削" --width=460 --height=200 \
      --window-icon=$DIR/icons/scraper.png \
      --text="片库: $(field 3)\n核心: $(field 1)\n\n请选择操作：" \
      --button="开始刮削:0" --button="设置…:1" --button="退出:2"
  case "$?" in
    0) break ;;                          # 去刮削
    1) "$DIR/settings.sh"; continue ;;   # 改完设置回到菜单
    *) exit 0 ;;                         # 退出 / 关窗
  esac
done

CL=$(field 1)
SD=$(field 3)
: > "$LOG"
{
  echo "电影刮削 · $CL"
  echo "片库: $SD"
  echo "--------------------------------"
} >> "$LOG"

# 刮削跑在本 shell 的子进程里，这样 wait 才能拿到它的退出码
"$BIN" scrape "$SD" --config "$CONF" >> "$LOG" 2>&1 &
PID=$!

# 实时跟随日志的窗口
yad --text-info --tail --filename="$LOG" \
    --title="电影刮削 · $CL" \
    --width=880 --height=560 --fixed --wrap \
    --window-icon=$DIR/icons/scraper.png \
    --button="关闭:0" &
YPID=$!

wait "$PID"; rc=$?
{
  echo "--------------------------------"
  if [ "$rc" -eq 0 ]; then
    echo ">> 刮削完成。回 EmulationStation 电影分页即可看到新海报与简介。"
  else
    echo ">> 刮削失败（退出码 $rc）。多为网络/代理问题。"
  fi
  echo ">> 按「关闭」退出。"
} >> "$LOG"

wait "$YPID"
rm -f "$LOG"
