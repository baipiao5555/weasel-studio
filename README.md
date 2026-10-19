# 小狼毫设置中心（Weasel Studio）

一个面向 Windows 小狼毫（Rime / Weasel）的图形化配置面板。目标是让常用设置不再需要手改 YAML，同时保留 Rime 原生的 `*.custom.yaml` 补丁机制。

当前版本是一个可运行的 MVP，已在本机 Weasel 0.17.4 + .NET 10 上验证。

## 功能

- **概览**：自动探测小狼毫安装目录、用户目录、部署器和版本；显示当前方案、方案数量、自定义文件数量和最近备份。
- **输入方案**：扫描用户目录下的 `*.schema.yaml`，勾选启用、调整顺序、设为默认，保存到 `default.custom.yaml` 的 `patch/schema_list`。
- **外观与候选窗**：可视化编辑配色方案、候选方向、字体/字号、行内预编辑、圆角、边框、边距、间距、阴影等；右侧带实时候选窗预览。
- **高级 YAML**：直接编辑 `default.custom.yaml`、`weasel.custom.yaml` 等文件，保存前校验语法。
- **一键部署**：调用 `WeaselDeployer.exe /deploy`，把配置应用到小狼毫。
- **安全写入**：只修改 `patch` 节点，保留注释和原有顺序；每次写入前自动备份，写入后校验 YAML。

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

当前本机结果：`23/23 checks passed`。

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
  ViewModels/                      四个页面 + 主窗口 ViewModel
  Views/                           四个页面的 XAML
  tests/CoreTests/                 控制台测试
```

## 已知限制

- 这是 MVP，优先覆盖高频设置；模糊音、标点、Emoji、`app_options`、词典管理、主题编辑器等还未做成可视化页面，可以先用「高级 YAML」。
- 候选窗预览是近似渲染，最终效果以部署后的小狼毫为准。
- `font_face` 在 Rime 中支持逗号分隔的字体回退链；界面直接编辑整条字符串，选择单个字体时会覆盖整条链。
- 只管理用户目录下的配置，不直接改共享目录里的方案文件。
- 未包含剪贴板同步；后续若要接 SyncClipboard，建议在独立的托盘程序或 Weasel 前端里做，而不是塞进 librime 核心。

## 相关项目

如果这个 MVP 的覆盖范围不够，也可以参考这些更完整的同类工具：

- [WeaselTune](https://github.com/harry10086/WeaselTune)：Tauri + React，面向小狼毫，包含模糊音、自定义短语、皮肤预览等。
- [RimeVision](https://github.com/franvz9/Rime_vision)：Tauri + Rust，跨平台 Rime 可视化配置。
- 小狼毫自带的 `WeaselDeployer.exe`：方案切换、外观、词典和同步。

本项目不隶属于 Rime / Weasel 官方。
