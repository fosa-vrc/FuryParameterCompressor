# Fury Parameter Compressor

# English: [README.md](README.md)

把 VRCFury 的参数压缩器移植到 NDMF，并用 Modular Avatar 的跨平台参数对齐方案替换掉 VRCFury 原本的做法。

## 使用说明

将 [Fury Parameter Compressor](https://fosa-vrc.github.io/FuryParameterCompressor/) 添加到 VCC / ALCOM 仓库

在 VCC / ALCOM 中为项目添加 Fury Parameter Compressor

在 Avatar 上挂载 **FOSA / Fury Parameter Compressor** 组件即可生效。没有挂载该组件的 Avatar 完全不会被处理。

## 开发声明

本项目为 **Vibe Coding** 产物：

* **核心开发**：由 **Claude Opus 5** 进行功能移植，并由人工进行微调。
* **审核与修复**：由 **Claude Fable 5** 与人工共同进行 Code Review 及 Bug 修复。

> **许可证**：本包是 VRCFury 的衍生作品，因此**仅限个人（非商业）用途**，详见 [LICENSE.md](LICENSE.md)。

## 上游版本

| | |
| --- | --- |
|VRCFury | 1.1389.0 |
|Modular Avatar | 1.18.0 |
|NDMF | 1.14.3 |


## 环境要求

| | |
| --- | --- |
| Unity | 2022.3 |
| NDMF | >= 1.14.3 |
| VRChat SDK | Avatars 3.0 |
| Modular Avatar | **可选** —— 见下文 |

Modular Avatar 不是硬依赖。整个包里只有一个文件会用到 MA 类型（`Editor/SyncParameterSequence/MaSyncParameterSequenceDetector.cs`，由 `FPC_MODULAR_AVATAR` 版本宏包裹），它唯一的用途是找出 MA Sync Parameter Sequence 组件以便做互斥检查。没有装 MA 时根本不可能存在该组件，因此这项检查永远不会触发。其余部分要么走 NDMF 的 API，要么是本包内自带的 MA 代码副本。

需要注意的是，Inspector 里显示的参数占用来自 NDMF 的参数自省 API——MA 正是把自己的组件接进这个 API 的——所以没装 MA 时，这个数字只是少统计了一部分来源。

## 功能

当 Avatar 的同步表情参数超出 VRChat 的上限时，压缩器会挑出其中一部分，让它们分批轮流复用少量共享同步槽位，从而把总占用压到上限之内。被挑中的参数不再各自单独同步，而是由一个名为 `Parameter Compressor` 的 FX 层逐批在共享槽位之间来回拷贝。

代价是同步延迟：完整同步一轮最快约 `批次数 × 0.1 秒`，最慢约为该值的两倍。每次构建后，实际数值都会输出到 NDMF 控制台。

## 组件选项

| 选项 | 说明 |
| --- | --- |
| 主平台 | `PC`（默认）/ `Android` / `iOS`。以该平台的解算结果为准——请先构建它，其他平台会复用其结果。 |
| Write Defaults | `自动` / `强制 ON` / `强制 OFF`。`自动` 会与 FX 控制器的其余部分保持一致，规则与 MA Merge Animator 的「匹配 Avatar Write Defaults」相同；若 Avatar 中两种设置混用，则结果为 OFF。 |
| 参数白名单 | 列在其中的参数永远不会被压缩，它们保持全速同步，也继续占用完整的 bit 数。 |

Inspector 还会显示当前 Avatar 的参数占用，数值通过 NDMF 的参数自省 API 统计——与「MA Information」同源，因此在安装了 Modular Avatar 时**包含 Modular Avatar 组件提供的参数**。

### 参数白名单

列表是标准的多选列表：点击行首的把手可选中该行，按住 Ctrl / Shift 可一次选中多行，然后按 `Delete` 键（或点「删除」按钮）删除。每行右侧还有一个 `-` 按钮，用于只删除该行。行本身是可编辑的文本框，列表支持拖拽排序（排序不影响构建结果）。

三种填写方式，可以随意混用：

* **添加** —— 追加一个空行，直接输入参数名。
* **选择** —— 打开选取窗口，其中列出当前 Avatar 上的同步参数（同样走 NDMF 自省，因此包含 MA 组件）。窗口支持按住 Ctrl / Shift 多选，另有「全选」/「反选」/「清空」/「刷新」，按 `Enter` 等同于「添加」。选取过程**只做追加**：窗口永远不会移除白名单里的任何东西，而且已经在白名单里的参数根本不会出现在列表中。「添加后关闭弹窗」（默认开启）决定点击「添加」后是否关闭窗口。
* **从 Asset 导入** —— 把 `VRC Expression Parameters` 资产拖到该字段上，即可追加其中的全部参数名。

「清空」会清空整个列表，并先弹窗确认。

列表中**允许**存在重复项和空行，这是有意为之；只有在构建时真正应用白名单的那一刻才会去重并忽略空项。

## 构建行为

三个 NDMF Pass：

* **Check for conflicts** —— `BuildPhase.Resolving`，即在 Modular Avatar 删除自己的组件之前执行。
* **Compress parameters** —— `BuildPhase.Transforming`，对项目中找到的**每一个** NDMF 插件都声明了 `AfterPlugin`（通过扫描程序集上的 `[assembly: ExportsPlugin]` 发现），并对 Avatar Optimizer 声明了 `BeforePlugin`，以保证它看到的是最终的参数列表。该 Pass 依赖 NDMF 的 `AnimatorServicesContext`。
* **Remove component** —— `BuildPhase.Optimizing`，与上面两个不同，它在**所有** NDMF 平台上运行。VRChat 构建中它是空操作（压缩 Pass 已经移除了组件）；在其他平台上它是唯一会运行的 Pass，确保导出产物不会带着组件。

无论是否真的执行了压缩，组件都会在 Pass 结束时从构建产物中移除。

### 中止与跳过

| 情况 | 结果 |
| --- | --- |
| Avatar 上存在多个组件 | `[FPC-0001]` 报错，中止构建 |
| 同一 Avatar 上存在 **MA Sync Parameter Sequence** | `[FPC-0002]` 报错，中止构建 |
| 项目中检测到 **VRCFury**（包安装或 Assets 散装均可），且 `Tools > VRCFury > Settings > When avatar is over parameter limit` 保持为 `Compress parameters to fit`（默认值） | `[FPC-0003]` 警告，什么都不做——交由 VRCFury 自带的压缩器处理。将其改为 `Ask` 或 `Fail the build` 即可改由本工具压缩 |
| 即使最大压缩强度也放不下 | `[FPC-0004]` 报错，中止构建 |
| 副平台没有主平台记录（或没有 blueprint ID） | `[FPC-0005]` 报错，中止构建 |
| 副平台存在主平台没有的同步参数 | `[FPC-0006]` 报错，中止构建 |
| 跨平台参数类型不一致 | `[FPC-0007]` 报错，中止构建 |
| 复现压缩结果后仍然超出上限 | `[FPC-0008]` 报错，中止构建 |
| 记录由其他版本的本工具生成，或已与本次构建不符 | `[FPC-0009]` 报错，中止构建 |
| 主平台设置与记录中的不一致 | `[FPC-0010]` 警告，构建继续 |
| 压缩结果与主平台记录的不一致 | `[FPC-0011]` 报错，中止构建 |
| 合并后的参数集合与主平台不一致（类型 / 默认值 / 是否同步） | `[FPC-0012]` 报错，中止构建 |
| 需要压缩但 Avatar 没有 FX 控制器 | `[FPC-0013]` 报错，中止构建 |
| 对齐失败，但当前是 Play 模式而非上传 | `[FPC-0014]` 警告，改为本地解算 |

上表中所有「中止构建」都只针对**上传构建**。在 NDMF 的 apply-on-play 处理中，对齐相关的消息会降级为警告，参数改为单独解算并压缩，这样在主平台还没构建过之前 Avatar 依然可以进 Play 模式测试。Play 模式永远不会写入记录。

### 主平台

表情参数会被复制到一份构建期资产中（原资产绝不会被修改）并按名称排序；与上游 VRCFury 一致，解算器看到的是尚未去重的参数列表，重名只在解算之后才会合并。随后解算器会依次尝试越来越激进的菜单类型组合，取其中「放得下且同步最快」的那一个。白名单内的参数和未被表情菜单驱动的参数不会被选中；被 `Add` 类型 Parameter Driver 驱动的参数同样不会被选中，但允许 FourAxisPuppet 的那几次尝试除外。

结果写入 `Library/net.fosa.fury-parameter-compressor/AvatarParameterInfo/<blueprint id>`，内容包括：工具版本、存档版本、压缩前的同步参数集合、压缩后的参数集合、槽位数量、被压缩参数的有序列表，以及一个 SHA-256——它覆盖副平台需要复现的那部分内容（压缩后的参数集合仅用于最后的一致性比对，不在哈希范围内，见下文）。本包不会向项目之外的任何位置写入文件。没有 blueprint ID 的 Avatar 仍然会被压缩，但不会留下记录，因此无法作为跨平台对齐的参照。

### 副平台

不做任何解算。加载主平台的记录并校验（存档版本、工具版本、哈希），把仅存在于主平台的参数补回来，然后原样复现记录中的解算结果。补齐之后会再次计算哈希并与记录比对，不一致就中止构建（并指出首个不一致的参数），而不是产出一个结构不同的 Avatar。

最后，在生成压缩层之后，还会把最终的同步参数集合与主平台的记录再比对一次。生成的 `FPC/*` 槽位参数是在哈希输入快照之后才创建的，无法被哈希覆盖；这一步用来捕捉「某个命名冲突只在单个平台上存在、导致该平台的槽位被迫改名」的情况。

Windows / Linux / Android / iOS 以外的构建目标不在追踪范围内：这类构建会自行解算，既不读取也不写入记录。

### 确定性

同一个 Avatar 的两次构建会得到相同结果：解算前参数按 Ordinal 排序，解算过程使用有序列表而非哈希集合，生成的参数名按确定规则派生（`FPC/<名称>`，冲突时依次追加 `_1`、`_2`……），且不依赖任何随机种子或时间戳。

### 生成的参数

全部带 `FPC/` 前缀：`SyncIndex<i>`（同步 Bool，批次索引）、`SyncDataNum<i>` 与 `SyncDataBool<i>`（共享同步槽位）、当解算结果需要多个批次时每个被压缩参数各一个非同步的锁存参数（单批次方案不需要锁存），以及一个仅存在于 Animator 的 `True`，用作无条件跳转的条件。

### 控制台输出

每次走到压缩器的构建都至少会向 NDMF 控制台写入一条记录，包括「无需压缩」。这是刻意为之：NDMF 只在本次构建产生了至少一条记录时才会刷新错误报告窗口，否则上一次构建的失败信息会一直留在屏幕上。

真正执行压缩时，报告会列出压缩前后的 bit 总数、被压缩的菜单类型、同步延迟区间、每批槽位数与批次数。如果解算器发现了一些「多半不需要同步」的参数（在所有控制器中完全未被使用、由 Contact 或 PhysBone 生成、由菜单中的瞬时按钮驱动、或只能通过 OSC 改变），会另外输出一条记录。

## 本地化

支持英文与简体中文，跟随 NDMF 的编辑器语言设置。文案位于 `Editor/Localization/*.json`；UI 元素带有 NDMF 的 `ndmf-tr` class，因此切换语言时 Inspector 会自动重新本地化。

## 代码来源

每个文件的首行注释都写明了它移植自哪个上游文件、以及做了哪些改动：

* `Editor/Editor-Avatars/**`、`Editor/Editor-Common/**` —— 来自 VRCFury（`com.vrcfury.vrcfury`），保持相同的相对路径。
* `Editor/ErrorReporting/**`、`Editor/Inspector/**`、`Editor/Localization/**`、`Editor/PluginDefinition/**`、`Editor/SyncParameterSequence/**`、`Runtime/**` —— 来自 Modular Avatar（`nadena.dev.modular-avatar`），保持相同的相对路径。它们都是**代码副本**，除 `MaSyncParameterSequenceDetector.cs` 外都不会调用 Modular Avatar，因此装不装 MA 都能正常工作。

VRCFury 的 legacy（bad-priority）压缩器分支未被移植。
