# 电影刮削 · 移植布置说明

部署件清单 + 每件的**落盘位置与机制**（安装步骤见 `README.md`）。
所有路径都在 `/userdata` 下（root 家 = `/userdata/system`）；`/` 是 tmpfs 覆盖层，
**写在 `/userdata` 之外的东西重启即失**，所以部署件一律落 `/userdata`。

## 一、落盘位置总览

| 部署件 | 落盘路径 | 作用 |
|---|---|---|
| 刮削器二进制 | `/userdata/system/movies/batoscrape` | .NET8 自包含单文件：扫描→匹配→海报→字幕 |
| 影片分发器 | `/userdata/system/movies/play.sh` | ES 选中项 → 按核心名起 VLC / mpv |
| 刮削入口 | `/userdata/system/movies/scrape-gui.sh` | F1 唯一入口（设置＋刮削二合一） |
| 设置表单 | `/userdata/system/movies/settings.sh` | YAD 两段式设置，写回 `scraper.json` |
| 配置读写桥 | `/userdata/system/movies/lib/config_io.py` | shell ↔ JSON（13 段契约） |
| 配置 | `/userdata/system/movies/scraper.json` | 唯一真源配置 |
| 图标 | `/userdata/system/movies/icons/scraper.png` | 菜单 / 窗口图标 |
| 版本 / 更新入口 | `/userdata/system/movies/{VERSION,install.sh}` | 记录版本；以后一键更新 |
| ES 电影分页 | `/userdata/system/configs/emulationstation/es_systems_movies.cfg` | 注册 `movies` 系统 |
| F1 快捷项 | `/userdata/system/.local/share/applications/{batoscrape,kodi,vlc}.desktop` | F1 启动器 |
| Ports 入口 | `/userdata/roms/ports/电影刮削.sh` | ES Ports 里启动设置界面 |
| VLC 配置 | `/userdata/system/.config/vlc/vlcrc` | 中文字幕字体 |
| Kodi 配置 | `/userdata/system/.kodi/userdata/{guisettings.xml,sources.xml}` | 中文界面 + 影片来源 |

## 二、电影分页（EmulationStation 叠加）

- 文件名**必须**是 `es_systems_<名>.cfg`（普通叠加合并语义）。**不能**用 `es_systems_custom.cfg`
  ——那是「整份替换」，会顶掉全部内置系统（实测只剩 电影 / All Games / Favorites）。
- 内容：`name=movies`、`fullname=电影`、`path=/userdata/roms/movies`、
  `command=/userdata/system/movies/play.sh %ROM% %CORE%`。
- **默认核心 = vlc**：`<emulator name="vlc"><core default="true">vlc</core></emulator>`；
  mpv 为非默认备选（`default="false"`）。
- 安装走 `lib/es_merge.py`（合并去重），卸载走 `lib/es_remove.py`（只摘 `movies` 系统）。
- 改完需**重启 ES（或整机）**才出现分页。

## 三、影片分发器 `play.sh` 与 VLC 退出机制

`play.sh <rom> <core>`：

```sh
vlc) exec env QT_QPA_PLATFORM=xcb /usr/bin/vlc --fullscreen --no-video-title-show --vout xcb_x11 "$rom" ;;
*)   exec /usr/bin/mpv --fullscreen --really-quiet "$rom" ;;
```

**VLC 退出机制（关键，别改回去）**

- 最早用 `--intf dummy`（无界面）→ **没有任何窗口 / 按键能关掉它**，ES 只能干等；
  「退出电影」后 VLC 仍在后台跑。
- 改用 **Qt 界面**：有窗口可关，也可 **Ctrl+Q** 退出。
- **强制 `QT_QPA_PLATFORM=xcb` + `--vout xcb_x11`**：VM / 软件 GL 下 Qt 默认走 EGL 会
  `abort`，强制 X11 平台即可正常起。

## 四、F1 菜单快捷启动项（3 个 `.desktop`）

装在 `/userdata/system/.local/share/applications/`：

| .desktop | Name[zh_CN] | Exec |
|---|---|---|
| `batoscrape.desktop` | 电影刮削 | `/userdata/system/movies/scrape-gui.sh` |
| `vlc.desktop` | VLC 播放器 | `env QT_QPA_PLATFORM=xcb /usr/bin/vlc` |
| `kodi.desktop` | Kodi 媒体中心 | `/usr/bin/kodi` |

- **刮削入口只有 `batoscrape.desktop` 一个**（设置＋刮削二合一）；旧的单独「刮削设置」项
  （`scraper-settings.desktop`）已废弃，安装时会清除。
- `vlc.desktop` 的 Exec 同样**强制 `QT_QPA_PLATFORM=xcb`**（与 `play.sh` 同因：避免 EGL abort）。
- 三者都带 `Categories=…;batocera.linux;` 才会进 F1 菜单。
- `batoscrape.desktop` 每次安装/更新都会刷新；`kodi.desktop` / `vlc.desktop`
  已存在时不覆盖，避免动到用户自己的菜单项。

### 刮削入口 `scrape-gui.sh` 要点

- Batocera 默认 locale 是 **POSIX（非 UTF-8）**，**GTK 会拒绝含中文的 argv** →
  脚本首行 `export LANG=zh_CN.utf8 LC_ALL=zh_CN.utf8`。
- 流程：当前核心要 key 又没填 → 先弹 `settings.sh`；否则主菜单循环
  （开始刮削 / 设置… / 退出）；刮削时跑 `batoscrape` 子进程 +
  `yad --text-info --tail` 实时日志，`wait` 拿退出码。
- 日志走 `/tmp/batoscrape_menu.log`（ramdisk，重启即失，仅当次实时看）。

## 五、VLC 中文

- `players/files/vlcrc` → `/userdata/system/.config/vlc/vlcrc`：
  `freetype-font=Noto Sans SC`、`freetype-rel-fontsize=16`
  （用系统自带 **Noto Sans SC**，免中文字幕显示成方块）。
- 已存在 `vlcrc` 则**不覆盖**（保留用户配置）。
- 桌面项 Exec 已强制 `QT_QPA_PLATFORM=xcb`。

## 六、Kodi 中文 + 指向第三分区

`players/setup-kodi.sh` → `players/lib/set_kodi.py`（**幂等，改动前备份 `*.bak-<时间戳>`**）：

- **界面简体中文**：`guisettings.xml` →
  `locale.language = resource.language.zh_cn`、`lookandfeel.font = Arial`
  （Arial 有 CJK 回退，Kodi 自带）。
- **影片来源**：`sources.xml` 的 `<video>` 下插入

  ```xml
  <source>
      <name>电影</name>
      <path pathversion="1">/media/sda3/roms/movies/</path>
      <allowsharing>true</allowsharing>
  </source>
  ```

- 来源默认直指**第三分区** `/media/sda3/roms/movies/`；可用环境变量覆盖：
  `MOVIE_SRC=/userdata/roms/movies/ sh players/setup-kodi.sh`。
- **Kodi 未初始化（`.kodi/userdata` 不存在）则跳过**——必须先启动一次 Kodi 再跑本脚本。

## 七、第三分区（影片库落点）

- 影片**实体放第三分区**，如 `/media/sda3/roms/movies`；**别放 `sda2` 上的小系统分区**
  （很快写满）。
- ES 侧 `path` 用 `/userdata/roms/movies`（mergerfs 合成视图，实体会落到第三分区）；
  Kodi 影片来源则**直指** `/media/sda3/roms/movies/`。

## 八、安装 / 卸载

- `install.sh`（5 步，幂等）：建目录 → 铺刮削器与脚本 → 配置（已有 `scraper.json` 不覆盖）
  → 注册电影分页（并清理旧的 `es_systems_custom.cfg`）→ F1 项（并清掉旧 `scraper-settings.desktop`）；
  末段 `players/setup-kodi.sh` + `setup-vlc.sh`（`--no-players` 可整段跳过）。
- `uninstall.sh`：删刮削器文件与 F1 项、从 `es_systems_movies.cfg` 摘 `movies`；
  **不动影片库 / 海报，也不动 Kodi / VLC 设置**；`--purge-config` 才连 `scraper.json`
  （含你的 key）一起删。
