#!/usr/bin/env bash
# 电影刮削器 · Batocera 一键安装 / 更新 / 卸载
#
# 首次安装（国内镜像）：
#   curl -fsSL https://ghfast.top/https://raw.githubusercontent.com/anturong/batocera-movie-scraper/main/install.sh | bash
# 以后更新：
#   bash /userdata/system/movies/install.sh            # 有新版就更新
#   bash /userdata/system/movies/install.sh --check    # 只看版本
#   bash /userdata/system/movies/install.sh --force    # 强制重装当前版
#
# ★ 本脚本不写入、不覆盖任何 API key ★
#   配置固定在 /userdata/system/movies/scraper.json；不存在时只用空模板生成。
set -u

REPO="${MOVIESCRAPER_REPO:-anturong/batocera-movie-scraper}"
ASSET="moviescraper.tar.gz"
VER_ASSET="moviescraper.version"
SUM_ASSET="sha256sums.txt"
UD="${MOVIESCRAPER_ROOT:-/userdata}"
MV="$UD/system/movies"
ESDIR="$UD/system/configs/emulationstation"
APPDIR="$UD/system/.local/share/applications"
PORTS="$UD/roms/ports"

MODE=install
FORCE=0
DO_PLAYERS=1
ASSUME_YES=0
PURGE=0
LOCAL_DIR=""
ASSET_URL=""
SELF_DIR=""

usage() {
    cat <<'EOF'
电影刮削器 · Batocera 一键安装 / 更新 / 卸载

首次安装（国内镜像）：
  curl -fsSL https://ghfast.top/https://raw.githubusercontent.com/anturong/batocera-movie-scraper/main/install.sh | bash
以后更新：
  bash /userdata/system/movies/install.sh

参数：
  --check          只检查版本，不安装
  --force          强制重新下载并安装当前最新版
  --no-players     跳过 Kodi / VLC 的播放设置
  --uninstall      卸载（保留 scraper.json）
  --purge-config   卸载时同时删除 scraper.json（含你的 key）
  --yes, -y        卸载时不询问
  --local DIR      用本地目录安装（调试用）
  --asset-url URL  指定发布包地址（内网/调试用）
  --repo OWNER/REPO
                   覆盖 GitHub 仓库
EOF
}

die()  { echo "错误: $*" >&2; exit 1; }
info() { echo "== $* =="; }

while [ $# -gt 0 ]; do
    case "$1" in
        --check)        MODE=check ;;
        --force)        FORCE=1 ;;
        --no-players)   DO_PLAYERS=0 ;;
        --uninstall)    MODE=uninstall ;;
        --purge-config) PURGE=1 ;;
        --yes|-y)       ASSUME_YES=1 ;;
        --local)        LOCAL_DIR="${2:-}"; shift ;;
        --asset-url)    ASSET_URL="${2:-}"; shift ;;
        --repo)         REPO="${2:-}"; shift ;;
        -h|--help)      usage; exit 0 ;;
        *)              echo "未知参数: $1" >&2; usage; exit 2 ;;
    esac
    shift
done

[ -n "$REPO" ] || die "--repo 不能为空"

REL_BASE="${MOVIESCRAPER_ASSET_BASE:-https://github.com/$REPO/releases/latest/download}"
RAW_BASE="${MOVIESCRAPER_RAW_BASE:-https://raw.githubusercontent.com/$REPO/main}"

# ---------------------------------------------------------------- 网络
try_urls() {
    printf '%s\n' "$1"
    case "$1" in
        https://github.com/*|https://raw.githubusercontent.com/*|https://api.github.com/*)
            if [ "${MOVIESCRAPER_NO_MIRROR:-0}" != 1 ]; then
                printf '%s\n' "https://ghfast.top/$1"
                printf '%s\n' "https://gh-proxy.com/$1"
            fi
            ;;
    esac
}

fetch_to() { # url out [max-time]
    local url="$1" out="$2" mt="${3:-30}" u
    while IFS= read -r u; do
        [ -n "$u" ] || continue
        if curl -fsSL --retry 1 --connect-timeout 10 --max-time "$mt" -o "$out" "$u" 2>/dev/null \
           && [ -s "$out" ]; then
            return 0
        fi
    done <<EOF
$(try_urls "$url")
EOF
    return 1
}

latest_version() {
    local tmp v
    tmp=$(mktemp) || return 1
    if fetch_to "$REL_BASE/$VER_ASSET" "$tmp" 20; then
        v=$(head -n 1 "$tmp" | tr -d '[:space:]')
        rm -f "$tmp"
        [ -n "$v" ] && { printf '%s' "$v"; return 0; }
    fi
    rm -f "$tmp"
    tmp=$(mktemp) || return 1
    if fetch_to "$RAW_BASE/VERSION" "$tmp" 20; then
        v=$(head -n 1 "$tmp" | tr -d '[:space:]')
        rm -f "$tmp"
        [ -n "$v" ] && { printf '%s' "$v"; return 0; }
    fi
    rm -f "$tmp"
    return 1
}

installed_version() {
    [ -f "$MV/VERSION" ] && head -n 1 "$MV/VERSION" | tr -d '[:space:]' || true
}

install_complete() {
    [ -x "$MV/batoscrape" ] && [ -x "$MV/scrape-gui.sh" ] \
        && [ -f "$MV/settings.sh" ] && [ -f "$ESDIR/es_systems_movies.cfg" ]
}

# ---------------------------------------------------------------- 安装
install_from() {
    local src="$1" pay edir_lib d f
    pay="$src/deploy/payload"
    edir_lib="$src/deploy/lib"

    [ -d "$pay" ] || die "安装源不完整：找不到 $pay"
    [ -f "$pay/system/movies/batoscrape" ] || die "安装源里缺少 batoscrape（请先构建或改用 Release 安装）"
    [ -f "$src/VERSION" ] || die "安装源里缺少 VERSION"
    command -v python3 >/dev/null || die "缺少 python3（合并 EmulationStation 配置需要）"

    info "1/6 创建目录"
    mkdir -p "$MV/lib" "$MV/icons" "$ESDIR" "$APPDIR" "$PORTS"

    info "2/6 安装刮削器与脚本"
    cp -f "$pay/system/movies/batoscrape" "$MV/batoscrape"
    cp -f "$pay/system/movies/play.sh" "$MV/play.sh"
    cp -f "$pay/system/movies/scrape-gui.sh" "$MV/scrape-gui.sh"
    cp -f "$pay/system/movies/settings.sh" "$MV/settings.sh"
    cp -f "$pay/system/movies/lib/config_io.py" "$MV/lib/config_io.py"
    [ -f "$pay/system/movies/icons/scraper.png" ] && cp -f "$pay/system/movies/icons/scraper.png" "$MV/icons/scraper.png"
    cp -f "$pay/system/movies/scraper.example.json" "$MV/scraper.example.json"
    chmod +x "$MV/batoscrape" "$MV/play.sh" "$MV/scrape-gui.sh" "$MV/settings.sh"
    chmod 644 "$MV/scraper.example.json" "$MV/lib/config_io.py" 2>/dev/null || true
    [ -f "$MV/icons/scraper.png" ] && chmod 644 "$MV/icons/scraper.png"
    echo "  已写入 $MV"

    info "3/6 配置（保留已有 key）"
    if [ -f "$MV/scraper.json" ]; then
        echo "  已存在 scraper.json，原样保留（你的 key 不会动）。"
    else
        cp "$MV/scraper.example.json" "$MV/scraper.json"
        echo "  已生成空配置，装好后到 F1 菜单「电影刮削」里填写凭据。"
    fi
    chmod 644 "$MV/scraper.json" 2>/dev/null || true
    if [ -f "$pay/system/movies/scraper.json" ]; then
        echo "  安装源里发现 scraper.json，已忽略（不会安装任何 key）。"
    fi

    info "4/6 EmulationStation「电影」分页"
    if [ -f "$ESDIR/es_systems_custom.cfg" ]; then
        python3 "$edir_lib/es_remove.py" "$ESDIR/es_systems_custom.cfg" || true
        if grep -q "<system>" "$ESDIR/es_systems_custom.cfg" 2>/dev/null; then
            echo "  ⚠ es_systems_custom.cfg 里还有其它系统，未删；请确认内置系统未被顶掉。"
        else
            rm -f "$ESDIR/es_systems_custom.cfg"
            echo "  已清理旧的 es_systems_custom.cfg（该文件名会顶掉内置系统）。"
        fi
    fi
    python3 "$edir_lib/es_merge.py" "$ESDIR/es_systems_movies.cfg" \
        "$pay/system/configs/emulationstation/es_systems_movies.cfg"

    info "5/6 菜单与快捷方式"
    for d in batoscrape kodi vlc; do
        f="$pay/system/.local/share/applications/$d.desktop"
        [ -f "$f" ] || continue
        if [ "$d" = batoscrape ]; then
            cp -f "$f" "$APPDIR/$d.desktop"
        elif [ ! -f "$APPDIR/$d.desktop" ]; then
            cp "$f" "$APPDIR/$d.desktop"
        fi
        chmod 644 "$APPDIR/$d.desktop" 2>/dev/null || true
    done
    rm -f "$APPDIR/scraper-settings.desktop"
    cat > "$PORTS/电影刮削.sh" <<EOF
#!/bin/bash
exec "$MV/scrape-gui.sh"
EOF
    chmod +x "$PORTS/电影刮削.sh"
    echo "  F1 菜单：电影刮削 / Kodi / VLC"
    echo "  Ports：电影刮削"

    if [ "$DO_PLAYERS" = 1 ]; then
        info "播放器设置"
        sh "$src/deploy/players/setup-kodi.sh" || echo "  (Kodi 设置跳过)"
        sh "$src/deploy/players/setup-vlc.sh"  || echo "  (VLC 设置跳过)"
    fi

    info "6/6 版本与更新入口"
    cp -f "$src/VERSION" "$MV/VERSION"
    chmod 644 "$MV/VERSION" 2>/dev/null || true
    if [ -f "$src/install.sh" ]; then
        cp -f "$src/install.sh" "$MV/install.sh"
        chmod +x "$MV/install.sh"
    fi
    echo "  当前版本：$(installed_version)"

    curl -fs --max-time 5 http://127.0.0.1:1234/reloadgames >/dev/null 2>&1 || true

    cat <<EOF

安装完成 ✓
  1) F1 菜单 →「电影刮削」→ 设置：填自己的凭据（本包不含 key）
     TMDB Key/Token：https://www.themoviedb.org/settings/api
     射手网 Token：  https://assrt.net/usercp.php
     OpenSubtitles： https://www.opensubtitles.com/en/consumers
     （刮削核心默认豆瓣，免 key；不下载字幕也不需要以上任何一项）
  2) 把影片放进 /userdata/roms/movies，ES 首页会出现「电影」分页
  3) 以后更新：bash $MV/install.sh
  4) 卸载：bash $MV/install.sh --uninstall
EOF
}

# ---------------------------------------------------------------- 卸载
do_uninstall() {
    [ "$(id -u)" = 0 ] || die "需要 root 权限运行"
    if [ ! -d "$MV" ]; then
        echo "未安装（$MV 不存在）。"
        return 0
    fi

    echo "将删除："
    echo "  $MV 下的刮削器、脚本、图标、版本文件"
    echo "  $APPDIR/batoscrape.desktop"
    echo "  $PORTS/电影刮削.sh"
    echo "  从 es_systems_movies.cfg 移除 movies 系统"
    if [ "$PURGE" = 1 ]; then
        echo "  $MV/scraper.json（含你的 key）"
    else
        echo "  （保留 $MV/scraper.json 与影片库）"
    fi
    if [ "$ASSUME_YES" != 1 ]; then
        printf "确认卸载？(y/N) "
        a=""
        read -r a || true
        case "$a" in y|Y) ;; *) echo "已取消。"; return 0 ;; esac
    fi

    python3 - "$ESDIR/es_systems_movies.cfg" "$ESDIR/es_systems_custom.cfg" <<'PY'
import os, re, shutil, sys, datetime
for cfg in sys.argv[1:]:
    if not os.path.exists(cfg):
        continue
    cur = open(cfg, encoding="utf-8").read()
    new, n = re.subn(r"\s*<system>(?:(?!</system>).)*?<name>movies</name>.*?</system>", "", cur, flags=re.S)
    if n == 0:
        continue
    bak = cfg + ".bak-" + datetime.datetime.now().strftime("%Y%m%d%H%M%S")
    shutil.copy2(cfg, bak)
    open(cfg, "w", encoding="utf-8").write(new)
    print("已移除 movies 系统:", cfg)
PY

    rm -f "$MV/batoscrape" "$MV/play.sh" "$MV/scrape-gui.sh" "$MV/settings.sh" \
          "$MV/scraper.example.json" "$MV/install.sh" "$MV/VERSION"
    rm -rf "$MV/lib" "$MV/icons"
    [ "$PURGE" = 1 ] && rm -f "$MV/scraper.json"
    rm -f "$APPDIR/batoscrape.desktop" "$APPDIR/scraper-settings.desktop"
    rm -f "$PORTS/电影刮削.sh"
    rmdir "$MV" 2>/dev/null || true
    curl -fs --max-time 5 http://127.0.0.1:1234/reloadgames >/dev/null 2>&1 || true

    echo "卸载完成。影片库/海报与 Kodi/VLC 设置保持原样。"
    [ "$PURGE" != 1 ] && echo "（scraper.json 已保留；要一并删除请加 --purge-config）"
    return 0
}

# ---------------------------------------------------------------- 检查
do_check() {
    local cur latest
    cur=$(installed_version)
    latest=$(latest_version) || die "无法获取最新版本（网络不通？可用 --asset-url 指定安装包）"
    echo "已安装：${cur:-未安装}"
    echo "最新版：$latest"
    if [ -n "$cur" ] && [ "$cur" = "$latest" ] && install_complete; then
        echo "已是最新。"
    else
        echo "可以安装或更新：bash $MV/install.sh"
        [ "$(id -u)" = 0 ] || echo "（安装/更新请用 root 运行）"
    fi
}

# ---------------------------------------------------------------- 主流程
case "$MODE" in
    check)
        do_check
        exit 0
        ;;
    uninstall)
        do_uninstall
        exit $?
        ;;
esac

[ "$(id -u)" = 0 ] || die "需要 root 权限运行"

# 在本仓库里直接跑时，用本地文件（调试/离线）
script_path="${BASH_SOURCE[0]:-}"
if [ -n "$script_path" ] && [ -f "$script_path" ]; then
    SELF_DIR=$(cd "$(dirname "$script_path")" && pwd)
fi
if [ -z "$LOCAL_DIR" ] && [ -n "$SELF_DIR" ] && [ -f "$SELF_DIR/deploy/payload/system/movies/scrape-gui.sh" ]; then
    LOCAL_DIR="$SELF_DIR"
fi

if [ -n "$LOCAL_DIR" ]; then
    echo "使用本地安装源：$LOCAL_DIR"
    install_from "$LOCAL_DIR"
    exit 0
fi

cur=$(installed_version)
latest=$(latest_version) || die "无法获取最新版本（网络不通？请检查代理或用 --asset-url 指定安装包）"

if [ "$FORCE" != 1 ] && [ -n "$cur" ] && [ "$cur" = "$latest" ] && install_complete; then
    echo "已是最新（$latest），无需更新。要强制重装请加 --force。"
    exit 0
fi

info "下载 $latest"
tmp=$(mktemp -d) || die "无法创建临时目录"
trap 'rm -rf "$tmp"' EXIT
mkdir -p "$tmp/src"

if [ -n "$ASSET_URL" ]; then
    if [ -f "$ASSET_URL" ]; then
        cp -f "$ASSET_URL" "$tmp/$ASSET"
    else
        fetch_to "$ASSET_URL" "$tmp/$ASSET" 600 || die "下载安装包失败：$ASSET_URL"
    fi
else
    fetch_to "$REL_BASE/$ASSET" "$tmp/$ASSET" 600 || die "下载安装包失败（$REL_BASE/$ASSET）"
    if fetch_to "$REL_BASE/$SUM_ASSET" "$tmp/$SUM_ASSET" 20; then
        want=$(awk -v f="$ASSET" '$2 == f {print $1; exit}' "$tmp/$SUM_ASSET")
        got=$(sha256sum "$tmp/$ASSET" | awk '{print $1}')
        if [ -n "$want" ]; then
            [ "$want" = "$got" ] || die "sha256 校验失败（期望 $want，实际 $got）"
            echo "sha256 校验通过"
        fi
    fi
fi

tar -xzf "$tmp/$ASSET" -C "$tmp/src" || die "解压失败"
[ -f "$tmp/src/install.sh" ] || die "安装包里缺少 install.sh"
[ -d "$tmp/src/deploy" ] || die "安装包里缺少 deploy/"

if [ -f "$tmp/src/VERSION" ] && [ "$(head -n 1 "$tmp/src/VERSION" | tr -d '[:space:]')" != "$latest" ]; then
    echo "警告：安装包版本与最新版不一致，以安装包为准。"
fi

install_from "$tmp/src"
