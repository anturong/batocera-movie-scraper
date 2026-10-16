#!/bin/sh
# 电影刮削 · 设置界面（GTK 表单，两段式）
# 第一段：通用项（刮削核心默认豆瓣·国内 / 语言 / 片库 / 代理 / TMDB key / 字幕通用项）。
# 第二段：仅当勾了「下载字幕」才弹出，且按所选字幕源只显示该源需要的凭据
#         （射手网只留 Token；OpenSubtitles 只留 key/账号/密码），不再一堆框挤一起。
# 字段顺序必须与 lib/config_io.py 的 set 一致。
export LANG=zh_CN.utf8 LC_ALL=zh_CN.utf8
DIR=/userdata/system/movies
CONF=$DIR/scraper.json
PY=$DIR/lib/config_io.py

OLD_IFS=$IFS
IFS='|' read cl lg sd px k tok sen ssrc slang stok skey suser spass <<EOF
$(python3 "$PY" "$CONF" get)
EOF
IFS=$OLD_IFS

CORES=$(python3 "$PY" "$CONF" cores)
SOURCES=$(python3 "$PY" "$CONF" sources)
sen_init=$(printf '%s' "$sen" | tr 'a-z' 'A-Z')   # yad 复选框认 TRUE/FALSE

out=$(yad --form --title="电影刮削 · 设置" --width=820 --height=430 \
    --window-icon=$DIR/icons/scraper.png \
    --field="刮削核心:CB" "$CORES" \
    --field="元数据语言（如 zh-CN / en-US）:" "$lg" \
    --field="片库目录:DIR" "$sd" \
    --field="代理地址（留空=直连；例 socks5://127.0.0.1:10808）:" "$px" \
    --field="TMDB API Key (v3，仅 TMDB 核心需要):" "$k" \
    --field="TMDB Read Token (v4，key 与 token 二选一):" "$tok" \
    --field="下载字幕:CHK" "$sen_init" \
    --field="字幕源:CB" "$SOURCES" \
    --field="字幕语言（逗号分隔，如 zh-cn,en）:" "$slang" \
    --button="取消:1" --button="下一步:0")
[ $? -ne 0 ] && exit 0

OLD_IFS=$IFS
IFS='|' read cl lg sd px k tok sen ssrc slang <<EOF
$out
EOF
IFS=$OLD_IFS

# 第二段：按字幕源只问该源的凭据。不勾下载字幕则跳过，保留原有凭据。
if [ "$sen" = "TRUE" ]; then
    sid=$(python3 "$PY" "$CONF" subsrc-id "$ssrc")
    if [ "$sid" = "opensubtitles" ]; then
        out2=$(yad --form --title="字幕源：OpenSubtitles" --width=580 \
            --field="OpenSubtitles API Key（opensubtitles.com 账号里免费生成）:" "$skey" \
            --field="OpenSubtitles 账号（邮箱）:" "$suser" \
            --field="OpenSubtitles 密码:H" "$spass" \
            --button="取消:1" --button="保存:0")
        [ $? -ne 0 ] && exit 0
        OLD_IFS=$IFS; IFS='|' read skey suser spass <<EOF
$out2
EOF
        IFS=$OLD_IFS
    else
        out2=$(yad --form --title="字幕源：射手网(伪)·国内" --width=580 \
            --field="射手网 Token（登录 assrt.net →「用户面板」复制 32 位 Token）:" "$stok" \
            --button="取消:1" --button="保存:0")
        [ $? -ne 0 ] && exit 0
        OLD_IFS=$IFS; IFS='|' read stok <<EOF
$out2
EOF
        IFS=$OLD_IFS
    fi
fi

printf '%s|%s|%s|%s|%s|%s|%s|%s|%s|%s|%s|%s|%s' \
    "$cl" "$lg" "$sd" "$px" "$k" "$tok" "$sen" "$ssrc" "$slang" "$stok" "$skey" "$suser" "$spass" \
    | python3 "$PY" "$CONF" set &&
    yad --info --title="电影刮削 · 设置" --width=460 \
        --text="已保存到:\n$CONF" --button=OK:0
