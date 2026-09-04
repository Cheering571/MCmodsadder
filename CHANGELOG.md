# MCMod++ v1.1.0 更新日志

发布日期：2026-09-04

## 本次更新

### 项目更名

- 软件统一更名为 `MCModPlus`，应用程序、项目文件、安装包和文档均已同步更新。
- 安装包名称为 `MCModPlus-Setup-1.1.0-win-x64.exe`。

### CurseForge 支持

- 新增 CurseForge Provider 初步支持，可在配置表搜索源下拉选项中选择 CurseForge。
- 支持合并 Modrinth 与 CurseForge 搜索结果，并根据项目来源路由版本查询、依赖解析和文件下载。
- 优化 CurseForge 搜索请求，传入用户实际搜索关键词并限制 Minecraft Mod 分类。
- 支持在设置页面编辑、清除 CurseForge API Key。

### 本地 Mod 库

- 新增本地 Mod 库，可批量导入 `.jar` 文件并集中管理。
- 导入后自动复制 Mod 文件到应用数据目录的 `local-mods/files`，支持离线使用。
- 自动解析并展示 Mod 名称、版本、加载器和 Minecraft 版本，并尝试提取 Mod 图标。
- 支持按名称或文件名搜索，按加载器和 Minecraft 版本筛选，以及按名称、版本或加载器排序。
- 支持重命名、修改加载器、修改 Minecraft 版本、批量编辑和删除本地 Mod。
- 配置表支持从本地 Mod 库直接添加 Mod，并与在线 Mod 条目一起保存和管理。

### 搜索与交互修复

- 修正配置表搜索源选择链路，确保选择 CurseForge 后实际请求 CurseForge，而不是继续使用 Modrinth。
- 保留 Modrinth 官方源与 MCIM 镜像源切换能力。
- 延续上一版本的页面标题和操作区固定、列表独立滚动及鼠标滚轮重定向改进。

## 使用说明

- 本版本为 Windows x64 自包含发布包，目标电脑无需单独安装 .NET 运行时。
- 首次使用前请备份 Minecraft 实例和 Mod 文件。
- CurseForge 搜索功能需要配置有效的 CurseForge API Key。
