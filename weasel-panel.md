# 小狼毫内置剪贴板面板（文本原型）

这是**修改版 Weasel**，不是 Weasel Studio 设置中心。目标是把剪贴板历史做进输入法本身，而不是做独立托盘程序。

## 当前形态

- 源码：https://github.com/baipiao5555/weasel/tree/clipboard-panel
- 基于 `rime/weasel` master `82b50c9`
- 提交：`4510d1d`
- 修改文件：`RimeWithWeasel/RimeWithWeasel.cpp`、`include/RimeWithWeasel.h`
- 构建：GitHub Actions MSBuild（CI lint 与编译均通过）

## 功能

- WeaselServer 监听系统剪贴板，保存最近 100 条文本
- `Ctrl+Shift+V` 打开候选窗剪贴板面板
- `Up` / `Down` 选择
- `Enter` 把选中条目写回剪贴板并发送 `Ctrl+V`
- `Esc` 关闭
- `Delete` 删除当前条目
- 输入 ASCII 字符过滤，`Backspace` 退格
- 历史保存在 `%APPDATA%\Rime\weasel-clipboard-history.dat`

## 安装

1. 先备份 `%APPDATA%\Rime`（至少备份 `.custom.yaml`、`custom_phrase.txt`）。
2. 从 [Releases](https://github.com/baipiao5555/weasel-studio/releases/tag/weasel-panel-v0.1) 下载 `weasel-*.exe` 安装包。
3. 运行安装包。它会替换 Weasel 的二进制和 IME DLL，保留用户配置。
4. 重启 WeaselServer 或重新登录。
5. 在任意文本框里按 `Ctrl+Shift+V` 测试。

回滚：重新安装官方 [Weasel 0.17.4](https://github.com/rime/weasel/releases/tag/0.17.4) 或官方 Nightly。

## 限制

- 当前只支持**文本**；图片、文件、收藏、分类、完整面板 UI 是下一阶段。
- 复用的是候选窗，不是 fcitx5-android 那种独立大面板。
- 搜索只支持 ASCII；中文搜索需要完整的文本输入/composition，当前未做。
- 粘贴通过 `SendInput` 发送 `Ctrl+V`；管理员权限应用可能拒绝。
- `Ctrl+Shift+V` 可能与部分应用的“粘贴为纯文本”快捷键冲突。
- 历史是明文存储，剪贴板里的敏感内容也会被记录。
- 安装包未签名，SmartScreen 可能提示“未知发布者”。
- 这是实验性 Weasel 分支，不是官方 Weasel。

## 下一阶段

- 自定义 `WeaselUI` 面板：列表、搜索框、分类、收藏、删除、图片缩略图、文件
- 配置项：历史上限、是否记录、快捷键、敏感内容过滤
- 与 Weasel Studio 的 SyncClipboard 同步共用历史存储
