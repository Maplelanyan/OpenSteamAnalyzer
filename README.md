# OpenSteamAnalyzer

![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)
![WPF](https://img.shields.io/badge/UI-WPF-5C2D91?logo=windows&logoColor=white)
![Platform](https://img.shields.io/badge/platform-Windows-blue)
![Language](https://img.shields.io/badge/language-C%23-239120?logo=csharp&logoColor=white)

OpenSteamAnalyzer 是一个基于 **WPF + .NET 8** 的 Steam 游戏库分析工具。它读取 Steam Web API 返回的公开账号资料和游戏库数据，在本地桌面仪表盘中展示账号信息、游戏时长统计、Top 游戏排行、游玩时长分布和可搜索的游戏列表。

项目重点是一个清晰、轻量的 Windows 桌面应用：使用 Prism 组织 MVVM 结构，使用 SQLite 做本地缓存，使用 LiveChartsCore 展示图表，并支持 Steam 个人资料装饰资源的展示。

## 功能

- 支持输入 SteamID64、`/profiles/{steamid}` 链接和 `/id/{vanity}` 自定义主页链接。
- 自动解析 Steam 自定义主页 ID。
- 读取 Steam 用户资料：昵称、头像、在线状态、等级、国家/地区、个人主页。
- 读取游戏库：游戏名称、AppId、总游玩时长、近两周游玩时长、最后游玩时间、游戏图标。
- 展示统计卡片：游戏总数、总时长、平均时长、未玩游戏数、近两周时长。
- 展示 Top 10 游戏柱状图和游玩时长分布饼图。
- 支持游戏列表搜索、筛选和排序。
- 支持 Steam API Key 本地保存，使用 Windows 当前用户 DPAPI 加密。
- 支持 `STEAM_API_KEY` 环境变量，且优先级高于本地保存的 Key。
- 使用 SQLite 缓存账号资料和游戏库，默认缓存有效期为 6 小时。
- 支持强制刷新，绕过本地缓存重新请求 Steam API。
- 支持静态/动态头像、头像框和个人资料背景展示。
- 动态头像和头像框通过 WebView2 渲染；动态资料背景下载到本地媒体缓存后播放。

## 截图

![Dashboard 0](./docs/images/dashboard0.png)

![Dashboard 1](./docs/images/dashboard1.png)

## 技术栈

| 技术 | 用途 |
|---|---|
| C# / .NET 8 | 应用主体和运行时 |
| WPF | Windows 桌面 UI |
| Prism.Wpf / Prism.DryIoc | MVVM、依赖注入和应用启动 |
| LiveChartsCore.SkiaSharpView.WPF | 图表展示 |
| Microsoft.Data.Sqlite | 本地缓存数据库 |
| Microsoft.Web.WebView2 | 动态头像和头像框渲染 |
| System.Security.Cryptography.ProtectedData | 本地 API Key 加密 |
| Steam Web API | Steam 账号和游戏库数据来源 |

## 系统要求

- Windows 10 / Windows 11
- .NET 8 SDK
- Microsoft Edge WebView2 Runtime
- 可访问 Steam Web API 和 Steam 静态资源域名的网络环境

开发推荐使用 Visual Studio、JetBrains Rider 或 VS Code + .NET SDK。

## 快速开始

```powershell
git clone https://github.com/Maplelanyan/OpenSteamAnalyzer.git
cd OpenSteamAnalyzer
dotnet restore .\OpenSteamAnalyzer\OpenSteamAnalyzer.csproj
dotnet run --project .\OpenSteamAnalyzer\OpenSteamAnalyzer.csproj
```

也可以直接用 Visual Studio 打开 `OpenSteamAnalyzer.slnx` 后运行 `OpenSteamAnalyzer` 项目。

## Steam API Key

应用需要 Steam Web API Key 才能读取账号和游戏库数据。Key 获取地址：

```text
https://steamcommunity.com/dev/apikey
```

配置方式一：在应用界面中输入 `Steam API Key`，点击 `保存 Key`。Key 会写入本地 `settings.json`，并使用 Windows 当前用户 DPAPI 加密。

配置方式二：使用环境变量：

```powershell
[Environment]::SetEnvironmentVariable("STEAM_API_KEY", "你的 Steam API Key", "User")
```

设置后重新启动应用。如果环境变量和本地保存的 Key 同时存在，应用优先使用 `STEAM_API_KEY`。

## 使用方式

1. 启动应用。
2. 输入 SteamID64 或 Steam 个人主页链接。
3. 配置 Steam API Key。
4. 点击 `分析`。
5. 查看账号资料、统计卡片、图表和游戏列表。
6. 需要重新请求 Steam API 时点击 `强制刷新`。

支持的输入示例：

```text
7656119xxxxxxxxxx
https://steamcommunity.com/profiles/7656119xxxxxxxxxx
https://steamcommunity.com/id/example
```

## 本地数据

应用数据默认保存到：

```text
%LOCALAPPDATA%\OpenSteamAnalyzer
```

常见文件和目录：

| 路径 | 说明 |
|---|---|
| `settings.json` | 保存最近输入的 Steam 账号和加密后的 API Key |
| `cache.db` | SQLite 缓存，保存账号资料和游戏库 |
| `media-cache/` | 动态背景等媒体资源缓存 |

`分析` 会优先使用 6 小时内的缓存；`强制刷新` 会重新请求 Steam API 并更新缓存。

## 项目结构

```text
OpenSteamAnalyzer/
|-- OpenSteamAnalyzer.slnx
|-- README.md
|-- docs/
|   `-- images/
`-- OpenSteamAnalyzer/
    |-- App.xaml
    |-- MainWindow.xaml
    |-- OpenSteamAnalyzer.csproj
    |-- Models/
    |-- Repositories/
    |-- Resources/
    |-- Services/
    |-- ViewModels/
    `-- Views/
```

主要职责：

| 目录 | 职责 |
|---|---|
| `Models` | Steam 资料、游戏、分析结果和缓存对象 |
| `Services` | Steam API 请求、SteamID 解析、游戏库分析 |
| `Repositories` | 本地设置和 SQLite 缓存 |
| `ViewModels` | 页面状态、命令、筛选排序、图表数据 |
| `Views` | WPF 页面和少量控件级交互 |
| `Resources` | 应用图标和全局样式 |

## 架构

```mermaid
flowchart LR
    View["DashboardView"] --> ViewModel["DashboardViewModel"]
    ViewModel --> Resolver["SteamIdResolverService"]
    ViewModel --> Api["SteamApiService"]
    ViewModel --> Analyzer["AnalyzerService"]
    ViewModel --> Cache["SteamCacheRepository"]
    ViewModel --> Settings["AppSettingsRepository"]
    Resolver --> Steam["Steam Web API"]
    Api --> Steam
    Cache --> Db[("cache.db")]
    Settings --> Json[("settings.json")]
```

## 使用的 Steam Web API

| API | 用途 |
|---|---|
| `ISteamUser/ResolveVanityURL` | 解析自定义主页 ID |
| `ISteamUser/GetPlayerSummaries` | 获取用户基础资料 |
| `IPlayerService/GetOwnedGames` | 获取游戏库 |
| `IPlayerService/GetRecentlyPlayedGames` | 获取近两周游玩 |
| `IPlayerService/GetSteamLevel` | 获取 Steam 等级 |
| `IPlayerService/GetProfileItemsEquipped` | 获取装备中的个人资料装饰 |
| `IPlayerService/GetAvatarFrame` | 获取头像框 fallback 数据 |
| `IPlayerService/GetAnimatedAvatar` | 获取动态头像 fallback 数据 |

## 常见问题

### 提示未配置 Steam API Key

在应用内保存 Key，或设置 `STEAM_API_KEY` 环境变量后重启应用。

### 游戏库为空或读取失败

常见原因包括目标账号游戏库未公开、API Key 无效、Steam API 限流、网络请求失败或 Steam 服务暂时不可用。

### 动态头像或头像框不显示

先确认系统安装了 WebView2 Runtime。也可能是 Steam 未返回对应装饰资源，或资源 URL 当前不可访问。

### 动态背景不显示或不动

动态背景会先下载到 `%LOCALAPPDATA%\OpenSteamAnalyzer\media-cache`，再由 WPF 媒体播放组件播放。若某些视频编码在当前系统上不可播放，会回退到静态背景。

### 数据没有更新

`分析` 默认复用 6 小时内缓存。需要立即请求最新数据时使用 `强制刷新`。

## 开发命令

```powershell
dotnet build .\OpenSteamAnalyzer\OpenSteamAnalyzer.csproj -c Debug
dotnet build .\OpenSteamAnalyzer\OpenSteamAnalyzer.csproj -c Release
dotnet publish .\OpenSteamAnalyzer\OpenSteamAnalyzer.csproj -c Release -r win-x64 --self-contained false -o .\publish
```

独立发布：

```powershell
dotnet publish .\OpenSteamAnalyzer\OpenSteamAnalyzer.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .\publish
```

## 维护说明

- View 只保留必要的控件级逻辑，例如密码框、滚轮转发、动态媒体展示。
- ViewModel 负责状态、命令、数据加载流程和列表筛选排序。
- Service 负责 Steam API、SteamID 解析和统计分析。
- Repository 负责本地持久化。
- 不要提交真实 Steam API Key。
- 不要把临时媒体缓存或本地数据库提交到仓库。

## 后续可改进方向

- 增加单元测试项目。
- 增加缓存清理入口。
- 增加 CSV / JSON 导出。
- 增加多账号对比。
- 增加更多图表和趋势分析。
- 增加应用内设置页。
- 增加 CI 和 Release 自动构建。

## License

本项目基于 MIT License 开源，详见 [LICENSE](LICENSE)。

## 免责声明

OpenSteamAnalyzer 不是 Steam 或 Valve 官方项目。本项目仅用于学习、研究和个人数据分析。Steam、Steam Web API 以及相关资源归 Valve Corporation 所有。使用本项目时请遵守 Steam Web API 的相关条款和限制。
