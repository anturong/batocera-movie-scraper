#!/usr/bin/env python3
# 把 payload 里的 movies <system> 合并进目标 es_systems_movies.cfg。
# 注意：目标文件名必须形如 es_systems_<名字>.cfg；es_systems_custom.cfg 在 ES 里是
# 「整份替换」语义（会顶掉全部内置系统），不要用它。
# 用法: es_merge.py <目标cfg> <payload cfg>
# 规则: 目标不存在 -> 直接写入; 已含 movies 系统 -> 备份后用 payload 的块整段替换
#       （movies 整块归本项目所有，这样升级能带上新模拟器/新命令）;
#       否则备份后插入。
import os
import re
import shutil
import sys
import datetime

target, src = sys.argv[1], sys.argv[2]
block = open(src, encoding="utf-8").read()

m = re.search(r"<system>.*?</system>", block, re.S)
if not m:
    sys.stderr.write("payload 里没有 <system> 块: %s\n" % src)
    sys.exit(2)
sysblock = m.group(0)


def find_movies(text):
    """返回包含 <name>movies</name> 的 <system>…</system> 匹配对象。"""
    for mm in re.finditer(r"<system>.*?</system>", text, re.S):
        if "<name>movies</name>" in mm.group(0):
            return mm
    return None


def backup(path):
    bak = path + ".bak-" + datetime.datetime.now().strftime("%Y%m%d%H%M%S")
    shutil.copy2(path, bak)
    return bak


if not os.path.exists(target):
    os.makedirs(os.path.dirname(target), exist_ok=True)
    open(target, "w", encoding="utf-8").write(block)
    print("写入新配置:", target)
    sys.exit(0)

cur = open(target, encoding="utf-8").read()

old = find_movies(cur)
if old is not None:
    if old.group(0).strip() == sysblock.strip():
        print("movies 系统已是最新，未改")
        sys.exit(0)
    bak = backup(target)
    new = cur[:old.start()] + sysblock + cur[old.end():]
    open(target, "w", encoding="utf-8").write(new)
    print("已更新 movies 系统到", target, "(备份", os.path.basename(bak) + ")")
    sys.exit(0)

if "</systemList>" not in cur:
    sys.stderr.write("现有配置没有 </systemList>，未改。请手工合并:\n%s\n" % target)
    sys.exit(3)

bak = backup(target)
new = cur.replace("</systemList>", "  " + sysblock + "\n</systemList>")
open(target, "w", encoding="utf-8").write(new)
print("已合并 movies 系统到", target, "(备份", os.path.basename(bak) + ")")
