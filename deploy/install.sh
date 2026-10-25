#!/bin/sh
# 兼容入口：安装/更新/卸载逻辑都在仓库根目录的 install.sh。
# 保留本文件是为了让旧文档里的 `sh deploy/install.sh` 继续可用。
HERE=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
exec bash "$HERE/../install.sh" "$@"
