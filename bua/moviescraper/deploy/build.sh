#!/bin/sh
# 构建全部产物并铺进 deploy/。需要 .NET 8 SDK。在项目根或本目录直接跑：sh deploy/build.sh
set -e

DEPLOY=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
ROOT=$(CDPATH= cd -- "$DEPLOY/.." && pwd)
CORE="$ROOT/src/BatoceraMovieScraper/BatoceraMovieScraper.csproj"
GUI="$ROOT/src/BatoceraMovieScraper.Win/BatoceraMovieScraper.Win.csproj"

echo "== linux-x64 刮削器（Batocera 用）=="
dotnet publish "$CORE" -c Release -r linux-x64 --self-contained true \
  -p:PublishSingleFile=true -p:DebugType=none -o "$ROOT/dist/linux-x64"
cp -f "$ROOT/dist/linux-x64/batoscrape" "$DEPLOY/payload/system/movies/batoscrape"

echo "== win-x64 命令行 =="
dotnet publish "$CORE" -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:DebugType=none -o "$ROOT/dist/win-x64"
cp -f "$ROOT/dist/win-x64/batoscrape.exe" "$DEPLOY/windows/batoscrape.exe"

echo "== win-x64 图形界面（单文件）=="
dotnet publish "$GUI" -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true -p:DebugType=none -o "$ROOT/dist/win-gui"
cp -f "$ROOT/dist/win-gui/电影刮削器.exe" "$DEPLOY/windows/电影刮削器.exe"

echo "完成。产物：$ROOT/dist/{linux-x64,win-x64,win-gui}，已拷入 deploy/"
