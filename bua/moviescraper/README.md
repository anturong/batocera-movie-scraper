# 电影刮削器 · Batocera 附加组件

把「电影刮削器」装进 Batocera：EmulationStation 会多出一个「电影」分页，F1 菜单里会有
「电影刮削」设置入口；可选一并把 Kodi / VLC 的播放设置配好。

## 推荐：一键安装 / 更新

本目录是 BUA 风格的兼容入口，真正的安装逻辑只有一份：仓库根目录的 `install.sh`。
在 Batocera 上以 root 运行：

```sh
curl -fsSL https://ghfast.top/https://raw.githubusercontent.com/anturong/batocera-movie-scraper/main/install.sh | bash
```

以后更新：

```sh
bash /userdata/system/movies/install.sh --check
bash /userdata/system/movies/install.sh
```

## 离线安装（本目录）

把整个 `moviescraper/` 目录拷到 Batocera（推荐 `/userdata/system/add-ons/moviescraper/`），
然后在 root 下执行：

```sh
bash moviescraper.sh                 # 全装（含 Kodi / VLC 播放器设置）
bash moviescraper.sh --no-players    # 只装刮削器与「电影」分页
```

`moviescraper.sh` 会优先调用仓库里的 `install.sh`；不在仓库里时自动下载最新安装器。
卸载同样走统一入口：

```sh
bash moviescraper_uninstall.sh
# 或
bash /userdata/system/movies/install.sh --uninstall
```

## ★ 关于 API key（重要）★

**这个包里不含任何 key。** 安装完成后，去 F1 菜单 →「电影刮削」→ 设置里填自己的凭据：

| 项 | 说明 |
| --- | --- |
| 刮削核心 | 默认 **豆瓣**（国内直连即可）；切 **TMDB** 才需要 API Key 或 v4 Read Token，被墙时填代理 |
| 字幕 · 射手网(伪) | 只需 32 位 `assrtToken`（assrt.net 用户面板） |
| 字幕 · OpenSubtitles | 需要 API Key + 账号 + 密码 |
| 代理 | 例 `socks5://127.0.0.1:10808`，留空即直连 |

如果你机器上**已经有** `/userdata/system/movies/scraper.json`，安装器检测到就不会覆盖 —— 你的 key 不会丢。

## 打包时被剔除的内容（请自己留着，不要跟着分发）

| 剔除项 | 原因 |
| --- | --- |
| `payload/system/movies/scraper.json` | 含 TMDB key + v4 read token + assrt Token + OpenSubtitles 账号密码 + 代理地址 |
| `config.json`、`windows/config.json` | 同上 |
| `windows/batoscrape.exe`、`windows/电影刮削器.exe` | Windows 专用，Batocera 用不到（约 130 MB） |
| `安装步骤.md` 里的示例 SMB 地址 | 含局域网 IP，已改成 `<你的共享主机IP>` |

包内保留的是 `scraper.example.json` / `config.example.json`（**空 key**），
安装器会用它生成待填写的配置。

## 状态

- 上游仓库：<https://github.com/anturong/batocera-movie-scraper>（公开，不含 key）。
- 发布包由 `tools/make_release.py` 白名单生成并扫描凭据，`scraper.json` / `config.json` / `.exe` 不会进入产物。
- 若日后要提交到 BUA：请再次确认上面那几个敏感文件依然不在包内，
  并按 BUA 的约定把安装命令/描述/分类登记进 `app/batocera-unofficial-addons.sh`。
