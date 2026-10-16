# 做 Batocera 附加组件的踩坑记录

> 这些是在把 **影视刮削器** 和另一个 app（Welkin 音乐播放器）打包成
> [BUA](https://github.com/batocera-unofficial-addons/batocera-unofficial-addons) 风格附加组件时，
> 在 Batocera 43.1（VMware 虚拟机）上实测踩出来的。与具体应用无关，通用。

## 1. 新附加组件必须在 **三处** 登记，否则菜单里看不到

只把自己的 `<名字>/` 目录放进 BUA 仓库是不够的 —— 安装器的菜单里根本不会出现这个条目。
BUA 有两套入口，**各自都有一份清单**：

**a) 旧版 dialog 菜单** `app/batocera-unofficial-addons.sh`

```bash
apps=(          # ① 安装命令
  ["FIREFOX"]="curl -Ls .../firefox/firefox.sh | bash"
  ["YOURAPP"]="curl -Ls .../yourapp/yourapp.sh | bash"     # ← 加这里
)
descriptions=(  # ② 说明
  ["YOURAPP"]="一句话说明。"
)
categories=(    # ③ 分类（Games / Game Utilities / System Utilities / Developer Tools）
  ["System Utilities"]="... YOURAPP ..."
)
```

**b) 新版 Python 安装器** `app/bua_installerx86.py`（arm64 是另一个文件）

```python
"YOURAPP": bua("yourapp/yourapp.sh"),        # 命令
"YOURAPP": "一句话说明。",                    # 描述
"System Utilities": [ ..., "YOURAPP" ],      # 分类
CHANGELOG = """- Added YOURAPP - 一句话说明"""   # 惯例上补一行更新日志
```

> 只提供 x86_64 时：`bua_installerarm64.py` 不用改，但脚本里要**先检查架构**并友好退出。

## 2. ES / 桌面都以 **root** 运行 → Electron/Chromium 类 flatpak 必须 `--no-sandbox`

现象：`Running as root without --no-sandbox is not supported`（Heroic、Steam 等 Electron/CEF 应用）

- **环境变量无效**：`ELECTRON_DISABLE_SANDBOX=1` 绕不过 —— 那是命令行开关检查
- 必须传参数：`flatpak run <app-id> --no-sandbox`
- 菜单里那条 flatpak 应用是 Batocera 直接 `flatpak run <id>` 起的，**加不了参数**
  → 要么用 BUA 里那些非 flatpak 版本，要么自己写带参数的 Ports 脚本：

```sh
#!/bin/bash
export $(cat /proc/1/environ | tr '\0' '\n')
export DISPLAY="${DISPLAY:-:0.0}"
export XDG_RUNTIME_DIR="${XDG_RUNTIME_DIR:-/var/run}"
exec dbus-run-session -- flatpak run com.example.App --no-sandbox "$@" >> /userdata/system/logs/App.log 2>&1
```

## 3. flatpak 应用需要 session bus

现象：`zypak-helper: Failed to connect to session bus … set DBUS_SESSION_BUS_ADDRESS`
→ 启动器统一套 `dbus-run-session -- …`。

## 4. 从菜单启动的脚本 **没有终端** → dialog / TUI 必须用 xterm 包

现象：dialog 菜单从菜单点开"什么都不发生"（脚本 0.2 秒就退出）
→ 用 xterm 包起来，并在退出后 `read`，这样出错时能看到信息：

```sh
exec xterm -T "标题" -e bash -c "bash '$MENU'; echo; echo '按回车关闭'; read -r"
```

## 5. 虚拟机里「有 render node」≠「有 3D」

- `ls /dev/dri` 会看到 `card0` **和** `renderD128`，但活跃的 Vulkan ICD 可能只有 `lvp_icd.json`
  （lavapipe = Mesa 的**软件** Vulkan），`vulkaninfo` 显示 `deviceName = llvmpipe`
- 后果①：**DXVK/VKD3D 不可用**（Heroic 日志：`No GPU with Vulkan 1.1 support found, DXVK will not work`）
  → 想跑游戏只能走 WineD3D/OpenGL：`PROTON_USE_WINED3D=1`
- 后果②：WebKit 类的应用必须走软件路径，否则**整屏白屏**
  → **不要**按 `/dev/dri/renderD128` 是否存在去自动开硬件合成，改成显式开关

## 6. 改 `gamelist.xml` 别把 `xmlstarlet -L` 和重定向混用

```sh
# ✗ 会把 gamelist.xml 清空（-L 已就地写入，重定向又造了个空 tmp，最后 mv 覆盖回去）
xmlstarlet ed -P -L -s "/gameList" ... "$G" > "$G.tmp" && mv "$G.tmp" "$G"
```

→ 二选一：`xmlstarlet ed -P -L … "$G"`（**不要**重定向），
或 `xmlstarlet ed -P … "$G" > "$G.tmp" && mv "$G.tmp" "$G"`（**不要** `-L`）。

## 7. Batocera 不自带 `xmlstarlet`

它只在 BUA 的安装器里（`app/xmlstarlet` + `ln -sf` 到 `/usr/bin`）。
脚本单独跑时要兜底 —— Batocera 自带 python3，可以直接改 XML：

```sh
if command -v xmlstarlet >/dev/null 2>&1; then
    xmlstarlet ed -P -L -s "/gameList" -t elem -n game -v "" ... "$G"
else
    python3 - "$G" <<'PY'
import sys, xml.etree.ElementTree as ET
tree = ET.parse(sys.argv[1]); root = tree.getroot()
g = ET.SubElement(root, "game"); ET.SubElement(g, "path").text = "./App.sh"
tree.write(sys.argv[1], encoding="utf-8", xml_declaration=True)
PY
fi
```

## 8. 网络与脚本写法（国内）

- 设备直连 GitHub 常常只有 **几 KB/s**，`wget` 裸下必失败
  → 用 `curl -C -` 配重试；或**在 PC 上用 GitHub API 抓**再 scp 过去
- 大包换官方 CDN：例如 Firefox 在 Mozilla 上约 549 KB/s，比 GitHub 快约 100 倍
- `wget` **不支持 `file://`**（`curl` 支持）→ 本地测试起 `python3 -m http.server`
- 从 Windows 生成设备脚本要小心 **BOM**：`Set-Content -Encoding UTF8` 会加 BOM，
  导致 `#!/bin/bash` 失效（报 `No such file or directory`）→ 用 Python 以 utf-8 + `newline="\n"` 写
- 覆盖**正在运行**的 AppImage 会 `Text file busy` → 先停进程再换

## 9. 分发前务必自检：包里不能有任何 key

打包脚本 / 安装脚本里加一道闸：

```sh
for bad in "payload/system/movies/scraper.json" "windows/config.json"; do
    [ -f "$BUILD/$bad" ] && { echo "包内发现疑似含 key 的文件：$bad，已中止"; exit 1; }
done
```

并把真配置写进 `.gitignore`（`config.json`、`scraper.json`、`windows/config.json`），
推送前再用真值全文搜索一遍（**包括大二进制**）。
