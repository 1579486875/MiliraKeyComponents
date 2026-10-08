using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace MiliraKeyComponents
{
    // ==========================================================================
    // 这个文件负责「简单模式」的第二半：让关键物品也能在**原版工作台**上制作，
    // 代价是在那边做要花 2.5 倍的时间。
    //
    // （第一半是材料减半、工时降到 20%，在 MKC_RecipeTuner 里，与本文件无关。）
    // ==========================================================================
    //
    // 【为什么必须用 Harmony，不能用改 Def 的老办法】
    //
    // 本模组其它地方都是直接改 Def 的字段（recipe.workAmount = ...），从来不碰方法。
    // 但这一件事改不了：
    //
    //     Verse.RecipeDef.workAmount 是**配方自己的字段**，
    //     一个配方只有一个 workAmount，跟它在哪台工作台上做完全无关。
    //
    // 也就是说，只要那个数字是 7000，那么在米莉拉太阳熔炉上是 7000，
    // 换成原版锻造台也还是 7000 —— 没有任何办法只在其中一边延长。
    //
    // 那真正算工时的地方在哪？反编译 RimWorld 1.6 可以看到：
    //
    //     // RimWorld.Bill —— 注意它是 virtual
    //     public virtual float GetWorkAmount(Thing thing = null)
    //     {
    //         return recipe.WorkAmountTotal(thing);
    //     }
    //
    // `this` 就是那个 Bill，而 Bill 身上带着两张关键的表：
    //
    //     RimWorld.Bill.billStack          （BillStack）
    //     RimWorld.BillStack.billGiver     （IBillGiver，实际就是那台工作台）
    //
    // 所以在这一层我们**能同时看到「哪个配方」和「哪台工作台」** ——
    // 这正是区分二者所需的全部信息。给这个方法挂一个 Postfix，
    // 判断工作台是不是原版的，是就把结果乘上延长系数。
    //
    // 【为什么只给原版工作台延长】
    //
    // 米莉拉自己的工作台（通用工作台 / 太阳熔炉 / 引力织造器）是这条配方的「正路」，
    // 保持作者设计的原始工时；原版工作台是简单模式额外给的一条**捷径**
    //（不用先造出米莉拉的那几台机器），所以用时间成本来平衡，这个设定由玩家选择。
    //
    // 【延长多少】
    //
    // WorkMultiplier = 2.5。这个数字是刻意放在常量里的：想调只改这一行，
    // 不用翻别的代码，也不用重新理解这套逻辑。
    //
    // 【它会不会影响别的东西】
    //
    // 不会。这个 Postfix 有三道早退，任何一道不满足就直接原样放行：
    //   1. 设置对象不存在（游戏刚启动、设置还没建好）→ 放行；
    //   2. 没开简单模式 → 放行（平衡模式下这条途径根本不存在，见 MKC_RecipeTuner）；
    //   3. 工作台不是我们认识的那几台原版工作台 → 放行。
    // 所以它**只**影响「简单模式 + 本模组自己的配方 + 原版工作台」这一个交集，
    // 原版配方、别的模组的配方、米莉拉工作台，全都不受任何影响。
    // ==========================================================================
    internal static class MKC_VanillaBench
    {
        /// <summary>
        /// 在原版工作台上制作时，工时延长到几倍。2.5 就是「原本 7000 变成 17500」。
        /// 想调整手感只改这一个数字即可。
        /// </summary>
        public const float WorkMultiplier = 2.5f;

        /// <summary>
        /// 认定的「原版工作台」，用 defName 比对。
        ///
        /// 为什么用名字而不是直接引用 ThingDefOf：这个类在**静态构造阶段**就会被用到，
        /// 而 ThingDefOf 的字段要等 Def 全部加载完才填好，那时候可能还是 null。
        /// 用名字比对既没有时序问题，也不会因为原版改了引用方式而崩。
        ///
        /// 这两台是怎么选出来的（尺寸来自原版 ThingDef 的 size）：
        ///   · TableMachining   机械加工台，(3,1) = 3 格
        ///   · FabricationBench 精密装配台，(5,2) = 10 格
        /// 配方的材料**种类数**决定了需要几格（每种材料占一格），
        /// 所以 2~3 种材料的配方挂机械加工台，4~6 种的挂精密装配台。
        /// 具体哪条配方挂哪台，见 MKC_RecipeTuner.PickVanillaBench。
        /// </summary>
        private static readonly HashSet<string> VanillaBenchDefNames = new HashSet<string>
        {
            "TableMachining",
            "FabricationBench",
        };

        /// <summary>
        /// 本模组专用的 Harmony 实例。
        ///
        /// ID 取「packageId + 用途」这种形式，与其它模组的 Harmony 实例互相隔离：
        /// 将来若要单独卸载本补丁，认准这一个 ID 就能准确摘掉，不会误伤别人。
        /// </summary>
        private static readonly Harmony HarmonyInstance =
            new Harmony("gnh.cn.cys.milirakeycomponents.vanillabench");

        /// <summary>本补丁是否已经装过。Harmony 补丁是叠加的，装两次会执行两层。</summary>
        private static bool installed;

        /// <summary>判断这台工作台是不是我们认定的原版工作台。传入 null 一律返回 false。</summary>
        internal static bool IsVanillaBench(Thing bench)
        {
            if (bench == null)
            {
                return false;
            }
            ThingDef def = bench.def;
            return def != null && VanillaBenchDefNames.Contains(def.defName);
        }

        /// <summary>
        /// 安装 Harmony 补丁。由 MKC_Bootstrap 的静态构造函数调用 ——
        /// [StaticConstructorOnStartup] 由 CLR 保证一个进程只跑一次，
        /// 所以这里的 installed 守卫其实是第二道保险（成本只有一次 bool 判断）。
        /// </summary>
        internal static void Install()
        {
            if (installed)
            {
                return;
            }

            try
            {
                MethodInfo target = AccessTools.Method(typeof(Bill), "GetWorkAmount");
                if (target == null)
                {
                    // 原版改了方法名或签名。**不置位**，留着下次再试，
                    // 并且打 Error —— 静默失败会让「原版工作台照样 2.5 倍」这件事
                    // 变成玩家眼里的「说好的延长呢」，却没有任何线索。
                    Log.Error("[MKC] Could not find Verse.Bill.GetWorkAmount; "
                        + "the vanilla-workbench work penalty is NOT active this session.");
                    return;
                }

                MethodInfo postfix = AccessTools.Method(typeof(MKC_VanillaBench), nameof(Postfix));
                HarmonyInstance.Patch(target, null, new HarmonyMethod(postfix), null, null);

                installed = true;

                // 装完读回补丁表：这是「装上了」与「装对了」之间的那次验证。
                // 本项目在别的模组上吃过亏 —— 补丁表里有，不等于运行时真的被调用。
                Patches info = Harmony.GetPatchInfo(target);
                Log.Message("[MKC] Vanilla workbench work penalty installed. "
                    + "Bill.GetWorkAmount postfixes = " + (info != null ? info.Postfixes.Count : -1)
                    + ", multiplier = " + WorkMultiplier + "x.");
            }
            catch (Exception ex)
            {
                Log.Error("[MKC] Failed to install the vanilla-workbench work penalty "
                    + "(recipes still work, just without the work penalty): " + ex);
            }
        }

        /// <summary>
        /// 挂在 Verse.Bill.GetWorkAmount 上的后置补丁。
        ///
        /// Harmony 按参数名把东西注入进来：
        ///   __instance —— 被补的那个对象本身（这里就是那个 Bill）
        ///   __result   —— 原方法的返回值（这里就是工时），加了 ref 才能改它
        /// 这两个名字是 Harmony 的约定，**不能改**，改了注入不进去。
        /// </summary>
        internal static void Postfix(Bill __instance, ref float __result)
        {
            // 四道早退，任何一道不满足就原样放行。顺序按「最便宜 + 最可能命中」排。
            if (MKC_Mod.Settings == null || !MKC_Mod.Settings.simpleMode)
            {
                return;   // 没开简单模式：这条途径本来就不该存在
            }

            if (__instance == null)
            {
                return;
            }

            // ★ 这一道**绝不可少**：只处理本模组自己加的那 21 条配方。
            //
            // 为什么（2026-10-04 复审查出的 P0）：
            // Bill.GetWorkAmount 是**所有配方共用**的方法，而机械加工台与精密装配台
            // 是原版最常用的两张制作台 —— 实测这两台的 recipeUsers 加起来有六百多条配方，
            // 其中**原版的就有 95 条**（防弹夹克、动力装甲、高级头盔、防毒面具、零部件……）。
            // 少了这一道判断，简单模式一开，那些**完全无关**的配方工时会被一起乘 2.5，
            // 等于把整个游戏的手工速度砍掉一大半 —— 而本文件开头还写着
            //「原版配方、别的模组的配方，全都不受任何影响」，那就成了彻底的谎话。
            //
            // IsOurRecipe 核对「名字以 GNH_Recipe_ 开头」+「来源模组就是本模组」两条，
            // 与 MKC_RecipeTuner 改配方时用的是同一把尺子。判定口径必须一致，
            // 否则会出现「改了的不延长、没改的反而延长」这种最糟的组合。
            if (!MKC_RecipeTuner.IsOurRecipe(__instance.recipe))
            {
                return;
            }

            // billStack / billGiver 是 RimWorld 的公开字段。
            // 极少数 Bill 可能没有工作台（例如挂在角色身上的），拿不到就放行。
            BillStack stack = __instance.billStack;
            Thing bench = (stack != null) ? (stack.billGiver as Thing) : null;
            if (!IsVanillaBench(bench))
            {
                return;   // 米莉拉自己的工作台，或者别的什么，一律不动
            }

            // 到这里才真正延长。注意做的是乘法而不是赋值 ——
            // 万一将来还有别的模组也在这个方法上加了 Postfix，
            // 乘法能跟它们叠加，而赋值会把别人的效果抹掉。
            __result *= WorkMultiplier;
        }
    }
}
