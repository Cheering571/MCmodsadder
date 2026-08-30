# McModsAdder · MC Mod 批量添加工具

换整合包时，一键补上你常用的 mod。自动识别实例（游戏版本 / 加载器 / 已装 mod），按配置表从 Modrinth 下载匹配版本并补装缺失的 mod（含必需依赖）。

## 功能

- **实例识别**：自动扫描 `.minecraft/versions/` 版本隔离实例（兼容 PCL / HMCL），支持手动选择整合包目录；解析实例 JSON 判定 MC 版本与加载器（Fabric / Forge / Quilt / NeoForge）。
- **已装 mod 识别**：jar 的 sha1 哈希批量匹配 Modrinth（重命名 jar 也能识别），未命中解析 `fabric.mod.json` / `mods.toml` / `quilt.mod.json` / `mcmod.info` 兜底。
- **配置表**：通过 Modrinth 搜索添加 mod，只记录项目（不含版本/加载器），一张表复用于任意整合包；支持多配置表与 JSON 导入导出。
- **对比视图**：逐项显示 已安装 / 缺失 / 无可用版本。
- **一键补装**：按实例版本+加载器下载最新匹配文件，BFS 自动补装必需依赖；下载校验 sha1，写入前备份旧文件，并发数可调。
- **设置**：Modrinth 官方 / MCIM 国内镜像切换、并发数、备份开关、依赖开关。

## 技术栈

C# / .NET 8 / WPF / WPF-UI (Fluent Design 深色 Mica) / CommunityToolkit.Mvvm / Tomlyn。
数据与配置表存于 `%APPDATA%/McModsAdder/`。

## 构建与运行

```powershell
dotnet build
dotnet run --project src/McModsAdder
```

打包单文件 exe：

```powershell
dotnet publish src/McModsAdder -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

## 使用流程

1. 「游戏实例」页自动扫描，或点「选择整合包目录」手动指定（.minecraft / versions / 实例目录均可）。
2. 「配置表」页新建配置表，搜索并添加常用 mod；可导出分享。
3. 打开实例详情，选择配置表，查看对比结果。
4. 点「一键补装缺失 mod」，确认清单（含自动补装的依赖）后开始安装。

## 当前开发状态（2026-08-29）

- 核心流程已实现：实例扫描、mod 哈希/元数据识别、配置表 CRUD 与导入导出、Modrinth 版本匹配、依赖闭包、并发下载、SHA-1 校验和冲突备份。
- 最近已完成滚动布局调整：页面标题/操作区固定，列表区域独立滚动；主窗口对鼠标滚轮进行重定向，避免导航栏焦点导致滚动失效。
- 实例识别已覆盖 `.minecraft`、`versions`、单实例目录，并支持从版本 JSON、HMCL `patches`、libraries 和目录名推断 MC 版本与 Fabric/Forge/Quilt/NeoForge 加载器。
- 本次继续开发：手动选择整合包根目录时，额外识别根目录下的 `.minecraft/versions`，避免常见整合包目录结构漏扫。
- 构建验证：`dotnet build McModsAdder.sln --no-restore` 已通过，0 警告、0 错误。

### 后续优先事项

1. 在真实 PCL/HMCL 和不同整合包目录结构上验证实例扫描结果。
2. 验证 Modrinth 官方源/镜像源、无版本匹配、网络失败和取消安装等异常流程。
3. 补充自动化测试，重点覆盖 `InstanceScanner` 版本/加载器判定、`ModInstaller` 依赖闭包和下载失败处理。
4. 二期功能：CurseForge Provider、已有 mod 更新，以及更细致的安装结果/日志展示。

