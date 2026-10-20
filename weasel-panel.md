# 小狼毫内置剪贴板面板 v0.3

这是**修改版 Weasel**，不是 Weasel Studio 设置中心。目标是把剪贴板历史做进输入法本身，而不是做独立托盘程序。

## 当前形态

- 源码：https://github.com/baipiao5555/weasel/tree/clipboard-panel
- 基于 `rime/weasel` master `82b50c9`
- 提交：`78d7aaa`
- 版本：`0.17.4.102.78d7aaa`
- 修改文件：`RimeWithWeasel/RimeWithWeasel.cpp`、`include/RimeWithWeasel.h`
- 构建：GitHub Actions MSBuild（CI lint 与编译均通过）
- 已确认 `WeaselServer.pdb` 包含 `_ShowClipboardPanel`、`_ToggleClipboardFavorite`、`ClipboardHistoryPath` 等面板符号

## 功能

- WeaselServer 监听系统剪贴板，保存最近 100 条文本，单条上限 256 KB
- `Ctrl+Shift+V` 打开候选窗剪贴板面板；面板打开时再按一次可关闭
- `Up` / `Down` 选择，`Enter` 写回剪贴板并发送 `Ctrl+V`
- 每页 8 条，`PageUp` / `PageDown` 翻页；鼠标悬停、点击、滚轮和翻页箭头在默认候选窗也生效（普通输入不受影响）
- `1`–`8` 直接粘贴当前页对应条目
- `Delete` 删除当前条目
- `Tab` 只看收藏，`Ctrl+D` 收藏/取消收藏；收藏项排在最前并带 `★ 收藏` 标记
- 输入 ASCII 字符过滤，`Backspace` 退格；查询为空时再按 `Backspace` 关闭
- `Esc` 关闭
- 打开面板时清空 Rime 当前组合，避免候选窗里残留正在输入的拼音
- 历史为空时，打开面板会自动读取一次当前剪贴板
- 历史：`%APPDATA%\Rime\weasel-clipboard-history.dat`
- 收藏：`%APPDATA%\Rime\weasel-clipboard-favorites.dat`

## 安装

1. 先备份 `%APPDATA%\Rime`（至少备份 `.custom.yaml`、`custom_phrase.txt`）。
2. 从 [Releases](https://github.com/baipiao5555/weasel-studio/releases/tag/weasel-panel-v0.3) 下载 `weasel-*.exe` 安装包。v0.2 保留作为回退版本。
3. 运行安装包。它会替换 Weasel 的二进制和 IME DLL，保留用户配置。
4. 重启 WeaselServer 或重新登录。
5. 在任意文本框里按 `Ctrl+Shift+V` 测试。

回滚：重新安装官方 [Weasel 0.17.4](https://github.com/rime/weasel/releases/tag/0.17.4) 或官方 Nightly。

## 已知限制

- 当前只支持**文本**；图片、文件、分类和独立大面板 UI 还没做。
- 复用的是候选窗，不是 fcitx5-android 那种独立面板。
- 搜索只支持 ASCII；中文搜索需要完整的文本输入/composition，当前未做。
- 粘贴通过 `SendInput` 发送 `Ctrl+V`；管理员权限应用可能拒绝。
- `Ctrl+Shift+V` 可能与部分应用的“粘贴为纯文本”快捷键冲突。
- 历史是明文存储，剪贴板里的敏感内容也会被记录。
- 安装包未签名，SmartScreen 可能提示“未知发布者”。
- 这是实验性 Weasel 分支，不是官方 Weasel；没有在本机替换正在使用的 Weasel 做运行时验证。

## 反馈时请附上

- 出问题的应用名称（浏览器、微信、Office、终端等）
- 是否能稳定复现
- `Ctrl+Shift+V` 没反应、面板不显示、翻页错位、粘贴失败，分别说明
- 如有崩溃，附上 `%APPDATA%\Rime` 下最新的日志

## 下一阶段

- 独立 `WeaselUI` 面板：搜索框、分类、图片缩略图、文件
- 配置项：历史上限、是否记录、快捷键、敏感内容过滤
- 与 Weasel Studio 的 SyncClipboard 同步共用历史存储
