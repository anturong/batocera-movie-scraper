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

## 一键安装到 Batocera

在 Batocera 上以 **root** 运行（国内走 ghfast 镜像，直连也可把前缀去掉）：

```sh
curl -fsSL https://ghfast.top/https://raw.githubusercontent.com/anturong/batocera-movie-scraper/main/install.sh | bash
```

一条命令会完成：

- 下载最新 Release（`moviescraper.tar.gz`）并校验 sha256
- 安装刮削器二进制与脚本到 `/userdata/system/movies/`
- 写入 EmulationStation「电影」分页
- 登记 F1 菜单：**电影刮削 / Kodi / VLC**，以及 Ports 里的「电影刮削」
- 配好 Kodi 中文界面、VLC 中文字幕字体（`--no-players` 可跳过）
- 生成空配置（**不会写入任何 key**）
- 把安装器保存到 `/userdata/system/movies/install.sh`

### 更新 / 检查 / 卸载

```sh
bash /userdata/system/movies/install.sh --check     # 只看版本
bash /userdata/system/movies/install.sh             # 有新版才下载更新
bash /userdata/system/movies/install.sh --force     # 强制重装当前版
bash /userdata/system/movies/install.sh --uninstall # 卸载（保留 scraper.json）
bash /userdata/system/movies/install.sh --uninstall --purge-config
```

| 参数 | 作用 |
| --- | --- |
| `--check` | 显示已安装 / 最新版本，不安装 |
| `--force` | 版本相同也重新下载安装 |
| `--no-players` | 跳过 Kodi / VLC 的播放设置 |
| `--uninstall` | 卸载程序、菜单项与 ES 分页 |
| `--purge-config` | 卸载时连 `scraper.json`（含 key）一起删 |
| `--yes` | 卸载不询问 |

更新只替换程序文件，**不动** `/userdata/system/movies/scraper.json` 和影片库。

### 离线 / 本地仓库安装

在仓库目录里直接跑（使用当前 `deploy/` 里的文件）：

```sh
bash install.sh --no-players
```

或解压 Release 后运行包里的 `install.sh`。`sh deploy/install.sh` 仍可用，是同一个安装器的快捷入口。

### BUA 附加组件（可选）

[`bua/moviescraper/`](bua/moviescraper/) 保留 BUA 风格的兼容入口，
`moviescraper.sh` 实际调用的仍是同一个 `install.sh`。

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
deploy/payload/system/movies/scraper.json  # 本机开发用（含 key，安装器也不会复制它）
deploy/windows/config.json                 # Windows 端配置
```

安装器会从 `scraper.example.json`（空 key）生成待填配置；若机器上已有 `scraper.json`，则**不会覆盖**你的 key。

发布包由 [`tools/make_release.py`](tools/make_release.py) 按白名单生成，打包后会扫描一遍：
`scraper.json`、`config.json`、Windows `.exe` 不会进入产物；示例配置里的凭据字段非空则直接打包失败。

---

## 构建与发布

本地构建需要 **.NET 8 SDK**：

```sh
sh deploy/build.sh
# 产出 dist/{linux-x64,win-x64,win-gui}，并自动铺进 deploy/
```

打 Batocera 安装包（白名单 + key 扫描）：

```sh
python tools/make_release.py
# 产出 dist-release/{moviescraper.tar.gz,moviescraper.version,sha256sums.txt}
```

发版：改 `VERSION`，提交后打同名 tag（`VERSION=2026.10.25.1` → `v2026.10.25.1`）。
GitHub Actions 会用 .NET 8 现场编译 linux-x64 二进制、打包并上传三个资产。

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
install.sh                      一键安装 / 更新 / 卸载（唯一逻辑入口）
VERSION                         当前版本（发版时与 tag 一致）
deploy/                         安装载荷：payload（二进制 + 脚本 + 配置模板 + 图标）
  payload/system/movies/          装到 /userdata/system/movies
  payload/system/configs/emulationstation/es_systems_movies.cfg   ES「电影」分页定义
  payload/system/.local/share/applications/*.desktop              F1 菜单项
  players/                        Kodi / VLC 播放设置
  lib/                            ES 配置合并/移除
tools/make_release.py           白名单打包 + key 扫描
.github/workflows/release.yml   tag 发版：编译 linux-x64 → 打包 → Release
src/BatoceraMovieScraper/       核心：扫描 / 刮削（豆瓣、TMDB、nova）/ 字幕 / 渲染 gamelist
src/BatoceraMovieScraper.Win/   Windows 图形界面（WinForms）
bua/moviescraper/               BUA 风格兼容入口（调用同一个 install.sh）
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
