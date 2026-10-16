# Batocera 影视刮削器（batoscrape）

把电影 / 剧集目录刮削成 **EmulationStation 能用的 `gamelist.xml`** + 封面 / 剧照 / 简介 / 评分，
可选下载字幕。刮削核心支持 **豆瓣（默认，国内直连）**、**TMDB**（可选代理）、**nova**；
字幕源支持 **射手网(伪)·assrt** 与 **OpenSubtitles**。

Windows 端另带图形界面（`电影刮削器.exe`），命令行端为 `batoscrape`。

---

## 功能

- 扫描影片目录 → 生成 `manifest.json` → 渲染 ES 影视页的 `gamelist.xml`
- 元数据：标题 / 原名 / 年份 / 评分 / 类型 / 简介 / 演职员 / 封面 / 背景图，可按语言（`zh-CN` / `en-US` …）抓取
- 字幕：assrt（只需 32 位 Token）或 OpenSubtitles（key + 账号 + 密码），可指定语言
- 代理：`socks5://127.0.0.1:10808` 之类，TMDB 被墙时使用
- 只刮一部：`batoscrape scrape <目录> --only <影片>`

---

## 安装到 Batocera

### 方式一：附加组件（推荐，BUA 风格）

仓库里的 **[`bua/moviescraper/`](bua/moviescraper/)** 就是可直接使用的附加组件（离线安装）：

1. 从本仓库的 **Releases** 下载 `batoscrape`（linux-x64 自包含单文件），
   放到 `bua/moviescraper/deploy/payload/system/movies/batoscrape`
   （该二进制不入库，所以需要单独下载；也可以自己 `sh deploy/build.sh` 编一个）
2. 把整个 `moviescraper/` 目录拷到 Batocera，root 下执行：

```sh
bash moviescraper.sh                 # 全装（含 Kodi / VLC 播放器设置）
bash moviescraper.sh --no-players    # 只装刮削器与 ES「电影」分页
bash moviescraper_uninstall.sh       # 卸载
```

细节见 [`bua/打包说明.txt`](bua/打包说明.txt) 与 [`bua/moviescraper/README.md`](bua/moviescraper/README.md)。

### 方式二：手动部署

```sh
sh deploy/install.sh              # 同方式一，直接跑项目自带的安装器
sh deploy/install.sh --no-players
sh deploy/uninstall.sh
```

---

## ★ 配置与 API key（重要）★

**本仓库不含任何 API key。** 安装后到 **F1 菜单 →「电影刮削」→ 设置** 里填自己的凭据：

| 项 | 说明 |
| --- | --- |
| 刮削核心 | 默认 **豆瓣**（国内直连即可）；切 TMDB 需要 API Key 或 v4 Read Token（被墙时填代理） |
| 字幕 · assrt | 只需 32 位 `assrtToken`（assrt.net 用户面板） |
| 字幕 · OpenSubtitles | 需要 API Key + 账号 + 密码 |
| 代理 | 例 `socks5://127.0.0.1:10808`，留空即直连 |

以下文件已在 [`.gitignore`](.gitignore) 中排除，**请不要提交**（提交 = 泄漏）：

```
config.json                                # 本机开发用
deploy/payload/system/movies/scraper.json  # 便携包预填配置
deploy/windows/config.json                 # Windows 端配置
```

安装器会从 `scraper.example.json`（空 key）生成待填配置；若机器上已有 `scraper.json`，则**不会覆盖**你的 key。

---

## 构建

需要 **.NET 8 SDK**：

```sh
sh deploy/build.sh
# 产出 dist/{linux-x64,win-x64,win-gui}，并自动铺进 deploy/
```

产物：

| 目标 | 说明 |
| --- | --- |
| `dist/linux-x64/batoscrape` | Batocera 用（自包含单文件，约 64 MB） |
| `dist/win-x64/batoscrape.exe` | Windows 命令行 |
| `dist/win-gui/电影刮削器.exe` | Windows 图形界面 |

> 大二进制**不入库**（避免仓库膨胀），请用上面的命令重建，或从 Releases 下载。

---

## 目录结构

```
src/BatoceraMovieScraper/       核心：扫描 / 刮削（豆瓣、TMDB、nova）/ 字幕 / 渲染 gamelist
src/BatoceraMovieScraper.Win/   Windows 图形界面（WinForms）
deploy/                         便携部署：install.sh / uninstall.sh / payload / players
  payload/system/movies/        装到 Batocera 的内容（batoscrape + 脚本 + 配置模板 + 图标）
  payload/system/configs/emulationstation/es_systems_movies.cfg   ES「电影」分页定义
  players/                      Kodi / VLC 播放设置
bua/moviescraper/               BUA 风格附加组件（本地打包，含去 key 的 deploy 副本）
samples/                        样例输出（gamelist.xml / manifest.json）
docs/BUA-ADDON-NOTES.md         做 Batocera 附加组件的踩坑记录
```

---

## 做 Batocera 附加组件的经验

见 **[docs/BUA-ADDON-NOTES.md](docs/BUA-ADDON-NOTES.md)** —— 包含「BUA 菜单要在三处登记」
「ES/桌面以 root 运行，Electron/Chromium 类 flatpak 必须 `--no-sandbox`」「flatpak 需要 session bus」
「TUI 程序必须用 xterm 包」「虚拟机里有 render node ≠ 有 3D」等实测坑。

---

## License

**GPL-3.0**，见 [LICENSE](LICENSE)。

> 如需换成 MIT / Apache-2.0 等更宽松的许可，替换 `LICENSE` 并修改本节即可。
