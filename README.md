# MCMod++

<p align="center">
  <strong>面向 Minecraft 整合包的 Mod 管理与批量补装工具</strong>
</p>
<p align="center">
  <em>专注于让整合包换装和 Mod 管理更简单。</em>
</p>


> **项目状态：开发中**
>
> 当前版本为 `v1.1.1`。项目仍在持续开发和完善，可能存在 Bug、兼容性问题或文件操作风险。请在使用前备份 Minecraft 实例和 Mod 文件，并在非生产环境中进行验证。

## 项目简介

MCMod++ 是一款 Windows 桌面应用，用于识别 Minecraft 游戏实例、维护可复用的 Mod 配置表，并根据实例的 Minecraft 版本和 Mod 加载器批量补装缺失 Mod。

软件名称已由原来的 **旧版项目名称** 正式更名为 **MCModPlus**。本次版本同时加入了 CurseForge 搜索支持和本地 Mod 库，在线 Mod 与本地 Mod 可以在同一套配置表中使用。

## 开发说明

本软件的开发工作全程由 **CodeBuddy** 协助完成并实际落地，包括代码编写、功能实现、问题修复、项目构建及相关开发工作。作者主要负责提出项目的初步想法、明确软件需求，并根据实际使用情况对软件后续功能和体验改进提出建议。感谢 CodeBuddy 在本项目开发过程中的协作与支持。

## 主要功能

- **游戏实例识别**：自动扫描常见的 `.minecraft`、`versions` 和单实例目录，也支持手动选择整合包目录。
- **加载器识别**：支持 Fabric、Forge、Quilt 和 NeoForge，并可从版本 JSON、HMCL patches、libraries 及目录结构中推断信息。
- **已安装 Mod 分析**：通过 SHA-1 哈希优先识别 Mod；无法匹配时解析 `fabric.mod.json`、`mods.toml`、`quilt.mod.json` 和 `mcmod.info`。
- **配置表管理**：创建、编辑、删除、导入和导出可复用的 Mod 配置表；配置表只记录项目，不绑定特定实例版本。
- **多来源搜索**：支持 Modrinth 与 CurseForge，可按来源搜索并根据项目来源获取版本、依赖和下载文件。
- **版本匹配**：根据实例的 Minecraft 版本和加载器筛选可用 Mod 版本，并显示已安装、缺失或无可用版本的状态。
- **一键补装**：批量下载并安装缺失 Mod，自动处理必需依赖，下载后校验 SHA-1，并可在写入前备份旧文件。
- **本地 Mod 库**：批量导入 `.jar` 文件，自动解析名称、版本、加载器、Minecraft 版本和图标；支持搜索、筛选、排序、重命名、批量编辑和删除。
- **本地 Mod 配置**：在配置表编辑页面直接从本地 Mod 库添加 Mod，与在线 Mod 条目统一管理。
- **设置项**：支持 Modrinth 官方源与 MCIM 镜像源切换、下载并发数、备份开关、依赖安装开关和 CurseForge API Key 管理。
- **界面体验**：基于 WPF-UI 的 Fluent Design 深色 Mica 界面，页面标题和操作区固定，列表区域独立滚动。

## 使用流程

1. 打开「游戏实例」页面，等待自动扫描，或手动选择 `.minecraft`、`versions` 或整合包目录。
2. 打开「配置表」页面，新建配置表。
3. 搜索并添加 Mod；也可以从「本地 Mod 库」导入 `.jar` 后，在配置表中添加本地 Mod。
4. 进入实例详情，选择配置表并查看已安装、缺失和不可用项目。
5. 点击「一键补装缺失 mod」，确认安装清单（包括自动解析出的必需依赖）后开始安装。

## 获取与运行

### 直接运行发布版

在 GitHub Releases 下载带有 `win-x64` 的发布包。若使用自包含发布包，目标电脑无需单独安装 .NET 运行时。

### 从源码构建

环境要求：

- Windows 10/11
- .NET 8 SDK
- 可访问 Modrinth 或 CurseForge 服务的网络环境（仅使用本地 Mod 库时不需要在线服务）

在仓库根目录执行：

```powershell
dotnet restore MCModPlus.sln
dotnet build MCModPlus.sln -c Release
```

运行项目：

```powershell
dotnet run --project src/MCModPlus/MCModPlus.csproj
```

生成 Windows x64 自包含单文件程序：

```powershell
dotnet publish src/MCModPlus/MCModPlus.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

发布文件默认位于：

```text
src/MCModPlus/bin/Release/net8.0-windows/win-x64/publish/
```

## 数据与隐私

应用数据默认保存在：

```text
%APPDATA%/MCModPlus/
```

其中包括设置、配置表、本地 Mod 索引以及本地 Mod 文件。CurseForge API Key 不会写入源码仓库；用户配置的 Key 使用 Windows DPAPI 保护后保存，内置默认 Key 也不会以明文形式直接写入代码。

请不要将个人配置目录、日志、API Key、Minecraft 实例文件或本地 Mod 文件提交到 GitHub。提交 Issue 或日志时，请先移除路径、账号信息和其他隐私内容。

## 第三方服务与内容声明

- Modrinth、CurseForge、Fabric、Forge、Quilt 和 NeoForge 等名称及相关商标归其各自权利人所有。
- Minecraft 及相关名称、标志和素材归 Mojang Studios / Microsoft 所有。
- MCMod++ 与 Mojang Studios、Microsoft、PCL、HMCL、Modrinth、CurseForge、Fabric、Forge、Quilt 或 NeoForge 没有隶属、赞助或官方授权关系。
- 本项目仅调用相关公开服务；Mod 文件、图标、名称、描述及其他第三方内容的版权和许可归原权利人所有。使用这些内容时请遵守对应服务条款和项目许可证。
- 项目依赖的许可证信息以各依赖项目的官方许可证为准。

## 许可证与使用限制

本仓库当前未提供独立的 `LICENSE` 文件。除第三方依赖和明确标注的第三方内容外，项目原创代码、界面、文档和资源的使用、再发布、商业使用及二次开发，请先联系维护者 `Cheering571` 并获得明确许可。请勿将本项目冒充为官方软件或官方授权产品，也不要移除项目中的版权和来源声明。

## 更新日志

完整更新记录见 [`CHANGELOG.md`](CHANGELOG.md)。
