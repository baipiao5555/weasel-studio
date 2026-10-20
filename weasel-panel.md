# 小狼毫内置剪贴板面板 v0.10

这是**修改版 Weasel**，不是 Weasel Studio 设置中心。目标是把剪贴板历史做进输入法本身，而不是做独立托盘程序。

## 当前形态

- 源码：https://github.com/baipiao5555/weasel/tree/clipboard-panel
- 基于 `rime/weasel` master `82b50c9`
- 提交：`19530bc`
- 版本：`0.17.4.119.19530bc`
- 修改文件：`RimeWithWeasel/RimeWithWeasel.cpp`、`include/RimeWithWeasel.h`
- 构建：GitHub Actions MSBuild（CI lint 与编译均通过）
- 已确认 `WeaselServer.pdb` 包含 `GetPngDimensions`、`FinalizeClipboardImageFile`、`ReadFileBytes`、`WriteBufferToFile` 等符号

## 功能

- WeaselServer 监听系统剪贴板，保存最近 100 条文本、文件列表和图片，单条文本上限 256 KB
- 复制文件后显示 `[文件] 文件名 等 N 个文件`；`Enter` 写回 `CF_HDROP` 并发送 `Ctrl+V`，支持文件粘贴的应用可以直接粘贴文件
- 复制截图/图片后显示 `[图片 宽×高]`；`Enter` 同时写回 `CF_DIB` 和 `PNG` 格式并发送 `Ctrl+V`
- 剪贴板里带原始 PNG 时直接保存原始数据，保留透明通道；否则从 `CF_BITMAP` / `CF_DIB` / `CF_DIBV5` 转 PNG
- 图片以 PNG 存在 `%APPDATA%\Rime\weasel-clipboard-images`，按内容哈希去重；最多保留 10 张，单边最大 4096，单张 PNG 最大 24 MB
- 图片粘贴后只跳过紧接着的一次图片剪贴板更新，避免回声重复；之后正常复制会继续记录
- 文件列表最多 100 个、路径总长 32K 字符；文本、文件、图片共用历史、收藏、搜索、翻页、删除
- `Ctrl+Shift+V` 打开候选窗剪贴板面板；面板打开时再按一次可关闭
- `Up` / `Down` 选择，`Enter` 写回剪贴板并发送 `Ctrl+V`
- 每页默认 8 条，`PageUp` / `PageDown` 循环翻页，`Home` / `End` 跳到首尾页
- `1`–`8` 直接粘贴当前页对应条目
- `Delete` 删除当前条目；`Ctrl+Shift+Delete` 连按两次清空全部历史和收藏，中间按其他键取消
- `Ctrl+U` 清空搜索词
- `Tab` 或 `Ctrl+F` 只看收藏，`Ctrl+D` 收藏/取消收藏；收藏项排在最前并带 `★ 收藏` 标记
- 输入 ASCII 字符过滤，`Backspace` 退格；查询为空时再按 `Backspace` 关闭
- `Esc` 关闭
- 标题显示当前筛选结果条数、页码和搜索词
- 打开面板时清空 Rime 当前组合，避免候选窗里残留正在输入的拼音
- 历史为空时，打开面板会自动读取一次当前剪贴板
- 面板打开期间复制的新内容，会在下一次按键时刷新出来
- 如果剪贴板被应用标记为 `ExcludeClipboardContentFromMonitorProcessing`，或 `CanIncludeInClipboardHistory` 为 0，则不会写入历史
- 历史和收藏先写 `.tmp` 再原子替换，写入失败不会破坏旧文件
- 历史总字符预算 32M，超出时淘汰最旧条目，避免大文本把内存撑爆
- 历史：`%APPDATA%\Rime\weasel-clipboard-history.dat`
- 收藏：`%APPDATA%\Rime\weasel-clipboard-favorites.dat`

## 配置

在 `%APPDATA%\Rime\weasel.custom.yaml` 里添加（没有就新建）：

```yaml
patch:
  clipboard/enabled: true
  clipboard/history_limit: 100
  clipboard/page_size: 8
  clipboard/max_text_length: 262144
  clipboard/hotkey: "Control+Shift+V"
```

- `enabled`：`false` 时完全不启动剪贴板监听
- `history_limit`：1–1000，默认 100
- `page_size`：1–8，默认 8；数字键只能覆盖 1–8
- `max_text_length`：1024–4194304，默认 262144
- `hotkey`：至少包含一个修饰键；支持 `Ctrl`/`Control`、`Shift`、`Alt`，加一个字母、数字或 `Space`/`Tab`/`Enter`
- 改完运行“重新部署”或重启 WeaselServer 生效
- 也可以用 Weasel Studio 0.5.0 的“内置剪贴板”页面编辑这些值，不需要手写 YAML

## 安装

1. 先备份 `%APPDATA%\Rime`（至少备份 `.custom.yaml`、`custom_phrase.txt`）。
2. 从 [Releases](https://github.com/baipiao5555/weasel-studio/releases/tag/weasel-panel-v0.10) 下载 `weasel-*.exe` 安装包。v0.9、v0.8、v0.7、v0.6、v0.5、v0.4 和 v0.2 保留作为回退版本；v0.3 的鼠标回调存在跨线程 UI 访问风险，不建议使用。
3. 运行安装包。它会替换 Weasel 的二进制和 IME DLL，保留用户配置。
4. 重启 WeaselServer 或重新登录。
5. 在任意文本框里按 `Ctrl+Shift+V` 测试。

回滚：重新安装官方 [Weasel 0.17.4](https://github.com/rime/weasel/releases/tag/0.17.4) 或官方 Nightly。

## 已知限制

- 当前支持**文本、文件列表和图片**；分类和独立大面板 UI 还没做。
- 文件粘贴依赖目标应用接受 `CF_HDROP`；只记录路径，文件移动或删除后粘贴会失败。
- 图片没有缩略图预览，候选窗只显示 `[图片 宽×高]`。
- 图片粘贴同时写 `CF_DIB` 和 `PNG`；支持 PNG 剪贴板的应用能保留透明通道，只读 `CF_DIB` 的应用仍可能丢透明通道。
- GIF 动画仍不支持，只会保存当前位图帧；部分特殊位图格式可能转换失败。
- 图片粘贴后的第一次图片剪贴板更新会被当作回声跳过；极快紧接着复制图片时可能漏一张。
- 图片最多 10 张、单边最大 4096、单张 PNG 最大 24 MB；超出会被跳过。
- 复用的是候选窗，不是 fcitx5-android 那种独立面板。
- 搜索只支持 ASCII；中文搜索需要完整的文本输入/composition，当前未做。
- 粘贴通过 `SendInput` 发送 `Ctrl+V`；管理员权限应用可能拒绝。
- `Ctrl+Shift+V` 可能与部分应用的“粘贴为纯文本”快捷键冲突；可在 `clipboard/hotkey` 里改。
- `Ctrl+Shift+Delete` 清空历史时会同时清空收藏。
- 内置剪贴板配置页需要 Weasel Studio 0.5.0 或更新版本；旧版仍需手改 `weasel.custom.yaml`。
- 历史总字符预算 32M，超过会淘汰最旧条目；单条上限 4M 不代表能同时保留很多条。
- 历史仍是明文存储；敏感过滤依赖应用主动设置 Windows 剪贴板排除标记，未标记的内容仍会被记录。
- 安装包未签名，SmartScreen 可能提示“未知发布者”。
- 这是实验性 Weasel 分支，不是官方 Weasel；没有在本机替换正在使用的 Weasel 做运行时验证。

## 反馈时请附上

- 出问题的应用名称（浏览器、微信、Office、终端等）
- 是否能稳定复现
- `Ctrl+Shift+V` 没反应、面板不显示、翻页错位、文本/文件/图片粘贴失败，分别说明
- 如有崩溃，附上 `%APPDATA%\Rime` 下最新的日志

## 下一阶段

- 独立 `WeaselUI` 面板：搜索框、分类、图片缩略图、文件
- 配置项：历史上限、是否记录、快捷键、敏感内容过滤
- 与 Weasel Studio 的 SyncClipboard 同步共用历史存储
