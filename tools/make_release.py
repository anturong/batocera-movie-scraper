#!/usr/bin/env python3
"""打 Batocera 安装包：moviescraper.tar.gz + moviescraper.version + sha256sums.txt

白名单打包，任何含 key 的配置都不会进入产物；打完后再扫一遍，发现非空凭据直接失败。
用法：
    python tools/make_release.py [--dist dist-release]
"""
import argparse
import hashlib
import json
import os
import shutil
import sys
import tarfile
import tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent

FILES = [
    "install.sh",
    "VERSION",
    "deploy/lib/es_merge.py",
    "deploy/lib/es_remove.py",
    "deploy/players/setup-kodi.sh",
    "deploy/players/setup-vlc.sh",
    "deploy/players/lib/set_kodi.py",
    "deploy/players/files/kodi-sources.xml",
    "deploy/players/files/vlcrc",
    "deploy/payload/system/movies/batoscrape",
    "deploy/payload/system/movies/play.sh",
    "deploy/payload/system/movies/scrape-gui.sh",
    "deploy/payload/system/movies/settings.sh",
    "deploy/payload/system/movies/scraper.example.json",
    "deploy/payload/system/movies/lib/config_io.py",
    "deploy/payload/system/movies/icons/scraper.png",
    "deploy/payload/system/configs/emulationstation/es_systems_movies.cfg",
    "deploy/payload/system/.local/share/applications/batoscrape.desktop",
    "deploy/payload/system/.local/share/applications/kodi.desktop",
    "deploy/payload/system/.local/share/applications/vlc.desktop",
]

EXECUTABLE = {
    "install.sh",
    "deploy/payload/system/movies/batoscrape",
    "deploy/payload/system/movies/play.sh",
    "deploy/payload/system/movies/scrape-gui.sh",
    "deploy/payload/system/movies/settings.sh",
    "deploy/players/setup-kodi.sh",
    "deploy/players/setup-vlc.sh",
}

# scraper.example.json 里这些字段必须为空
SENSITIVE_PATHS = [
    ("tmdb", "apiKey"),
    ("tmdb", "readAccessToken"),
    ("proxy", "url"),
    ("subtitles", "assrtToken"),
    ("subtitles", "apiKey"),
    ("subtitles", "username"),
    ("subtitles", "password"),
]

FORBIDDEN_NAMES = {"scraper.json", "config.json", "config.local.json"}
FORBIDDEN_SUFFIXES = {".exe"}


def fail(msg):
    sys.stderr.write("错误: %s\n" % msg)
    sys.exit(1)


def stage(dist_dir):
    if not (ROOT / "install.sh").is_file():
        fail("找不到 install.sh")
    if not (ROOT / "VERSION").is_file():
        fail("找不到 VERSION")
    missing = [rel for rel in FILES if not (ROOT / rel).is_file()]
    if missing:
        fail("缺少文件：\n  " + "\n  ".join(missing))

    binary = ROOT / "deploy/payload/system/movies/batoscrape"
    if binary.stat().st_size < 1024 * 1024:
        fail("batoscrape 看起来不是完整二进制（%d 字节）。先跑 sh deploy/build.sh" % binary.stat().st_size)

    tmp = Path(tempfile.mkdtemp(prefix="moviescraper-release-"))
    for rel in FILES:
        src = ROOT / rel
        dst = tmp / rel
        dst.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(src, dst)
        name = Path(rel).name
        if name in FORBIDDEN_NAMES or Path(rel).suffix.lower() in FORBIDDEN_SUFFIXES:
            fail("白名单里出现禁止分发的文件：%s" % rel)
        os.chmod(dst, 0o755 if rel in EXECUTABLE else 0o644)
    return tmp


def scan_keys(stage_dir):
    bad = []
    for p in stage_dir.rglob("*"):
        if not p.is_file():
            continue
        rel = p.relative_to(stage_dir).as_posix()
        if p.name in FORBIDDEN_NAMES or p.suffix.lower() in FORBIDDEN_SUFFIXES:
            bad.append("禁止分发的文件：%s" % rel)
            continue
        if p.suffix.lower() != ".json":
            continue
        try:
            data = json.loads(p.read_text(encoding="utf-8"))
        except Exception as e:
            bad.append("%s 不是合法 JSON：%s" % (rel, e))
            continue
        for a, b in SENSITIVE_PATHS:
            v = (data.get(a) or {}).get(b) if isinstance(data.get(a), dict) else None
            if isinstance(v, str) and v.strip():
                bad.append("%s 的 %s.%s 非空" % (rel, a, b))
        proxy = data.get("proxy")
        if isinstance(proxy, dict) and proxy.get("enabled") is True:
            bad.append("%s 里 proxy.enabled=true" % rel)
    if bad:
        fail("产物里发现可疑凭据：\n  " + "\n  ".join(bad))


def make_tar(stage_dir, out):
    epoch = int(os.environ.get("SOURCE_DATE_EPOCH", "1792886400"))
    with tarfile.open(out, "w:gz", compresslevel=9) as tf:
        for rel in FILES:
            src = stage_dir / rel
            ti = tf.gettarinfo(str(src), arcname=rel)
            ti.uid = ti.gid = 0
            ti.uname = ti.gname = "root"
            ti.mtime = epoch
            ti.mode = 0o755 if rel in EXECUTABLE else 0o644
            with open(src, "rb") as f:
                tf.addfile(ti, f)


def sha256(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1024 * 1024), b""):
            h.update(chunk)
    return h.hexdigest()


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--dist", default="dist-release")
    args = ap.parse_args()

    version = (ROOT / "VERSION").read_text(encoding="utf-8").strip()
    if not version:
        fail("VERSION 为空")

    dist = Path(args.dist)
    if not dist.is_absolute():
        dist = ROOT / dist
    dist.mkdir(parents=True, exist_ok=True)

    stage_dir = stage(dist)
    try:
        scan_keys(stage_dir)

        tar_path = dist / "moviescraper.tar.gz"
        make_tar(stage_dir, tar_path)
        (dist / "moviescraper.version").write_text(version + "\n", encoding="utf-8", newline="\n")
        digest = sha256(tar_path)
        (dist / "sha256sums.txt").write_text("%s  moviescraper.tar.gz\n" % digest, encoding="utf-8", newline="\n")
    finally:
        shutil.rmtree(stage_dir, ignore_errors=True)

    print("版本：%s" % version)
    print("产物：%s" % dist)
    for name in ("moviescraper.tar.gz", "moviescraper.version", "sha256sums.txt"):
        p = dist / name
        print("  %-24s %9d 字节" % (name, p.stat().st_size))
    print("sha256：%s" % digest)


if __name__ == "__main__":
    main()
