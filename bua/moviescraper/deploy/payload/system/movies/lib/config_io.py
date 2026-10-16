#!/usr/bin/env python3
# 配置读写助手：给 shell/yad 设置界面用，避免在 shell 里拼 JSON。
#   get     -> 打印 13 段（| 分隔，顺序即设置表单字段顺序）：
#              core|lang|src|proxy|tmdb_key|tmdb_token|sub_en|sub_src|sub_langs|sub_token|sub_key|sub_user|sub_pass
#   set     -> 从 stdin 读同样 13 段写回（保留 yad 不认识的段，如 userAgent/maxDownloads）
#   has-key -> 有 tmdb key 或 token 则退出码 0，否则 1
#   cores   -> 打印 yad 组合框选项（'!' 分隔，'^' 标当前项）
#   sources -> 打印字幕源组合框选项（同上格式）
#   subsrc-id <显示名> -> 打印对应字幕源 id（assrt / opensubtitles）
import json
import sys

conf = sys.argv[1]
cmd = sys.argv[2] if len(sys.argv) > 2 else "get"

DEFAULT_SRC = "/userdata/roms/movies"
DEFAULT_CORE = "douban"  # 国内优先：默认豆瓣核心（免 key、国内直连）
DEFAULT_SUB = "assrt"    # 国内优先：默认射手网(伪)字幕源

# 刮削核心：id ↔ 下拉显示名。国内优先放最前。新增核心只改这里。
CORES = [
    ("douban", "豆瓣（内置）"),
    ("tmdb", "TMDB（内置）"),
    ("nova", "Nova（开发中）"),
]

# 字幕源：id ↔ 下拉显示名。国内优先放最前。
SUB_SOURCES = [
    ("assrt", "射手网(伪)·国内"),
    ("opensubtitles", "OpenSubtitles"),
]

# 需要填 API key 才能刮的核心（豆瓣免 key）。
KEYED = {"tmdb"}


def _label(pairs, cid, default):
    for i, l in pairs:
        if i == cid:
            return l
    return dict(pairs).get(default) or pairs[0][1]


def _id(pairs, label, default):
    for i, l in pairs:
        if label == l or label == i:
            return i
    return default


def load():
    try:
        with open(conf, encoding="utf-8") as f:
            return json.load(f)
    except Exception:
        return {}


def save(d):
    with open(conf, "w", encoding="utf-8") as f:
        json.dump(d, f, ensure_ascii=False, indent=2)
        f.write("\n")


if cmd == "get":
    d = load()
    t = d.get("tmdb") or {}
    p = d.get("proxy") or {}
    l = d.get("library") or {}
    s = d.get("scraper") or {}
    sub = d.get("subtitles") or {}
    print("|".join([
        _label(CORES, s.get("core") or DEFAULT_CORE, DEFAULT_CORE),
        t.get("language") or "zh-CN",
        l.get("sourceDir") or DEFAULT_SRC,
        p.get("url") or "",
        t.get("apiKey") or "",
        t.get("readAccessToken") or "",
        "true" if sub.get("enabled") else "false",
        _label(SUB_SOURCES, sub.get("source") or DEFAULT_SUB, DEFAULT_SUB),
        sub.get("languages") or "zh-cn,en",
        sub.get("assrtToken") or "",
        sub.get("apiKey") or "",
        sub.get("username") or "",
        sub.get("password") or "",
    ]))
elif cmd == "cores":
    d = load()
    cid = (d.get("scraper") or {}).get("core") or DEFAULT_CORE
    print("!".join(("^" + l) if i == cid else l for i, l in CORES))
elif cmd == "sources":
    d = load()
    sid = (d.get("subtitles") or {}).get("source") or DEFAULT_SUB
    print("!".join(("^" + l) if i == sid else l for i, l in SUB_SOURCES))
elif cmd == "set":
    line = sys.stdin.read().rstrip("\n")
    parts = (line.split("|") + [""] * 13)[:13]
    corelab, lang, src, prox, key, tok, sen, srclab, slang, stok, skey, suser, spass = \
        [x.strip() for x in parts]
    d = load()  # 保留 yad 界面不认识的段（如 Win 端写进 subtitles 的其它键），别整段覆盖
    d["scraper"] = {"core": _id(CORES, corelab, DEFAULT_CORE)}
    d["tmdb"] = {"apiKey": key, "readAccessToken": tok, "language": lang or "zh-CN"}
    d["proxy"] = {"enabled": bool(prox), "url": prox}
    d["library"] = {"sourceDir": src or DEFAULT_SRC}
    sub = d.get("subtitles") or {}
    sub["enabled"] = sen.lower() in ("true", "1", "yes", "on")
    sub["source"] = _id(SUB_SOURCES, srclab, DEFAULT_SUB)
    sub["languages"] = slang or "zh-cn,en"
    sub["assrtToken"] = stok
    sub["apiKey"] = skey
    sub["username"] = suser
    sub["password"] = spass
    d["subtitles"] = sub
    save(d)
elif cmd == "subsrc-id":
    # 供设置界面按字幕源分流：把下拉显示名换算成 id（assrt / opensubtitles）
    print(_id(SUB_SOURCES, sys.argv[3] if len(sys.argv) > 3 else "", DEFAULT_SUB))
elif cmd == "needs-key":
    # 当前核心需要 key 且还没填 → 0（须先进设置）；否则 1（可直接刮）。
    d = load()
    cid = (d.get("scraper") or {}).get("core") or DEFAULT_CORE
    t = d.get("tmdb") or {}
    have = bool((t.get("apiKey") or "").strip() or (t.get("readAccessToken") or "").strip())
    sys.exit(0 if (cid in KEYED and not have) else 1)
elif cmd == "has-key":
    d = load()
    t = d.get("tmdb") or {}
    ok = bool((t.get("apiKey") or "").strip() or (t.get("readAccessToken") or "").strip())
    sys.exit(0 if ok else 1)
else:
    sys.stderr.write(f"未知命令: {cmd}\n")
    sys.exit(2)
