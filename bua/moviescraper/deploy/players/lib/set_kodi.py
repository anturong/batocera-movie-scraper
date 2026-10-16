#!/usr/bin/env python3
# Kodi 设置（幂等）：界面改简体中文 + 加一条影片来源。
# 用法: set_kodi.py <kodi_userdata_dir> [来源路径]
# 例:   set_kodi.py /userdata/system/.kodi/userdata /media/sda3/roms/movies/
import os
import re
import shutil
import sys
import datetime

kd = sys.argv[1]
movie_src = sys.argv[2] if len(sys.argv) > 2 else "/media/sda3/roms/movies/"
ts = datetime.datetime.now().strftime("%Y%m%d%H%M%S")


def backup(p):
    if os.path.exists(p):
        shutil.copy2(p, p + ".bak-" + ts)


# 1) 界面语言 + 字体（Arial 有 CJK 回退，Kodi 自带）
gs = os.path.join(kd, "guisettings.xml")
if os.path.exists(gs):
    s = open(gs, encoding="utf-8").read()
    s2 = re.sub(r'(<setting id="locale\.language"[^>]*>)[^<]*',
                r'\1resource.language.zh_cn', s)
    s2 = re.sub(r'(<setting id="lookandfeel\.font"[^>]*>)[^<]*',
                r'\1Arial', s2)
    if s2 != s:
        backup(gs)
        open(gs, "w", encoding="utf-8").write(s2)
        print("Kodi 界面 → 简体中文")
    else:
        print("Kodi 界面已是简体中文")
else:
    print("缺 %s，跳过界面设置（先启动一次 Kodi）" % gs)

# 2) 影片来源
src = os.path.join(kd, "sources.xml")
if not os.path.exists(src):
    print("缺 %s，跳过来源设置（先启动一次 Kodi）" % src)
    sys.exit(0)

s = open(src, encoding="utf-8").read()
if movie_src in s:
    print("Kodi 影片来源已存在:", movie_src)
    sys.exit(0)

block = ('        <source>\n'
         '            <name>电影</name>\n'
         '            <path pathversion="1">%s</path>\n'
         '            <allowsharing>true</allowsharing>\n'
         '        </source>\n' % movie_src)

new = re.sub(r"(<video>\s*\n)", r"\1" + block, s, count=1)
if new == s:
    # 没有 <video> 标签的异常结构：在第一个 <default 前兜底插入
    new = re.sub(r"(<sources>\s*\n)", r"\1", s, count=1)
    sys.stderr.write("sources.xml 结构异常，未改。请手工加来源: %s\n" % movie_src)
    sys.exit(1)

backup(src)
open(src, "w", encoding="utf-8").write(new)
print("Kodi 已加影片来源:", movie_src)
