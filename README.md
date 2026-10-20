# 小狼毫设置中心（Weasel Studio）

一个面向 Windows 小狼毫（Rime / Weasel）的图形化配置面板。目标是让常用设置不再需要手改 YAML，同时保留 Rime 原生的 `*.custom.yaml` 补丁机制。

当前版本（0.4.0）是一个可运行的 MVP，已在本机 Weasel 0.17.4 + .NET 10 上验证。

## 下载与安装

- **安装版**：从 [Releases](https://github.com/baipiao5555/weasel-studio/releases/latest) 下载 `WeaselStudio-Setup-0.4.0.exe`。按当前用户安装，不需要管理员权限；默认安装到 `%LOCALAPPDATA%\Programs\WeaselStudio`，会创建开始菜单快捷方式，桌面快捷方式可选。卸载不会删除 `%APPDATA%\Rime` 里的配置和备份。
- **便携版**：下载 `WeaselStudio-Portable-0.4.0.exe`，双击直接运行，不写安装目录和注册表。
- 系统要求：Windows 10 / 11 x64。

## 功能

- **概览**：自动探测小狼毫安装目录、用户目录、部署器和版本；显示当前方案、方案数量、自定义文件数量和最近备份。
- **输入方案**：扫描用户目录下的 `*.schema.yaml`，勾选启用、调整顺序、设为默认，保存到 `default.custom.yaml` 的 `patch/schema_list`。
- **输入习惯**：按方案编辑 switches（中英文、简繁、全角/半角、标点、Emoji 等）和每页候选数。
- **模糊音**：按方案启用平翘舌、鼻边音、唇齿音、前后鼻音、ian/iang 等常见组合；保存会重建 `speller/algebra` 列表并保留原有规则。
- **自定义短语**：用表格维护 `custom_phrase.txt`，支持短语/编码/权重，保存前自动备份。
- **剪贴板同步**：用 SyncClipboard 协议同步文本剪贴板；支持托盘常驻、开机启动、DPAPI 保存密码、ETag/哈希防回环。默认拒绝本机/私有地址，局域网服务器需勾选“允许私有地址”。
- **外观与候选窗**：内置 Windows 11 浅色/深色、微信输入法风、极简代码风等主题预设；也可细调配色、方向、字体、圆角、边框、边距、间距、阴影，右侧带实时候选窗预览；支持主题 JSON 导入/导出。
- **高级 YAML**：直接编辑 `default.custom.yaml`、`weasel.custom.yaml` 等文件，保存前校验语法。
- **一键部署**：调用 `WeaselDeployer.exe /deploy`，把配置应用到小狼毫。
- **安全写入**：只修改 `patch` 节点，保留注释和原有顺序；每次写入前自动备份，写入后校验 YAML。

## 剪贴板同步

- 协议：SyncClipboard / WebDAV 兼容服务端，文本通过 `SyncClipboard.json` 双向同步。
- 运行方式：设置中心常驻托盘；可设置开机启动；关闭窗口默认最小化到托盘。
- 安全：密码用 Windows DPAPI 加密保存在 `%APPDATA%\WeaselStudio\sync-settings.json`；默认拒绝本机、环回和私有地址，局域网服务器需显式勾选“允许私有地址”。
- 限制：当前只同步文本，图片/文件后续。

## 安全设计

- 不覆盖 `weasel.yaml`、`default.yaml` 或 `*.schema.yaml` 原文件，只写 `*.custom.yaml`。
- 写入前把原文件复制到 `%APPDATA%\Rime\.weasel-studio-backups`。
- 写入流程是「临时文件 → YAML 校验 → 替换原文件」，校验失败不会落盘。
- 补丁编辑是按行进行的，不会把整个文件重新序列化，因此用户注释、引号风格和键顺序不会被抹掉。
- 部署通过 `WeaselDeployer.exe /deploy` 完成，程序本身不需要管理员权限。

## 环境要求

- Windows 10 / 11。
- 已安装小狼毫（Weasel），0.16 / 0.17 系列均可。
- 构建需要 .NET 10 SDK；如果使用自包含发布，目标机器不需要预装 .NET。

程序通过注册表定位小狼毫：

- `HKCU\Software\Rime\Weasel\RimeUserDir`
- `HKLM\SOFTWARE\WOW6432Node\Rime\Weasel\WeaselRoot`

找不到时会回退到 `%APPDATA%\Rime` 和 `C:\Program Files\Rime\weasel-*`。

## 构建与运行

```powershell
dotnet build WeaselStudio.sln -c Release
dotnet run --project WeaselStudio.csproj -c Release
```

生成的可执行文件位于：

```text
bin\Release\net10.0-windows\WeaselStudio.exe
```

## 发布单文件 exe

```powershell
dotnet publish WeaselStudio.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

输出在 `publish\WeaselStudio.exe`。也可直接运行仓库根目录的 `publish-win-x64.bat`。

## 构建安装器

先完成上面的单文件发布，然后用 NSIS 3.13+ 编译：

```powershell
$env:NSIS_EXE = "C:\path\to\makensis.exe"
installer\build-installer.bat
```

输出 `publish\WeaselStudio-Setup-0.4.0.exe`。安装脚本在 `installer\WeaselStudio.nsi`，按用户安装、不需要管理员权限。

## 测试

`tests/CoreTests` 是一个不依赖 UI 的控制台测试，覆盖：

- 注册表/路径探测；
- 方案列表读取；
- 样式与配色方案读取；
- `*.custom.yaml` 行级补丁编辑；
- 注释保留、嵌套键更新、序列替换、删除键；
- 写入后 YAML 可重新解析。

运行：

```powershell
dotnet run --project tests/CoreTests/CoreTests.csproj -c Release
```

当前本机结果：`62/62 checks passed`。

## 目录结构

```text
weasel-studio/
  App.xaml / MainWindow.xaml       应用入口和主框架
  Themes/Styles.xaml               颜色、按钮、导航、输入框等样式
  Models/                          SchemaInfo、ColorSchemeInfo、StyleSettings 等
  Services/
    RimeEnvironment.cs             注册表与安装路径探测
    ConfigService.cs               读取方案、样式、配色，写入补丁
    PatchFile.cs                   行级 YAML 补丁编辑器
    SchemaMetadataReader.cs        容错的方案元数据读取
    YamlConfig.cs                  YamlDotNet 读取封装
    BackupService.cs               自动备份
    DeployService.cs               调用 WeaselDeployer /deploy
    ShellService.cs                打开目录（ShellExecute）
  ViewModels/                      各页面 + 主窗口 ViewModel
  Views/                           各页面的 XAML
  Models/FuzzyRuleGroup.cs         模糊音组合定义
  Models/ThemeFile.cs              主题导入/导出 JSON 模型
  Services/FuzzyPinyinService.cs   algebra 读取、合并与写入
  Assets/                          应用图标
  installer/                       NSIS 安装脚本
  tests/CoreTests/                 控制台测试
```

## 已知限制

- 常见模糊音和主题导入/导出已支持；`app_options`、更细的自定义 algebra 仍可先用「高级 YAML」。
- 词典管理入口暂时隐藏：当前环境里 `WeaselDeployer.exe /dict` 会挂起（重启算法服务后仍复现），代码保留，等改成控制台包装或直接读取 userdb 后再开放。
- 支持 `--user-dir <目录>` 或环境变量 `WEASEL_STUDIO_USER_DIR` 进入测试模式：所有读写只作用于指定目录，部署会被禁用，适合在配置副本上试改。
- 候选窗预览是近似渲染，最终效果以部署后的小狼毫为准。
- `font_face` 在 Rime 中支持逗号分隔的字体回退链；界面直接编辑整条字符串，选择单个字体时会覆盖整条链。
- 只管理用户目录下的配置，不直接改共享目录里的方案文件。
- 剪贴板同步当前只支持文本；图片/文件后续版本加入。需要设置中心保持运行（可关闭到托盘）。

## 相关项目

如果这个 MVP 的覆盖范围不够，也可以参考这些更完整的同类工具：

- [WeaselTune](https://github.com/harry10086/WeaselTune)：Tauri + React，面向小狼毫，包含模糊音、自定义短语、皮肤预览等。
- [RimeVision](https://github.com/franvz9/Rime_vision)：Tauri + Rust，跨平台 Rime 可视化配置。
- 小狼毫自带的 `WeaselDeployer.exe`：方案切换、外观、词典和同步。

本项目不隶属于 Rime / Weasel 官方。
