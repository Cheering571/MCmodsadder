# McModsAdder 对话修改记录与项目交接文档

> 文档用途：记录本对话期间用户提出的问题、已采取的修改方案、当前项目状态和后续建议，便于后续大模型或用户快速恢复上下文。
>
> 整理时间：2026-08-29
> 项目目录：`f:/Codebuddy/MCmodsadder`

## 1. 文档范围与记录依据

本记录根据当前工作区可见内容、项目内已有开发计划和 `README.md` 汇总。当前工作区未检测到可用的 Git 仓库元数据，因此无法通过 Git 提交时间或差异精确还原每一次对话修改；以下时间线按项目文档中的开发阶段和 `README.md` 的“当前开发状态”整理。未在当前上下文中出现、且无法从工作区确认的内容不作臆测。

## 2. 项目目标

McModsAdder 是一个 Windows WPF 桌面工具，用于：

1. 扫描 PCL/HMCL 等启动器创建的 Minecraft 版本隔离实例。
2. 识别 Minecraft 游戏版本、Mod 加载器和 `mods` 目录。
3. 通过 SHA-1 和 jar 内元数据识别已安装 Mod。
4. 维护不绑定具体版本和加载器的 Mod 配置表。
5. 根据实例环境从 Modrinth 查找并下载缺失 Mod及其必需依赖。
6. 下载前备份、SHA-1 校验，并展示安装进度和失败原因。

## 3. 按时间线整理的用户问题与修改

### 阶段一：搭建桌面应用骨架

**用户提出的问题/需求**

希望从空工作区开发一款中文 Windows 图形界面的 Minecraft Mod 批量添加工具，要求使用现代深色 Fluent/Mica 风格，并提供实例、配置表、安装和设置等页面。

**修改与实现**

- 创建 .NET 8 WPF 解决方案和项目结构。
- 引入 WPF-UI、CommunityToolkit.Mvvm、Tomlyn 等依赖。
- 配置应用入口、依赖注入、导航服务和主题。
- 创建主窗口和页面导航骨架：
  - 游戏实例
  - 配置表
  - 设置
  - 实例详情
  - 配置表编辑
  - 安装进度
- 按分层架构组织代码：`Views` → `ViewModels` → `Services` → `Providers` → `Models`。

**结果**

项目具备可运行的 Windows 桌面应用基础，后续领域功能可以独立扩展。

### 阶段二：实现 Minecraft 实例识别

**用户提出的问题/需求**

需要自动发现 PCL/HMCL 的版本隔离实例，并识别 Minecraft 版本、Forge/Fabric/Quilt/NeoForge 加载器以及 `mods` 目录；同时支持用户手动选择目录。

**修改与实现**

实现 `InstanceScanner`，主要策略如下：

- 扫描 `.minecraft/versions` 下的实例目录。
- 支持直接选择 `.minecraft`、`versions` 或单个实例目录。
- 解析实例 JSON，包括：
  - `inheritsFrom`
  - `mainClass`
  - `libraries`
  - HMCL `patches`
- 从 JSON、库坐标和目录名称综合推断游戏版本及加载器。
- 定位实例对应的 `mods` 目录。
- 支持一级子目录中的 Mod 文件。
- 跳过 `.disabled` 文件。

**补充完善**

手动选择整合包根目录时，额外检查根目录下的 `.minecraft/versions`，覆盖常见整合包目录结构，避免只选择整合包根目录时漏扫实例。

**结果**

实例识别范围覆盖常见启动器目录结构，并保留了对未知结构的容错推断能力。

### 阶段三：实现已安装 Mod 识别

**用户提出的问题/需求**

需要准确判断实例里已经安装了哪些 Mod，即使 jar 文件被重命名，也应尽量识别；无法通过在线服务识别时，也要能展示基础信息。

**修改与实现**

实现 `ModJarAnalyzer`：

- 并行计算所有 Mod jar 的 SHA-1。
- 调用 Modrinth 批量哈希接口进行精确匹配。
- 哈希命中时获取项目 ID、项目名称、版本等信息。
- 对未命中的 jar 解析内部元数据作为兜底：
  - `fabric.mod.json`
  - `META-INF/mods.toml`
  - `quilt.mod.json`
  - `mcmod.info`
- 将识别方式区分为哈希命中或元数据识别。

**结果**

重命名的 Mod jar 仍可通过内容哈希识别；第三方 Mod 或未被 Modrinth 匹配的文件也能显示基础信息。

### 阶段四：接入 Modrinth Provider

**用户提出的问题/需求**

配置表只应保存 Mod 项目，而不是固定版本。安装时应根据实例的 Minecraft 版本和加载器实时选择兼容版本，并支持未来接入 CurseForge。

**修改与实现**

- 定义 `IModProvider` 抽象接口。
- 实现 `ModrinthProvider`，支持：
  - Mod 搜索
  - 项目详情查询
  - 按 Minecraft 版本和加载器筛选版本
  - 批量 SHA-1 匹配
  - 文件下载
  - 官方源与镜像源基地址切换
- Quilt 查询时优先使用 Quilt，并回退查询 Fabric。
- 对 Forge 与 NeoForge 做严格区分，避免在新版本中错误混用。
- 为 CurseForge 预留 Provider 扩展点，不在一期引入 API Key 和授权限制的复杂度。

**结果**

同一张配置表可以复用于不同 Minecraft 实例，Mod 版本由当前实例环境动态决定。

### 阶段五：实现配置表管理

**用户提出的问题/需求**

需要维护多张常用 Mod 清单，支持搜索添加、删除、重命名、导入和导出，便于复用和分享。

**修改与实现**

实现 `ProfileService` 及其页面/视图模型：

- 配置表增删改查。
- 条目保存项目 ID/slug，而不是具体版本号。
- 保存项目显示名、图标 URL 和添加时间等展示信息。
- 通过 Modrinth 搜索添加 Mod。
- 支持多个配置表。
- 支持 JSON 导入导出。
- 导入时进行格式校验，并合并重复条目。
- 搜索增加约 300ms 防抖，减少无效请求。

**结果**

用户可以建立“常用客户端 Mod”“性能优化”“服务器必备”等可复用清单。

### 阶段六：实现对比、依赖和一键补装

**用户提出的问题/需求**

需要将实例当前已安装 Mod 与配置表对比，并一键下载缺失 Mod；自动处理必需依赖，同时避免循环依赖、重复安装和下载失败导致的文件损坏。

**修改与实现**

实现 `ModInstaller`、实例详情和安装进度流程：

- 展示已安装、缺失、无可用版本三种状态。
- 按实例的 Minecraft 版本和加载器查询最佳匹配文件。
- 通过 BFS 递归收集必需依赖，形成依赖闭包。
- 使用已安装集合和访问集合去重，并防止循环依赖。
- 找不到兼容版本的必需依赖进入不可自动安装清单。
- 文件先下载到临时目录。
- 下载完成后校验 SHA-1，校验通过才移动到 `mods` 目录。
- 被替换或冲突的旧文件移动到 `mods/.mcmodsadder-backup/<时间戳>/`。
- 支持可配置下载并发数，单个文件失败不阻断其他文件。
- 最终展示成功数量、失败原因、不可用清单和备份位置。

**结果**

形成从“选择实例”到“确认缺失清单”再到“下载、校验、备份、安装、汇总”的完整核心流程。

### 阶段七：滚动布局与窗口交互修复

**用户提出的问题/需求**

页面内容较多时，希望页面标题和操作区固定，仅列表内容滚动；同时主窗口鼠标滚轮不能因为导航栏焦点而失效。

**修改与实现**

在各页面采用标题/操作区与内容滚动区分离的布局，并在 `MainWindow` 中增加窗口级滚轮处理：

- 禁用 `NavigationView` 宿主的外层滚动条。
- 在导航完成后查找并禁用导航宿主内部的 `ScrollViewer`。
- 通过 `ApplyPageConstraint()` 将当前页面最大高度限制为 `NavigationView` 实际高度，防止页面按无限高度测量。
- 将滚轮事件交给鼠标所在的实际内容滚动区处理。
- 在窗口初始化阶段和页面切换后都重新应用滚动约束。

**涉及的关键文件**

- `src/McModsAdder/MainWindow.xaml`
- `src/McModsAdder/MainWindow.xaml.cs`
- `src/McModsAdder/Helpers/ScrollHelper.cs`
- 各页面 XAML 文件

**结果**

页面头部保持可见，长列表独立滚动；导航栏获得焦点时，鼠标滚轮仍可作用于内容区域。

### 阶段八：主窗口标题栏图标完善

**用户提出的问题/需求**

希望应用窗口和标题栏显示项目图标，提升桌面应用识别度。

**修改与实现**

在 `MainWindow.xaml` 中：

- 为窗口设置 `Icon`：`/McModsAdder;component/Assets/AppIcon.png`。
- 在 WPF-UI `TitleBar` 中增加 `ImageIcon`，使用同一资源。
- 保留窗口标题 `McModsAdder · MC Mod 批量添加工具`。

**注意事项**

当前 XAML 已引用 `src/McModsAdder/Assets/AppIcon.png`。后续若构建时报资源不存在，需要确认该图片确实位于该目录，并在项目文件中以 WPF `Resource` 方式编译；如果资源实际只存在仓库根目录的 `MCmodsadder.png`，应将其复制/转换为 `Assets/AppIcon.png` 或调整引用路径。

## 4. 当前已确认的项目状态

根据项目文档和当前代码：

- 核心实例扫描已实现。
- Mod 哈希及元数据识别已实现。
- 配置表 CRUD、导入导出已实现。
- Modrinth 版本匹配已实现。
- 必需依赖闭包解析已实现。
- 并发下载、SHA-1 校验和冲突备份已实现。
- 官方源/镜像源、下载并发、备份和依赖相关设置已规划或实现。
- 滚动布局和窗口级滚轮重定向已完成。
- 手动选择整合包根目录识别 `.minecraft/versions` 已完成。
- `dotnet build McModsAdder.sln --no-restore` 在项目记录中显示为 0 警告、0 错误；在后续改动后仍建议重新构建确认。

## 5. 当前代码结构速览

```text
src/McModsAdder/
├── App.xaml / App.xaml.cs
├── MainWindow.xaml / MainWindow.xaml.cs
├── Models/              领域模型与 Modrinth DTO
├── Providers/           Modrinth API Provider
├── Services/            扫描、分析、配置、安装、设置服务
├── ViewModels/          MVVM 页面逻辑
├── Views/               WPF 页面与详情弹窗
├── Converters/          状态、枚举、颜色和图标转换器
└── Helpers/             滚轮等通用辅助逻辑
```

## 6. 后续大模型接手指南

后续处理本项目时，建议按以下顺序确认：

1. 先阅读本文件、`README.md` 和 `.codebuddy/plans/MC_Mods_Adder_桌面应用_b0666e6f.md`。
2. 检查 `src/McModsAdder/McModsAdder.csproj` 的资源和依赖配置。
3. 检查 `Assets/AppIcon.png` 是否存在，以及 `MainWindow.xaml` 的资源引用是否可解析。
4. 运行 `dotnet build McModsAdder.sln --no-restore`，确认当前基线。
5. 在真实 PCL/HMCL 和不同整合包目录结构上验证 `InstanceScanner`。
6. 验证 Modrinth 官方源、镜像源、无匹配版本、网络失败和取消安装流程。
7. 为 `InstanceScanner`、`ModInstaller` 和下载失败处理补充自动化测试。
8. 二期再考虑 CurseForge Provider、已有 Mod 更新和更详细的日志展示。

## 7. 建议的持续记录规范

以后每完成一项修改，建议在本文件追加一条记录，至少包含：

- 日期和大致时间。
- 用户问题或目标。
- 修改文件。
- 核心实现方式。
- 验证结果。
- 已知限制和后续事项。

如果未来恢复 Git 管理，建议以提交记录作为精确时间线，并在每次功能完成后保留简短、可检索的提交说明。

## 8. 交接摘要

McModsAdder 已从项目骨架发展为具备核心可用流程的 .NET 8 WPF 应用：能够发现 Minecraft 实例，识别已装 Mod，维护跨实例复用的 Mod 配置表，并从 Modrinth 自动补装兼容版本及必需依赖。近期重点修复了长页面滚动体验和整合包根目录漏扫问题，并完善了窗口标题栏图标引用。下一步重点是确认图标资源构建、重新构建验证，以及在真实启动器和网络异常环境下进行实测。

## 9. 2026-08-29 当日持续记录

### Mods 详情入口与布局调整

- **大致时间**：今日会话期间（具体时分未记录）。
- **用户问题或目标**：希望实例详情页中“共计 xx 个 mod”入口按钮延伸到可用区域右侧，按钮文字居中显示；同时希望实例详情页顶部的返回和重新识别按钮脱离整合包名称卡片，放置在页面最上方，并保持一左一右的位置。
- **修改文件**：`src/McModsAdder/Views/InstanceDetailPage.xaml` 及对应实例详情页代码。
- **核心实现方式**：将 Mods 详情入口按钮设置为横向拉伸、内容区域拉伸并通过三列布局实现图标靠左、文字居中、箭头靠右；调整实例详情页顶部操作区布局，使返回和重新识别按钮位于页面顶部两侧。
- **验证结果**：相关修改后曾完成构建并启动应用。
- **已知限制和后续事项**：Mods 详情窗口尺寸、三栏自适应宽度和独立横向滚动方案曾进行尝试，后按用户要求撤回，当前恢复原始窗口尺寸、固定三栏布局和文本省略显示。

### Mods 详情页滚动条方案撤回

- **大致时间**：今日会话期间（具体时分未记录）。
- **用户问题或目标**：撤回最近三次为解决 Mods 详情页滚动条显示不全而进行的操作。
- **修改文件**：`src/McModsAdder/Views/InstalledModsWindow.xaml`。
- **核心实现方式**：移除详情窗口尺寸放大、三栏自适应最大宽度、独立横向滚动区域、额外 `MinHeight`、滚动边距、像素滚动和自动隐藏滚动条附加属性，恢复原始 `ScrollViewer VerticalScrollBarVisibility="Auto"` 配置。
- **验证结果**：撤回过程中修复了残留 `helpers` 命名空间属性导致的 XAML 编译错误；最终构建为 0 个警告、0 个错误，并启动应用。
- **已知限制和后续事项**：当前超长名称、版本号和文件名仍按原布局使用省略显示，未提供独立横向查看全文的区域。

### Mods 详情窗口打开响应与激活状态优化

- **大致时间**：今日会话期间（具体时分未记录）。
- **用户问题或目标**：点击“共计 xx 个 mod”后，详情页弹出前存在短暂延迟，且主窗口阴影会发生变化。
- **修改文件**：`src/McModsAdder/Views/InstalledModsWindow.xaml`、`src/McModsAdder/Views/InstalledModsWindow.xaml.cs`。
- **核心实现方式**：窗口构造阶段不立即创建 `InstalledModsViewModel`，改为窗口加载后通过 `DispatcherPriority.ContextIdle` 延后填充 mod 列表；设置 `ShowActivated="False"`，减少弹出窗口时主窗口激活状态切换及阴影变化。
- **验证结果**：重新构建并启动应用，结果为 0 个警告、0 个错误。
- **已知限制和后续事项**：延后列表绑定可能导致窗口首帧与列表内容填充存在极短的视觉先后顺序；如实测仍有延迟，应进一步对比列表项数量和 WPF 布局测量耗时。

### Mods 详情窗口交互当前实现

- `src/McModsAdder/Views/InstalledModsWindow.xaml` 的最外层详情卡片通过 `PreviewMouseLeftButtonDown="WindowDrag_MouseLeftButtonDown"` 响应拖动。
- `SearchBorder` 和 `ModsListBorder` 用于标记搜索区域和 mod 列表区域；这两个区域在拖动判断中被排除，搜索输入和列表滚动不应被窗口拖动抢占。
- 关闭按钮使用 `x:Name="CloseButton"`，其自身及模板内部元素在拖动判断中被排除，`CloseButton_Click` 负责关闭窗口。
- 详情窗口当前使用无边框、不可调整大小模式，并设置 `ShowActivated="False"`，用于减少弹出时主窗口激活状态和阴影变化。
- 详情窗口的 mod 列表通过 `InstalledModsWindow.xaml.cs` 中的 `Loaded` 事件和 `DispatcherPriority.ContextIdle` 延后绑定，以改善点击入口后的响应速度。

## 10. 明日继续开发交接

### 明日开始前必须了解

1. 项目路径为 `F:/Codebuddy/MCmodsadder`，核心项目位于 `src/McModsAdder/`。
2. 这是一个 .NET 8 WPF 应用，界面使用 WPF-UI，整体为深色 Fluent/Mica 风格。
3. 今天主要改动集中在 Mods 详情入口和详情窗口，涉及：
   - `src/McModsAdder/Views/InstanceDetailPage.xaml`
   - `src/McModsAdder/Views/InstanceDetailPage.xaml.cs`
   - `src/McModsAdder/Views/InstalledModsWindow.xaml`
   - `src/McModsAdder/Views/InstalledModsWindow.xaml.cs`
   - `CHANGELOG_HANDOFF.md`
4. 当前项目没有可用的 Git 仓库元数据，不能依赖提交记录还原修改；应优先阅读本文件和当前代码。

### 今天结束时的功能状态

- 实例详情页中的“共计 xx 个 mod”按钮已横向延伸到可用区域，按钮文本居中，左侧保留图标，右侧保留箭头。
- 实例详情页顶部的返回和重新识别按钮已脱离整合包名称区域，分别位于页面最左侧和最右侧。
- Mods 详情窗口滚动条相关的三次尝试已按用户要求撤回：当前恢复原始窗口尺寸、固定三栏布局、文本省略显示，以及 `ScrollViewer VerticalScrollBarVisibility="Auto"`。
- Mods 详情窗口采用延后列表绑定和 `ShowActivated="False"`，用于改善弹出延迟和主窗口阴影变化。
- Mods 详情窗口标题及其他非搜索、非列表的空白区域支持按住拖动。
- 搜索框、mod 列表和关闭按钮不应拖动窗口；关闭按钮应正常关闭详情窗口。
- 最近一次构建已成功：0 个警告、0 个错误，并启动了应用。

### 明日建议的第一步

1. 先重新阅读本文件第 9、10 节，再读取上述 4 个界面相关文件的当前内容，不要直接根据历史描述修改。
2. 运行 `dotnet build src/McModsAdder/McModsAdder.csproj --no-restore`，建立当前基线。
3. 实测 Mods 详情窗口：点击入口、观察弹出延迟和主窗口阴影；拖动标题和空白区域；确认搜索、列表滚动和关闭按钮行为。
4. 若用户提出新需求，优先进行小范围修改，保留今天已经确认正常的交互行为。
5. 每完成一项后，继续按照第 7 节格式在本文件追加日期、目标、文件、实现、验证和限制记录。

### 目前已知的待观察问题

- 延后列表绑定理论上会让窗口先显示、列表稍后填充；如果实测出现空白闪烁或仍有明显延迟，应评估列表项数量、WPF 布局测量和窗口透明渲染开销，再决定是否调整实现。
- `ShowActivated="False"` 主要用于降低主窗口阴影变化，但需要在实际 Windows 桌面环境确认详情窗口的激活、置顶和键盘焦点体验。
- 无边框窗口的拖动依赖最外层 Border 的预览鼠标事件和可视树判断；如后续增加新的控件或区域，应明确决定其是否属于可拖动区域。

### 交接原则

明日继续开发时，以当前代码实际行为为准，以用户最新要求为最高优先级；不得自动恢复今天已撤回的 Mods 详情页横向滚动和自适应列宽方案，除非用户再次明确提出。涉及窗口交互时，应同时回归验证拖动、搜索、列表滚动和关闭按钮，避免修复一项功能时破坏其他行为。

### 每次修改后的强制验证流程

每完成一次代码或界面修改，都必须按以下顺序执行，不得直接沿用修改前已经打开的软件实例：

1. **关闭当前已打开的软件**：先关闭正在运行的 `McModsAdder`；如软件未正常退出，再结束对应进程，避免旧进程锁定构建输出文件。
2. **重新构建项目**：在项目目录 `F:/Codebuddy/MCmodsadder` 执行构建，优先使用：
   `dotnet build src/McModsAdder/McModsAdder.csproj --no-restore`
3. **检查构建结果**：必须确认构建成功，并且结果为 0 个警告、0 个错误；若构建失败或输出文件仍被占用，应先处理问题，不得启动旧版本继续验证。
4. **打开新的软件实例**：仅在构建成功后启动最新生成的 `McModsAdder.exe`，确认实际运行的是本次修改后的版本。
5. **进行功能回归**：针对本次修改验证目标功能，同时回归相关交互，尤其是 Mods 详情窗口的打开、拖动、搜索、列表滚动和关闭按钮。
6. **记录结果**：在本文件按照第 7 节规范追加本次修改的日期、大致时间、用户目标、修改文件、实现方式、验证结果、已知限制和后续事项。

> 注意：如果构建时提示 `McModsAdder.exe` 被进程占用，应先关闭该软件进程，再重新执行构建；不能因为构建失败而直接向用户报告已验证完成。

## 11. 2026-08-30 Inno Setup 安装程序制作与复核

- **大致时间**：2026-08-30，会话期间。
- **用户问题或目标**：验证并继续完成使用 Inno Setup 制作 McModsAdder 安装程序的任务。
- **修改文件**：`McModsAdder.iss`；生成发布目录 `publish/win-x64/` 和安装包目录 `installer/`。
- **核心实现方式**：复核现有 Inno Setup 脚本，关闭可能运行的旧版程序后重新执行 .NET 8 `Release`、`win-x64`、自包含、单文件发布；使用 Inno Setup 6.7.3 编译脚本，将发布文件打包为 Windows x64 安装程序。安装脚本支持选择安装目录、开始菜单快捷方式、可选桌面快捷方式、卸载和安装完成后启动。
- **验证结果**：项目 Release 构建成功，0 个警告、0 个错误；自包含发布成功；Inno Setup 编译成功，生成 `installer/McModsAdder-Setup-1.0.0-win-x64.exe`。安装包大小为 51,570,217 字节，SHA-256 为 `1396C1416E8C97C843037AC2CDAABB179E24527955885AFB060A6CB31FB2D553`；安装脚本检查无诊断信息。


## 12. 2026-08-30 GitHub Release 中文描述乱码修复

- **大致时间**：2026-08-30，会话期间。
- **用户问题或目标**：发现 GitHub `v1.0.0` Releases 页面中的中文描述出现乱码，要求检查并修复。
- **修改文件**：GitHub Release `v1.0.0` 的发布说明；本地使用临时文件 `release-notes-zh.md`，更新完成后已删除。
- **核心实现方式**：重新生成 UTF-8 编码的 Markdown 发布说明，通过 GitHub CLI 的 `--notes-file` 更新 Release，避免 PowerShell 命令行直接传入中文时发生编码转换，并保留 Markdown 标题、列表和代码格式。
- **验证结果**：Release API 返回的正文已为正常简体中文，临时说明文件已清理；安装包资产和原有 SHA-256 校验值未改变。
- **已知限制和后续事项**：部分网页抓取工具可能仍按错误编码显示 GitHub 页面缓存内容；以 GitHub 页面实际刷新后的显示和 Release API 返回正文为准。

## 13. 2026-08-30 README 项目声明与版权标识完善

- **大致时间**：2026-08-30，会话期间。
- **用户问题或目标**：说明项目主要由 AI 生成，补充必要的开发状态、版权、第三方商标/内容归属和免责声明，降低误导及侵权风险，并明确项目仍在开发中、可能存在较多 Bug。
- **修改文件**：`README.md`、`CHANGELOG_HANDOFF.md`。
- **核心实现方式**：在 README 顶部增加“开发中”提示，明确项目尚未完成、可能存在 Bug 和使用风险；新增 AI 生成与开发者审阅说明、原创贡献版权声明、第三方项目和 Mod 内容归属说明、非官方关系声明、按现状提供及责任限制说明，以及再发布/商业使用/二次开发需先取得书面许可的提示。
- **验证结果**：README 已完成修改；未修改软件源代码、安装包或 Release 资产；待提交并推送至 GitHub 后核对仓库页面显示。
- **已知限制和后续事项**：README 声明不构成法律意见，也不能替代正式许可证；如未来明确开放源代码或允许再分发，应另行选择并添加适当的 `LICENSE` 文件，且逐项核对第三方依赖许可证。
