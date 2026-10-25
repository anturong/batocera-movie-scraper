#!/bin/sh
# 兼容入口：真正的卸载逻辑在仓库根目录的 install.sh。
HERE=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
exec bash "$HERE/../install.sh" --uninstall "$@"
