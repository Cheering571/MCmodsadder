# MCMod++ v1.1.1 更新日志

发布日期：2026-09-05

## 本次更新

### 发布形式

- 新增 Windows x64 自包含单文件程序：`MCModPlus.exe`，无需安装 .NET 运行时，可直接运行。
- 同时提供安装包：`MCModPlus-Setup-1.1.1-win-x64.exe`。

### CurseForge API Key

- 默认不配置 CurseForge API Key，软件不会内置或自动使用任何 Key。
- 如需使用 CurseForge 搜索源，请在设置页面手动配置有效的 CurseForge API Key。
- 未配置 CurseForge API Key 无需担心：软件默认搜索源为 Modrinth，不会影响 Modrinth 的搜索、版本查询和下载使用。

### 下载与文件处理修复

- 修复多个 Mod 并行下载时临时文件冲突及文件占用问题。
- 下载完成后释放文件流，再执行校验和文件替换，减少 `IOException` 文件占用错误。
- 增加文件替换和备份操作的重试机制，提高下载和安装稳定性。

### 配置表与实例管理

- 改进实例 Mod 识别、配置表匹配和本地 Mod 库处理逻辑。
- 优化配置表中 Mod 的版本、加载器和 Minecraft 版本信息展示。

## 使用说明

- 本版本为 Windows x64 自包含发布包，目标电脑无需单独安装 .NET 运行时。
- 首次使用前请备份 Minecraft 实例和 Mod 文件。
- 默认搜索源为 Modrinth；只有需要使用 CurseForge 搜索时才需要配置 API Key。
