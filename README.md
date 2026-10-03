# 米莉拉：关键物品制作补丁 / Milira: Key Component Patch

> **📦 下载**：[最新版本（Release）](https://github.com/1579486875/MiliraKeyComponents/releases/latest) —— 下载 zip，解压后放进 `Mods\` 目录，排在米莉拉天空精灵之后。

把米莉拉系模组里**只能靠兑换、贸易或任务获得的关键件**接回生产链。

**软依赖设计：装多少，解锁多少。**
只装米莉拉本体就能用（15 条配方）；再装米帝拓展会自动解锁另外 6 条。

- packageId：`gnh.cn.cys.milirakeycomponents`
- 支持版本：1.6 ｜ 模组版本：1.6.4
- 必需依赖：米莉拉天空精灵（`Ancot.MiliraRace`，工坊 3256974620）
- 可选依赖：米莉拉派系：米莉拉帝国（`Ariandel.MiliraImperium`，工坊 3588393755）

---

## 一、软依赖是怎么实现的

用 `LoadFolders.xml` 的 `IfModActive` 做**文件夹级条件加载**：

```xml
<loadFolders>
  <v1.6>
    <li>.</li>                                                    <!-- 永远加载 -->
    <li IfModActive="Ariandel.MiliraImperium">MiliraImperium</li>  <!-- 装了米帝才加载 -->
  </v1.6>
</loadFolders>
```

好处是**配方和它的翻译待在同一个文件夹里**：没装米帝时两边一起不加载，
不会出现「翻译 key 找不到对应 Def」之类的报错。

---

## 二、配方总览（按生产力分层）

| 物品 | 市价 | 工作台 | 台面格数 | 材料种类 | 需要米帝 |
|---|---:|---|---:|---:|:---:|
| 谐振子 `Milira_ResonatorCore` | 100 | 米莉拉通用工作台 | 2×1 = 2 | 2 | ✗ |
| 职阶许可：士兵 `Milian_NamePlate_Pawn` | 60 | 米莉拉通用工作台 | 2×1 = 2 | 2 | ✗ |
| 职阶许可：骑士 `Milian_NamePlate_Knight` | 120 | 米莉拉通用工作台 | 2×1 = 2 | 2 | ✗ |
| 职阶许可：主教 `Milian_NamePlate_Bishop` | 120 | 米莉拉通用工作台 | 2×1 = 2 | 2 | ✗ |
| 职阶许可：战车 `Milian_NamePlate_Rook` | 200 | 米莉拉通用工作台 | 2×1 = 2 | 2 | ✗ |
| 职阶许可：国王 `Milian_NamePlate_King` | 1200 | 太阳熔炉 | 3×2 = 6 | 4 | ✗ |
| 职阶许可：王后 `Milian_NamePlate_Queen` | 1200 | 太阳熔炉 | 3×2 = 6 | 4 | ✗ |
| 太阳熔炉炉心模型 `Milira_FurnaceCoreModel` | 2000 | 太阳熔炉 | 3×2 = 6 | 4 | ✗ |
| 灵能核心 `Milira_PsionicCore` | 1500 | 引力织造器 | 3×3 = 9 | 6 | ✓ |
| 卡冈都亚核心 `Milira_GargantuaCore` | 1500 | 引力织造器 | 3×3 = 9 | 6 | ✓ |
| 金色米莉拉之羽 `Milira_Feather_gold` | 1500 | 引力织造器 | 3×3 = 9 | 3 | ✓ |

设计逻辑：**基础小件放基础工作台，高阶大件放高阶工作台**。
谐振子只是 100 银的部件，就放在只有 2 格台面的通用工作台上（因此只允许 2 种材料）；
炉心模型是太阳熔炉自己的核心组件，就放回太阳熔炉里铸造；
帝国货币与核心件属于米帝的物质重构范畴，统一挂在引力织造器上。

> **注：谐振子目前是「未实装内容的材料」。**
> 米莉拉本体里，谐振子的下游是「谐振仪」（`Milira_ResonatorPack_*`）——一件可部署的背包，落地后生成一台自带弹幕护盾、给范围内同职阶米莉安叠加「调谐 I~IV」（操作 +0.64、瞄准延迟 −0.9）的谐振器，30 秒后自毁；谐振子既是它的造价材料（4 个/件），也是它的装填弹药（1 个/发）。
> 但这整条线在 1.6 里是**注释状态**：`Apparel_Utility.xml` 214–372 行、`Buildings_Deploy.xml` 147–261 行、`Buildings_Cluster.xml` 339 行起都没生效，1.5 目录里的注释位置一模一样；扫过整个工坊目录也只有米莉拉本体引用这个 defName，`Milira.dll` 里没有任何代码引用它。
> 所以补丁**保留**了谐振子的配方：成本 132 略高于市价 100，不构成套利；它的意义是稳定货源（不必等商队），并且本体哪天放出谐振仪就能立刻接上。游戏内配方描述里也写明了这一点。

---

## ✦ 模组设置：简单模式

选项 → 模组设置 → **米莉拉：关键物品制作补丁**，里面有一个「简单模式」开关：

| 档位 | 材料 | 工作量 | 适用人群 |
|---|---|---|---|
| **关闭（默认）** | 100% | 100% | 作者配平档：材料成本 ≈ 产物市价，不产生套利 |
| **开启** | **50%** | **20%** | 偏好正义模组的玩家 |

实现要点（都在 `Source/MKC_Mod.cs` 里）：

- **只改数值，不做补丁**：程序集不含任何 Harmony 补丁，仅在启动与设置变更时直接改写 `DefDatabase` 里本模组配方的 `workAmount` 与材料数量，因此**不需要 Harmony 前置**。
- **绝不碰原模组**：改写范围严格限定在 `defName` 以 `GNH_Recipe_` 开头的配方。
- **首次 Apply 时缓存原始值**：之后所有改写都从缓存的原值算出，反复开关不会出现「越调越离谱」的累积误差。
- **勾选后立即生效**：`DoSettingsWindowContents` 检测到变化就重算，不必重开游戏。
- **三语界面**：设置文字走 `Keyed` 翻译（简中 / 繁中 / English）。
- **第四语言兜底**：RimWorld 的语言加载是**按当前语言的文件夹名精确匹配**的（见 `Verse.LoadedLanguage` 的构造逻辑），因此玩家若使用本模组未提供的语言（日语、俄语等），`Keyed` 不会被加载，`Translate()` 会原样返回 key 名。代码里对设置界面文字做了**英文兜底**，避免界面出现 `MKC_SimpleMode` 这类字样。（Def 内容不需要这层兜底——DefInjected 缺失时 RimWorld 会自动使用 `Defs` 里内建的英文。）

> ⚠️ 这也是本模组唯一的 C# 部分。之所以必须有程序集：RimWorld 的模组设置界面由 `Mod.DoSettingsWindowContents` 提供，纯 XML 无法实现。
## ✦ 批量生产配方

除「太阳熔炉炉心模型」这类一次性科技道具外，**每条单件配方都配了一个 `_Bulk` 批量版本**，玩法与原模组的「熔炼钢渣 x3」同类。

| 工作台 | 台面格数 | 批量倍率 | 原因 |
|---|---:|---:|---|
| 米莉拉通用工作台 | 2 | **x10** | 两种材料各 1 堆即可容纳（日凌晶 40/50、工业件 30/50） |
| 太阳熔炉 | 6 | **x3** | 国王占 4 格、王后占 5 格；再往上就会溢出 |
| 引力织造器 | 9 | 灵能核心 / 金色羽毛 **x5**、卡冈都亚核心 **x3** | 卡冈都亚核心要吃灵能核心，而后者堆叠上限为 1，每多产一个多占一格（x3 已占 8 格） |

**倍率不是拍脑袋定的**：批量会让材料用量同比放大，材料占用的格数随之上升（`ceil(数量 / 堆叠上限)`），必须留出台面余量。

**工作量**：批量工作量 = 单件工作量 × 倍率 × 0.85，约 15% 的规模折扣（与原模组 Bulk 配方的取法一致）。

| 批量配方 | 产出 | 工作量 |
|---|---:|---:|
| 打印谐振子 x10 | 谐振子 ×10 | 10000 |
| 刻蚀职阶许可：士兵 x10 | 士兵 ×10 | 8500 |
| 刻蚀职阶许可：骑士 x10 | 骑士 ×10 | 12500 |
| 刻蚀职阶许可：主教 x10 | 主教 ×10 | 12500 |
| 刻蚀职阶许可：战车 x10 | 战车 ×10 | 17000 |
| 刻蚀职阶许可：国王 x3 | 国王 ×3 | 17500 |
| 刻蚀职阶许可：王后 x3 | 王后 ×3 | 17500 |
| 织造灵能核心 x5 | 灵能核心 ×5 | 25000 |
| 织造卡冈都亚核心 x3 | 卡冈都亚核心 ×3 | 22500 |
| 织造金色米莉拉之羽 x5 | 金色米莉拉之羽 ×5 | 25000 |
## 三、配方明细

### ① 谐振子 ×1 ｜ 米莉拉通用工作台（无需米帝）
| 材料 | 数量 | 单价 | 小计 |
|---|---:|---:|---:|
| 日凌晶 `Milira_SolarCrystal` | 4 | 25 | 100 |
| 工业零部件 `ComponentIndustrial` | 1 | 32 | 32 |
| **合计** | **2 种 / 2 格** | | **≈132 银** |

- 前置研究：通用工作台 `Milira_UniversalPrinter` ｜ 工作量 1200

### ✦ 职阶许可（士兵 / 骑士 / 主教 / 战车 / 国王 / 王后）（无需米帝）

米莉安的身份识别芯片（`class permit`），原渠道只有米莉拉商队、教会贡品收集者与米帝工程院。
其中**士兵 / 骑士 / 主教 / 战车**被用作米莉安装备的建造材料，**国王 / 王后**是最高职阶的凭证。

| 许可 | 材料 | 成本 | 市价 | 倍率 | 工作台 |
|---|---|---:|---:|---:|---|
| 士兵 `Milian_NamePlate_Pawn` | 日凌晶 1 ＋ 工业零部件 1 | 57 | 60 | 0.95 | 米莉拉通用工作台 |
| 骑士 `Milian_NamePlate_Knight` | 日凌晶 2 ＋ 工业零部件 2 | 114 | 120 | 0.95 | 米莉拉通用工作台 |
| 主教 `Milian_NamePlate_Bishop` | 日凌晶 3 ＋ 工业零部件 1 | 107 | 120 | 0.89 | 米莉拉通用工作台 |
| 战车 `Milian_NamePlate_Rook` | 日凌晶 4 ＋ 工业零部件 3 | 196 | 200 | 0.98 | 米莉拉通用工作台 |
| 国王 `Milian_NamePlate_King` | 日盘钢 20 ＋ 日凌晶 15 ＋ 高级零部件 2 ＋ 黄金 20 | 1335 | 1200 | 1.11 | 太阳熔炉 |
| 王后 `Milian_NamePlate_Queen` | 日盘钢 15 ＋ 日凌晶 20 ＋ 高级零部件 2 ＋ 翡翠 20 | 1270 | 1200 | 1.06 | 太阳熔炉 |

- 前置研究：低阶四种为「通用工作台」`Milira_UniversalPrinter`；国王/王后为「太阳熔炉建造」`Milira_SunBlastFurnace_Build`
- 工作量：士兵 1000、骑士 1500、主教 1500、战车 2000、国王/王后 7000
- 数值取舍：
  - **士兵 → 战车**按职阶递进增加刻蚀复杂度，成本与市价基本持平
  - **主教**是灵能职阶，线路本身承担更多灵能负载，所以更吃日凌晶（3 枚）而少用机械件
  - **战车**是重装职阶，芯片线路最密、承载件最多
  - **国王**侧重军权统御 → 以锻钢与黄金为骨；**王后**侧重神权灵能 → 以晶石与翡翠为骨
### ② 太阳熔炉炉心模型 ×1 ｜ 太阳熔炉（无需米帝）
| 材料 | 数量 | 单价 | 小计 |
|---|---:|---:|---:|
| 日盘钢 `Milira_SunPlateSteel` | 40 | 18 | 720 |
| 日凌晶 `Milira_SolarCrystal` | 30 | 25 | 750 |
| 高级零部件 `ComponentSpacer` | 2 | 200 | 400 |
| 玻璃钢 `Plasteel` | 20 | 9 | 180 |
| **合计** | **4 种 / 6 格** | | **≈2050 银** |

- 前置研究：太阳熔炉建造 `Milira_SunBlastFurnace_Build` ｜ 工作量 8000
- 市价 2000 银，自制 ≈1.03 倍：**基本等值**，不产生套利空间

### ③ 灵能核心 ×1 ｜ 引力织造器（需要米帝）
| 材料 | 数量 | 单价 | 小计 |
|---|---:|---:|---:|
| 虚境凌晶 `Milira_ShroudCrystal` | 1 | 1800 | 1800 |
| 日凌晶 `Milira_SolarCrystal` | 15 | 25 | 375 |
| 零素 `Milira_Neutronium` | 10 | ≈63 | 630 |
| 暗物质 `Milira_DarkMatter` | 1 | 300 | 300 |
| 黄金 `Gold` | 15 | 10 | 150 |
| 翡翠 `Jade` | 10 | 5 | 50 |
| **合计** | **6 种 / 9 格** | | **≈3305 银** |

- 前置研究：物质编织 `Milira_Imperium_MatterWeaving` ｜ 工作量 6000

### ④ 卡冈都亚核心 ×1 ｜ 引力织造器（需要米帝）
| 材料 | 数量 | 单价 | 小计 |
|---|---:|---:|---:|
| 灵能核心 `Milira_PsionicCore` | 1 | 1500 | 1500 |
| 零素 `Milira_Neutronium` | 20 | ≈63 | 1260 |
| 暗物质 `Milira_DarkMatter` | 3 | 300 | 900 |
| 日盘钢 `Milira_SunPlateSteel` | 15 | 18 | 270 |
| 黄金 `Gold` | 15 | 10 | 150 |
| 翡翠 `Jade` | 10 | 5 | 50 |
| **合计** | **6 种 / 9 格** | | **≈4130 银** |

- 前置研究：暗物质约束 `Milira_Imperium_DarkMatterConstraint` ｜ 工作量 9000

### ⑤ 金色米莉拉之羽 ×1 ｜ 引力织造器（需要米帝）
| 材料 | 数量 | 单价 | 小计 |
|---|---:|---:|---:|
| 米莉拉之羽 `Milira_Feather` | 20 | 12 | 240 |
| 虚境灵尘 `MI_Thing_ShroudAsh` | 1 | 1500 | 1500 |
| 黄金 `Gold` | 30 | 10 | 300 |
| **合计** | **3 种 / 9 格** | | **≈2040 银** |

- 前置研究：灵能利刃 `Milira_ImperiumPsyBlade` ｜ 工作量 6000
- 选用**虚境灵尘**对应原描述「注入了微量超凡粉尘的米莉拉之羽」
- 市价 1500 银，自制 ≈1.36 倍：**刻意更贵**，否则它会变成印钞机

---

## 四、平衡原则：第一枚必须走原渠道

这个模组**不是一键解锁**，每一项都通过「工作台自身的门槛」保留了原获取渠道：

| 物品 | 第一枚怎么来 | 之后怎么自给 |
|---|---|---|
| 谐振子 | 米莉拉商队 | 通用工作台自制（谐振仪未实装，目前只作储备/贸易品） |
| 太阳熔炉炉心模型 | 米莉拉商队 / 米帝工程院商船 | 太阳熔炉里铸造 |
| 灵能核心 | 金色羽毛兑换 / 海盗据点任务 | 造出引力织造器后自制 |
| 卡冈都亚核心 | 金色羽毛兑换 / 海盗据点任务 | 同上 |
| 金色米莉拉之羽 | 帝国骑士头衔后定期领取 | 引力织造器自制（2040 > 1500，无套利） |

**两处自举闭环**（设计核心）：

```
贸易买 1 枚炉心模型 → 分析解锁「太阳熔炉建造」研究
   → 造出太阳熔炉 → 炉心模型本身也能铸造了（等价成本，不套利）

兑换 1 个灵能核心 → 造出引力织造器（造价含灵能核心 1）
   → 灵能核心 / 卡冈都亚核心 / 金色羽毛 全部可自制
```

---

## 五、配料格机制（为什么材料种类不能乱加）

反编译 RimWorld 本体可确认：

```csharp
// RimWorld.Building_WorkTable
public IEnumerable<IntVec3> IngredientStackCells => GenAdj.CellsOccupiedBy(this);
```

**配料位就是工作台自身占用的格子数**：通用工作台 2×1 → 只有 **2 格**；
太阳熔炉 3×2 → 6 格；引力织造器 3×3 → 9 格。

因此本模组的两条硬约束：

1. **材料种类 ≤ 台面格数**（否则材料会溢出到台面外）
2. **每种材料用量 ≤ 其堆叠上限**（保证每种只占 1 格）

核对结果：谐振子 2/2、炉心模型 4/6、灵能核心 6/9、卡冈都亚核心 6/9、金色羽毛 3/9 —— 全部合规。

> 供参考，本体在配料格不足时会 fallback 到候选格（`Toils_JobTransforms.SetTargetToIngredientPlaceCell`），
> 不会直接卡死，但会溢出到台面外，所以这里按硬约束设计。

---

## 六、新增的 Def

| defName | 类型 | 说明 | 加载条件 |
|---|---|---|---|
| `GNH_Recipe_ResonatorCore` | RecipeDef | 打印谐振子 | 总是 |
| `GNH_Recipe_NamePlate_Pawn` | RecipeDef | 刻蚀职阶许可：士兵 | 总是 |
| `GNH_Recipe_NamePlate_Knight` | RecipeDef | 刻蚀职阶许可：骑士 | 总是 |
| `GNH_Recipe_NamePlate_Bishop` | RecipeDef | 刻蚀职阶许可：主教 | 总是 |
| `GNH_Recipe_NamePlate_Rook` | RecipeDef | 刻蚀职阶许可：战车 | 总是 |
| `GNH_Recipe_NamePlate_King` | RecipeDef | 刻蚀职阶许可：国王 | 总是 |
| `GNH_Recipe_NamePlate_Queen` | RecipeDef | 刻蚀职阶许可：王后 | 总是 |
| `GNH_Recipe_ResonatorCore_Bulk` | RecipeDef | 打印谐振子 x10 | 总是 |
| `GNH_Recipe_NamePlate_Pawn_Bulk` | RecipeDef | 刻蚀职阶许可：士兵 x10 | 总是 |
| `GNH_Recipe_NamePlate_Knight_Bulk` | RecipeDef | 刻蚀职阶许可：骑士 x10 | 总是 |
| `GNH_Recipe_NamePlate_Bishop_Bulk` | RecipeDef | 刻蚀职阶许可：主教 x10 | 总是 |
| `GNH_Recipe_NamePlate_Rook_Bulk` | RecipeDef | 刻蚀职阶许可：战车 x10 | 总是 |
| `GNH_Recipe_NamePlate_King_Bulk` | RecipeDef | 刻蚀职阶许可：国王 x3 | 总是 |
| `GNH_Recipe_NamePlate_Queen_Bulk` | RecipeDef | 刻蚀职阶许可：王后 x3 | 总是 |
| `GNH_Recipe_PsionicCore_Bulk` | RecipeDef | 织造灵能核心 x5 | 装了米帝 |
| `GNH_Recipe_GargantuaCore_Bulk` | RecipeDef | 织造卡冈都亚核心 x3 | 装了米帝 |
| `GNH_Recipe_GoldenMiliraFeather_Bulk` | RecipeDef | 织造金色米莉拉之羽 x5 | 装了米帝 |
| `GNH_Recipe_FurnaceCoreModel` | RecipeDef | 铸造太阳熔炉炉心模型 | 总是 |
| `GNH_Recipe_PsionicCore` | RecipeDef | 织造灵能核心 | 装了米帝 |
| `GNH_Recipe_GargantuaCore` | RecipeDef | 织造卡冈都亚核心 | 装了米帝 |
| `GNH_Recipe_GoldenMiliraFeather` | RecipeDef | 织造金色米莉拉之羽 | 装了米帝 |

命名统一带 `GNH_` 前缀，已确认全库无重名。

---

## ✦ 兼容性推演与加固（极端场景）

这一节记录"大胆假设、小心验证"的极端场景推演结果：

| 极端场景假设 | 验证结果 |
|---|---|
| **只装米莉拉本体（无米帝）** | ✅ Base 组 8 条配方的 23 个引用**全部**来自本体无条件主模块 + 原版 |
| **本体 + 米帝，但无 Odyssey / Ideology / Anomaly** | ✅ Imperium 组引用的 18 个 Def 全在米帝**无条件主模块**，不受条件子模块影响 |
| **未安装 Biotech DLC** | ⚠️→✅ 6 种职阶许可定义在本体的 `1.6\Mods\Biotech\` 条件模块里。本体虽在 `About.xml` 强制依赖 Biotech，但这 6 条配方仍显式标注 `MayRequire="Ludeon.RimWorld.Biotech"` 兜底：缺失时**静默跳过**，而不是抛红字 |
| **旧版本模组目录/标识残留** | ✅ 已扫描：全库只有一个 `gnh.cn.cys.milirakeycomponents`。2026-10-01 按作者签名（GNH-CN-CYS）把 `taml.` / `TAML_` 统一改为 `gnh.cn.cys.` / `GNH_`，旧目录已清除，不会重复定义；改前的副本已在本地留存备份 |
| **加载顺序被排到本体/米帝之前** | ✅ `About.xml` 已声明 `loadAfter`；且 RimWorld 的 Def 加载是两阶段（先收集再 ResolveReferences），跨模组引用本身不依赖顺序 |
| **配方之间形成循环依赖** | ✅ 唯一的产物间引用是「卡冈都亚核心 ← 灵能核心」，单向无环，不存在自锁 |
| **经济套利（造出来卖给商人）** | ✅ 11 条配方的材料成本全部 **≥ 产物市场价格**，而商人收购价只有市价的 5~6 成 —— 卖了必亏 |
| **游戏语言不是简中/繁中/英文** | ✅ 会回落到 `Defs` 内建的英文 label，不会出现空白文本 |
| **游戏版本为 1.5** | ✅ `supportedVersions` 只声明 1.6，1.5 环境下不会加载 |
| **产物被后续配方复用形成雪球** | ✅ 只有卡冈都亚核心消耗灵能核心，其余配方都吃基础材料，无滚雪球路径 |

### 引用来源分层（全 36 个引用）

| 来源 | 数量 | 加载条件 |
|---|---:|---|
| 原版 `Data` | 7 | 无条件 |
| 本体无条件主模块 | 12 | 无条件 |
| 本体 Biotech 子模块 | 6 | 条件 = Biotech（本体强制依赖 + 本模组 MayRequire 兜底） |
| 米帝无条件主模块 | 11 | 无条件（装了米帝时） |
## 七、安装

1. 把整个 `米莉拉关键物品制作补丁-MiliraKeyComponentPatch` 文件夹放进
   `…\RimWorld\Mods\`
2. 在模组列表里启用它，位置放在「米莉拉天空精灵」**之后**
   （若装了米帝，也放在「米莉拉帝国」之后）
3. 旧存档可直接加装，无需新开档

---

## 八、怎么确认生效

- 装了米帝：建造 **米莉拉通用工作台**、**太阳熔炉**、**引力织造器**，
  添加清单搜索「打印」「铸造」「织造」，应能看到 5 条配方
- 只装本体：前两台工作台上应能看到「打印谐振子」「铸造太阳熔炉炉心模型」，
  且日志中**不会**出现 `GNH_Recipe_PsionicCore` 之类的缺失报错
- 正常情况游戏日志（`Player.log`）里关于 `GNH_Recipe_` 的红字应为 0 条

---

## 九、想调整

改这两个文件即可，存盘后重进游戏生效：

- `Defs\MiliraKeyComponents_Base.xml` —— 本体部分的 2 条配方
- `MiliraImperium\Defs\MiliraKeyComponents_Imperium.xml` —— 米帝部分的 3 条配方

可调项：`<count>`（材料量）、`<workAmount>`（工作量）、`<researchPrerequisite>`（研究前置）、
`<recipeUsers>`（工作台）。

> 注意两条红线：**材料种类别超过该工作台的台面格数**，**单项用量别超过它的堆叠上限**（见第五节）。

---

## 十、文件结构

```
米莉拉关键物品制作补丁-MiliraKeyComponentPatch/
├── About/
│   └── About.xml
├── LoadFolders.xml                                    ← 条件加载规则
├── Common/                                            ← 总是加载（只需本体）
│   ├── Assemblies/
│   │   └── MiliraKeyComponents.dll                    ← 程序集（仅模组设置用）
│   ├── Defs/
│   │   ├── MiliraKeyComponents_Base.xml               ← 单件配方 8 条
│   │   └── MiliraKeyComponents_Bulk.xml               ← 批量配方 7 条
│   └── Languages/
│       ├── ChineseSimplified/DefInjected/RecipeDef/MiliraKeyComponents_Base.xml
│       ├── ChineseSimplified/DefInjected/RecipeDef/MiliraKeyComponents_Bulk.xml
│       ├── ChineseSimplified/Keyed/MKC_Keys.xml
│       ├── ChineseTraditional/…（同上三件）
│       └── English/…（同上三件）
├── MiliraImperium/                                    ← 仅装了米帝时加载
│   ├── Defs/
│   │   ├── MiliraKeyComponents_Imperium.xml           ← 单件配方 3 条
│   │   └── MiliraKeyComponents_ImperiumBulk.xml       ← 批量配方 3 条
│   └── Languages/…（三语各两件）
├── Source/
│   ├── MiliraKeyComponents.csproj                     ← 编译配置（net48）
│   └── MKC_Mod.cs                                     ← 设置界面 + 数值调节器
└── README.md
```

> 编译：`dotnet build "Source\MiliraKeyComponents.csproj" -c Release -p:RimWorldDir="<RimWorld 目录>"`
> 产物自动落到 `Common\Assemblies\`——**必须放在 LoadFolders 列出的文件夹下**，否则不会被加载。

> ⚠️ `LoadFolders.xml` 中**不要**用 `<li>.</li>` 表示根目录。反编译 `ModContentPack.InitLoadFolders` 可知：只有 `/` 或 `\` 会被解析成根目录（`folderName = ""`），写 `.` 会得到 `…\ModName\.` 这种非规范路径，可能导致该目录下的 `Languages` 不被正确合并（表现为配方只显示英文）。本模组改用显式的 `Common/` 子文件夹彻底规避该问题。

> ⚠️ `LoadFolders.xml` 中**不要**用 `<li>.</li>` 表示根目录。反编译 `ModContentPack.InitLoadFolders` 可知：只有 `/` 或 `\` 会被解析成根目录（`folderName = ""`），写 `.` 会得到 `…\ModName\.` 这种非规范路径，可能导致该目录下的 `Languages` 不被正确合并（表现为配方只显示英文）。本模组改用显式的 `Common/` 子文件夹彻底规避该问题。

---

## 开发与构建

本仓库同时是该模组的源码仓库。

**环境要求**：.NET SDK（目标框架 net48）、RimWorld 1.6

**编译**：不需要把游戏 DLL 复制进仓库 —— 用参数指向你自己的游戏安装目录即可：

```powershell
dotnet build "Source\MiliraKeyComponents.csproj" -c Release `
    -p:RimWorldDir="<你的 RimWorld 安装目录>"
```

产物直接输出到 `Common\Assemblies\MiliraKeyComponents.dll`（仓库内已附编译好的 dll）。

**目录结构**

| 路径 | 说明 |
| --- | --- |
| `Source/` | C# 源码与工程文件 |
| `Common/` | 主模块：Defs、三语翻译、贴图、程序集 |
| `MiliraImperium/` | 米莉拉帝国拓展的附加配方（软依赖） |
| `LoadFolders.xml` | 条件加载：装了帝国拓展才加载 `MiliraImperium/` |
| `About/` | 模组元数据与预览图 |

**许可**：[MIT](LICENSE)

**创意工坊**：[米莉拉：关键物品制作补丁（3812443768）](https://steamcommunity.com/sharedfiles/filedetails/?id=3812443768)

**依赖**：[米莉拉天空精灵](https://steamcommunity.com/sharedfiles/filedetails/?id=3256974620)（`Ancot.MiliraRace`）；装了 [米莉拉帝国拓展](https://steamcommunity.com/sharedfiles/filedetails/?id=3588393755) 会自动多出 3 种配方。
