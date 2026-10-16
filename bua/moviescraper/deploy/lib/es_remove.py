#!/usr/bin/env python3
# 从 es_systems_movies.cfg 移除 movies <system>（卸载用）。备份后改。
# 用法: es_remove.py <cfg>
import os
import re
import shutil
import sys
import datetime

cfg = sys.argv[1]
if not os.path.exists(cfg):
    print("无此文件，跳过:", cfg)
    sys.exit(0)

cur = open(cfg, encoding="utf-8").read()
# 只删 name=movies 的那个 <system> 块
new, n = re.subn(r"\s*<system>(?:(?!</system>).)*?<name>movies</name>.*?</system>", "", cur, flags=re.S)
if n == 0:
    print("未发现 movies 系统，跳过")
    sys.exit(0)

bak = cfg + ".bak-" + datetime.datetime.now().strftime("%Y%m%d%H%M%S")
shutil.copy2(cfg, bak)
open(cfg, "w", encoding="utf-8").write(new)
print("已移除 movies 系统 (备份 %s)" % os.path.basename(bak))
