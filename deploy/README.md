# Batocera 电影刮削器 · 可移植部署包

把影视刮削接进 Batocera 的 EmulationStation 首页**独立「电影」分页**（海报网格卡片 +
mpv/VLC 播放），并顺带把 **Kodi 设为中文并把影片库指到第三分区**、**VLC 中文字幕字体**。
丢到任意 Batocera 盘上一键布置，可重复运行，带卸载。

## 目录结构

```
deploy/
├─ install.sh              一键安装（幂等）
├─ uninstall.sh            卸载（不动影片库）
├─ build.sh                从源码编双平台产物（需 .NET 8 SDK）
├─ 安装步骤.md             照着做的安装操作手册（Step by step）
├─ DEPLOY.md               移植布置：部署件落盘位置与机制（详细）
├─ payload/                ← /userdata 的镜像，install.sh 照此铺开
│  └─ system/
│     ├─ movies/           刮削器二进制 + 脚本 + lib/config_io.py + icons/
│     ├─ configs/emulationstation/es_systems_movies.cfg   ES「电影」分页
│     └─ .local/share/applications/*.desktop              F1 菜单快速启动项
├─ players/                Kodi 中文界面 + 第三分区来源；VLC 中文字幕字体
└─ windows/                Windows 端：图形界面 + 命令行（同一核心）
   ├─ 电影刮削器.exe        图形界面（WinForms，单文件）
   ├─ batoscrape.exe       命令行
   └─ config.example.json  配置样例
```

## 安装

推荐直接一键安装/更新（安装器会下载 Release、校验、装菜单与 ES 分页）：

```sh
curl -fsSL https://ghfast.top/https://raw.githubusercontent.com/anturong/batocera-movie-scraper/main/install.sh | bash
bash /userdata/system/movies/install.sh            # 以后更新
bash /userdata/system/movies/install.sh --check    # 只看版本
```

离线安装：

1. 把整个 `deploy/` 目录拷到盘上，例如 `/userdata/deploy/`。
2. 在盘上以 root 运行：

   ```sh
   sh /userdata/deploy/install.sh              # 全装（含 Kodi/VLC 设置）
   sh /userdata/deploy/install.sh --no-players # 只装刮削器与电影分页
   ```

   `deploy/install.sh` 现在只是仓库根目录 `install.sh` 的快捷入口，行为完全一致。

3. F1 菜单 →「电影刮削」→ 首次进入自动弹设置，填 **TMDB API Key 或 Read Token**（可设代理），选好刮削核心。
   TMDB 注册 <https://www.themoviedb.org/signup>，申请 Key / Read Token 到
   <https://www.themoviedb.org/settings/api>。
   若要下字幕：勾「下载字幕」，**字幕源默认「射手网(伪)·国内」**（免翻墙），需填它的 **Token**
   —— 登录 <https://assrt.net/usercp.php>（「用户面板」）复制 32 位 Token，API 文档见
   <https://assrt.net/api/doc>。也可改成 OpenSubtitles：在
   <https://www.opensubtitles.com/en/consumers> 生成 API Key。
4. 设置里点「保存」回到菜单，选「开始刮削」跑一次。
5. 重启 EmulationStation / 整机 → 首页出现「电影」分页。

> 「设置」与「刮削」合并在同一个 F1 入口「电影刮削」里，不再单独占一个菜单项。

> **本包不含任何 key。** 安装器只从 `scraper.example.json` 生成空模板；
> 机器上已有 `/userdata/system/movies/scraper.json` 时不会覆盖。
> 开发目录里的 `scraper.json` / `windows/config.json` 已被 `.gitignore` 排除，
> 发布包由 `tools/make_release.py` 白名单 + 扫描生成，不会带出去。

## 依赖

- Batocera 43.x。`mpv`、`python3`、`yad`、`Kodi 21`、`VLC 3` 均为系统自带，无需另装。
- 刮削器是 **.NET 8 自包含单文件**，目标盘无需装运行时。

## 影片库位置

默认片库目录 `/userdata/roms/movies`（mergerfs 合成视图）。**实体建议放第三分区**
（如 `/media/sda3/roms/movies`），别放 `sda2` 上的小系统分区，否则很快写满。
Kodi 影片来源默认指向 `/media/sda3/roms/movies/`，可用环境变量覆盖：

```sh
MOVIE_SRC=/userdata/roms/movies/ sh players/setup-kodi.sh
```

## 卸载

```sh
sh uninstall.sh                 # 保留 scraper.json
sh uninstall.sh --purge-config  # 连 scraper.json（含你的 key）一起删
```

卸载**不动**影片库与海报，也不动 Kodi/VLC 的设置。

## Windows 10（图形界面）

Windows 端带一个**图形界面**，双击即用：

- `deploy/windows/电影刮削器.exe` —— 界面里填 TMDB Key/Token/代理，用**「选择文件夹…」
  挑电影目录**，选刮削核心，点「开始刮削」，下方实时滚动日志；设置存到同目录 `config.json`。
  TMDB Key/Token：<https://www.themoviedb.org/settings/api>。
  勾「启用字幕下载」后选字幕源（默认国内**射手网(伪)**，Token 在
  <https://assrt.net/usercp.php> 复制；或 OpenSubtitles，API Key 在
  <https://www.opensubtitles.com/en/consumers> 生成）。
- `deploy/windows/batoscrape.exe` —— 同一核心的命令行版（`scrape` / `render`），供脚本/批处理。
- `deploy/windows/config.example.json` —— 配置样例（GUI 首次「保存设置」会自动生成 `config.json`）。

已自带 .NET 运行时（自包含单文件），目标机无需装 .NET。TMDB 被墙时在「代理」填
`socks5h://127.0.0.1:10808`（本机 v2rayN 默认端口）。

## 从源码构建

```sh
sh build.sh    # 需 .NET 8 SDK；产出 dist/{linux-x64,win-x64}，并自动拷入 deploy/
```

## 设计说明

- 刮削核心与展示层分离：核心产出中立清单 `manifest.json`（唯一真源），渲染器吐
  `gamelist.xml`（给 ES）与海报；`--core` 可在 tmdb / nova 间切换。
- 跨平台：纯控制台 `net8.0`（不带 `-windows`），按 RID 自包含发布 `win-x64` / `linux-x64`。
- **不修改 EmulationStation 源码**；不依赖 Kodi 或 ARRM/Skyscraper 做刮削。
- 路径相对 `/userdata`，不写死盘符；ES 侧 `gamelist.xml` 用 POSIX 正斜杠路径。
