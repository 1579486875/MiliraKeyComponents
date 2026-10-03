using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace MiliraKeyComponents
{
    /// <summary>
    /// 这个模组整体就干一件事：把名字以 GNH_Recipe_ 开头的那一批「米莉拉关键物品」配方，
    /// 材料数量和工作量改小，让玩家能自己造出来，而不是只能靠交易或者抢。
    ///
    /// 这里放的是它的设置数据，里面只有一个开关：简单模式。
    ///
    /// 为什么要有这个开关：不开的时候，数值是按原模组的经济水平配好的
    /// （做一件东西要的材料大概顶得上成品的市价），属于正常难度；
    /// 但有些玩家就想快点把关键件拿到手，所以额外给一个更省料的档位。
    /// 这个开关只会动本模组自己新增的那批配方 —— 判定条件是「defName 以 GNH_Recipe_ 开头」
    /// 且「该配方的来源模组就是本模组」两条同时成立（见 MKC_RecipeTuner.IsOurRecipe）。
    /// 只按名字前缀判断是不够的：万一将来有别的模组也用了这个前缀，就会被本模组一起改掉。
    /// 米莉拉本体、米莉拉帝国原有的任何 Def 都不会被碰到。
    /// </summary>
    public class MKC_Settings : ModSettings
    {
        /// <summary>是否打开简单模式：打开后材料只要原来的一半，工作量降到原来的 20%。</summary>
        public bool simpleMode = false;

        public override void ExposeData()
        {
            // 这里的 false 是「设置文件里压根没有这个字段」时用的默认值。
            // 顺带说明：这类设置是存在 Config\Mod_<模组文件夹名>_MKC_Mod.xml 里的，
            // 不是存在存档里 —— 游戏里所有 ModSettings 都是这么存的。
            // 第一次装本模组时这个文件还不存在，游戏就按 false（不开启）处理，
            // 也就是安全地回到平衡档，不会因为读不到设置就出问题。
            Scribe_Values.Look(ref simpleMode, "simpleMode", false);
        }
    }

    /// <summary>
    /// 模组入口。游戏加载模组时会创建这个类，它的活儿只有两件：
    /// 把设置数据读出来，以及画出设置界面上的那个勾选框。
    /// 真正的数值改写不在这个类里做，统一交给 MKC_RecipeTuner。
    /// </summary>
    public class MKC_Mod : Mod
    {
        public static MKC_Settings Settings;

        public MKC_Mod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<MKC_Settings>();
        }

        /// <summary>设置界面左侧列表里显示的分类名（就是模组名）。</summary>
        public override string SettingsCategory()
        {
            return Tr("MKC_SettingsCategory", "Milira: Key Component Crafting");
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);

            // 万一设置对象没建起来（理论上不会发生），也要把窗口正常画完再退出。
            // 不能直接 return —— IMGUI 要求 Begin 和 End 必须成对出现，
            // 少调一次 End 会让整个设置界面出问题。
            if (Settings == null)
            {
                listing.Label(Tr("MKC_SettingsUnavailable", "Settings unavailable; please restart the game."));
                listing.End();
                return;
            }

            bool before = Settings.simpleMode;
            listing.CheckboxLabeled(
                Tr("MKC_SimpleMode", "Simple Mode"),
                ref Settings.simpleMode,
                Tr("MKC_SimpleModeDesc", "Halves ingredient requirements and drops work amount to 20%."));

            listing.Gap(6f);
            listing.Label(Tr("MKC_CurrentEffect", "Current: {0}").Formatted(
                Settings.simpleMode
                    ? Tr("MKC_On", "Simple Mode enabled (half ingredients, 20% work)")
                    : Tr("MKC_Off", "Balanced (author's default tuning)")));

            listing.End();

            // 玩家把勾选框拨动过（和刚打开窗口时不一样）就立刻重算一遍配方数值，
            // 这样不用退出游戏重进就能看到效果。
            //
            // 这里包一层 try/catch 是必要的：这个方法是**每帧**都会被调用的，
            // 设置窗口开着的时候一秒钟要跑几十次。万一里面抛了异常，
            // 游戏会一帧一条错误日志地刷屏，比「设置没生效」糟糕得多。
            if (before != Settings.simpleMode)
            {
                try
                {
                    MKC_RecipeTuner.Apply();
                }
                catch (System.Exception ex)
                {
                    Verse.Log.Error("[MKC] Applying settings from the settings window failed.\n" + ex);
                }
            }
        }

        public override void WriteSettings()
        {
            base.WriteSettings();
            // 关掉设置窗口时再算一遍保底：让「记在设置文件里的开关」和「实际生效的配方数值」
            // 处在同一个状态上，不会出现一个说开了、另一个还是老数值的情况。
            // 同样包一层 catch —— 这里抛异常会打断游戏的设置保存流程，波及别的模组。
            try
            {
                MKC_RecipeTuner.Apply();
            }
            catch (System.Exception ex)
            {
                Verse.Log.Error("[MKC] Applying settings while saving failed.\n" + ex);
            }
        }

        /// <summary>
        /// 按 key 取一句翻译文本。这个方法的承诺是：不管翻译文件是什么样，
        /// 它都会还回一段人能看懂的话，不会甩给你一串看不懂的代码。
        ///
        /// 先看游戏自带的 key.Translate()（内部就是 Verse.Translator.Translate）：
        /// 它自己其实已经会兜底了 —— 当前语言（比如日语）的 Keyed 文件里找不到这个 key，
        /// 它还会去 defaultLanguage（英语）里再翻一遍
        /// （代码上就是 TryTranslate 失败之后，接着调
        /// LanguageDatabase.defaultLanguage.TryGetTextFromKey）。
        /// 咱这个模组简中、繁中、英文三种语言都写全了，所以玩日语、俄语的玩家
        /// 看到的是英文原文，不会看到 "MKC_SimpleMode" 这种光秃秃的 key。
        ///
        /// 那这一层包装还有啥用？只有「连英语里都缺这个 key」的时候才轮得到它 ——
        /// 这时就拿调用处写死的英文文本（englishFallback）顶上，当作最后一道保险，
        /// 免得界面上冒出一串谁也看不懂的东西。说白了就是一层多余的保险，正常用不上。
        ///
        /// 注意：Def 里的内容（配方的名字、描述）不需要这种保险 ——
        /// 如果某个语言的 DefInjected 翻译文件里缺了这一条，
        /// 游戏会自动退回去显示 Def 文件里写死的英文，不会显示成一串奇怪代码。
        /// </summary>
        private static string Tr(string key, string englishFallback, params object[] args)
        {
            string text = key.Translate();
            if (string.IsNullOrEmpty(text) || text == key)
            {
                text = englishFallback;
            }
            // 这里用的是 .NET 自带的 string.Format：它会把文本里的 {0} 换成后面传进来的第 0 个参数。
            // 游戏自带的 Formatted 扩展也认 {0} 这种编号占位符（内部 GrammarResolverSimple 会先用
            // int.TryParse 试着把 {0} 里的 0 读成数字，读成了就取 argsObjects[第几号] 那个参数），
            // 所以「能把参数填进 {0}」这件事上两种写法是一样的；
            // 但 Formatted 还会多做一步当前语言的后处理（Find.ActiveLanguageWorker.PostProcessed），
            // 而且参数填不进去时它只记一条错误日志、不会抛异常，因此两者并不完全等价。
            // 说明：这个 args 参数目前没有任何调用方会用到 ——
            // 本文件里所有调用点都不传参数（唯一带 {0} 的那处设置界面文案走的是 Formatted，
            // 因为那里是链式调用、接着往下写更顺手）。
            // 留着它是为了以后真需要「先取翻译、再自己填参数」时直接能用，不必再改方法签名
            //（也正因为如此，这里不写死调用点个数和行号 —— 那种数字改几次代码就过期了）。
            return (args == null || args.Length == 0) ? text : string.Format(text, args);
        }
    }

    /// <summary>
    /// 真正干活的类：负责把配方里的材料数量和工作量改掉。
    ///
    /// 关键点（也是这模组不会越改越乱的原因）：第一次调用 Apply 的时候，
    /// 会先把每个配方当时的原始数值抄一份存起来；以后每次改写，都是拿「最初抄下来的那一份」
    /// 重新算，而不是在当前的数值上再乘一遍。
    /// 所以你把简单模式的开关拨来拨去一百次，结果也和只拨一次一模一样，
    /// 不会出现「越调越小、越调越离谱」的情况。
    /// </summary>
    public static class MKC_RecipeTuner
    {
        /// <summary>简单模式下工作量打几折：0.2 就是乘以 0.2，也就是降到原来的 20%。</summary>
        private const float WorkFactor = 0.2f;

        /// <summary>简单模式下材料数量的系数：0.5 就是减半。</summary>
        private const float IngredientFactor = 0.5f;

        /// <summary>
        /// 只处理名字以 GNH_Recipe_ 开头的配方。这是第一道筛子；
        /// 第二道筛子是下面那个「来源模组必须是本模组」的检查 —— 两道都过才动手。
        /// </summary>
        private const string RecipePrefix = "GNH_Recipe_";

        /// <summary>
        /// 本模组自己的 packageId，必须与 About\About.xml 里写的一模一样。
        /// 用它确认「这个配方确实是我们自己加的」，免得将来有别的模组
        /// 也用了 GNH_Recipe_ 这个前缀时，被本模组顺手改掉。
        /// </summary>
        private const string OwnPackageId = "gnh.cn.cys.milirakeycomponents";

        /// <summary>每个配方的原始工作量，键是配方的 defName。第一次 Apply 时抄下来，之后一直当底稿用。</summary>
        private static readonly Dictionary<string, float> originalWork = new Dictionary<string, float>();
        /// <summary>每个配方每种材料的原始数量。注意是个列表：一个配方可能同时要好几种材料。</summary>
        private static readonly Dictionary<string, List<float>> originalCounts = new Dictionary<string, List<float>>();
        /// <summary>
        /// 抄底稿时那批配方的 Def 实例本身。用途见 BaselineIsStale：
        /// 开发者模式热重载 Defs 之后，同一个配方会换成**新的** Def 实例，
        /// 光看 defName 是看不出来的，得比对象本身才能发现底稿已经过期。
        /// </summary>
        private static readonly Dictionary<string, RecipeDef> capturedDefs = new Dictionary<string, RecipeDef>();

        /// <summary>
        /// 抄底稿时，每个配方里各材料的 defName 顺序。用途见 IngredientsMatch：
        /// 万一有别的模组在启动之后往配方里插了材料，只按位置（第 0 个、第 1 个……）
        /// 把老数量贴回去就会贴错东西，所以每次改写前都先对一遍名字。
        /// </summary>
        private static readonly Dictionary<string, List<string>> capturedIngredientNames = new Dictionary<string, List<string>>();

        /// <summary>
        /// 「原始数值已经抄过了」的标记。抄成功了才把它立起来（见 Apply），
        /// 这样万一抄的过程出错抛了异常，标记还是 false，下次还有机会重来。
        /// </summary>
        private static bool captured;

        /// <summary>
        /// 最近一次 Apply 里**真正改到了**几个配方（匹配到、但被跳过的不算）。
        /// 专门留着给启动日志报数用 —— 有了它，日志里才能看出「到底改到了几个配方」，
        /// 而不是只有一句「跑过了」。
        /// </summary>
        public static int LastAppliedCount { get; private set; }

        /// <summary>最近一次 Apply 的时候，玩家是不是开着简单模式。同样只给启动日志报数用。</summary>
        public static bool LastWasSimpleMode { get; private set; }

        /// <summary>
        /// 判断一个配方是不是「本模组自己加的」。两个条件缺一不可：
        /// 名字以 GNH_Recipe_ 开头，并且它的来源模组就是本模组。
        /// 只查名字是不够的 —— 前缀这种东西别的模组也可能撞上。
        /// </summary>
        private static bool IsOurRecipe(RecipeDef recipe)
        {
            if (recipe.defName == null) return false;
            if (!recipe.defName.StartsWith(RecipePrefix)) return false;

            ModContentPack pack = recipe.modContentPack;
            if (pack == null) return false;   // 理论上不会为 null，这儿只是保险

            // 大小写不敏感地比：游戏内部会把 packageId 规范化，两种写法都能对上。
            return string.Equals(pack.PackageId, OwnPackageId, System.StringComparison.OrdinalIgnoreCase);
        }

        public static void Apply()
        {
            List<RecipeDef> recipes = DefDatabase<RecipeDef>.AllDefsListForReading
                .Where(IsOurRecipe)
                .ToList();

            if (recipes.Count == 0)
            {
                LastAppliedCount = 0;

                // 一个都没找到时，把「为什么」一次问清楚，免得来回猜。
                // 这一段只在出问题时才会执行，正常游戏不会看到它。
                try
                {
                    int total = DefDatabase<RecipeDef>.AllDefsListForReading.Count;
                    int byPrefix = 0;
                    string sample = "";
                    foreach (RecipeDef r in DefDatabase<RecipeDef>.AllDefsListForReading)
                    {
                        if (r.defName == null || !r.defName.StartsWith(RecipePrefix)) continue;
                        byPrefix++;
                        if (sample.Length == 0)
                        {
                            string pid = r.modContentPack == null ? "<null>" : r.modContentPack.PackageId;
                            sample = r.defName + " (来源模组=" + pid + ")";
                        }
                    }
                    Verse.Log.Warning("[MKC] diag: RecipeDef 总数=" + total
                        + "，以 GNH_Recipe_ 开头的=" + byPrefix
                        + "，本模组期望的 packageId=" + OwnPackageId
                        + (sample.Length > 0 ? "；样例：" + sample : "；一个前缀匹配的配方都没有"));
                }
                catch (System.Exception ex)
                {
                    Verse.Log.Warning("[MKC] diag failed: " + ex.Message);
                }

                return; // 一个都没找到 —— 通常是模组被禁用了，或者配方表（Def）还没读进来。
                        // 这种时候安静退出就好：没东西可改，也不必在这里刷日志；
                        // 启动的那一次由 MKC_Bootstrap 统一报一声（见本文件末尾）。
            }

            // 抄底稿的时机：第一次进来抄一份；或者发现底稿已经过期，就丢掉重抄。
            //
            // 抄的时候是先抄完、再立 captured 标记的（见 Recapture），
            // 这样万一抄到一半抛了异常，标记还是 false，下次调用还有机会重来，
            // 不会落得「以后永远没有原始值可用、配方再也改不动」的地步。
            //
            // 怎么算「过期」：开发者模式下热重载 Defs 时，游戏会把整个配方表清空重建，
            // 同一个配方变成全新的对象、数值也回到 XML 原值；而静态构造函数
            // CLR 只保证跑一次，不会自动再 Apply 一次。这时候要是还抱着旧底稿不放，
            // 玩家一碰设置开关就会把过期的数值写回去。判断方式见 BaselineIsStale。
            if (!captured || BaselineIsStale(recipes))
            {
                Recapture(recipes);
            }

            // 看看玩家有没有勾上简单模式。设置对象还没建好（Settings 为 null）时，就当成没勾。
            bool simple = MKC_Mod.Settings != null && MKC_Mod.Settings.simpleMode;

            int applied = 0;
            int skipped = 0;

            foreach (RecipeDef recipe in recipes)
            {
                // 下面两次查表都必须查得到，才轮到去改这个配方。
                // 因为原始工作量（originalWork）和原始材料数（originalCounts）是成对存进去的，
                // 要是只拿到其中一张就动手改，会留下个半拉子状态：
                // 工作量已经按简单模式变小了，材料却还是老数目 —— 那样的配方数值是乱的。
                // 所以宁可整条跳过。
                // （说明：这两张表在 CaptureOriginals 里永远是一起写入的，正常不可能只缺一张；
                //   这里纯粹是保险，免得以后改了抄写逻辑时漏掉这条。）
                if (!originalWork.TryGetValue(recipe.defName, out float work))
                {
                    skipped++; continue;   // 正常走不到这儿；真走到了就保守跳过，不抛异常
                }
                if (!originalCounts.TryGetValue(recipe.defName, out List<float> counts))
                {
                    skipped++; continue;
                }

                // 材料清单跟抄底稿时对不上了（个数变了、或者顺序 / 种类变了），也整条跳过。
                // 硬改的话会把 A 材料的数量贴到 B 材料头上，那种错误没有任何提示，
                // 玩家只会看到「做东西要的料莫名其妙变了」，极难排查。
                if (!IngredientsMatch(recipe, counts.Count))
                {
                    skipped++; continue;
                }

                // 工作量：简单模式就乘 0.2。
                //
                // 但要注意 work 可能是**负数**，那不是「没填」，而是游戏的一个约定：
                // workAmount 小于 0 表示「工作量跟着产物走」（取产物的 WorkToMake 属性）。
                // 对这种配方做乘法会先得到 -0.2，再被 Mathf.Max 抬成 1 ——
                // 等于把「跟着产物算」悄悄改成「瞬间完成」，是实打实的破坏。
                // 所以负数原样保留，不碰它。
                // 不是简单模式就直接写回抄下来的原始值，也就是恢复成原来的样子。
                recipe.workAmount = simple
                    ? (work < 0f ? work : Mathf.Max(1f, work * WorkFactor))
                    : work;
                for (int i = 0; i < counts.Count; i++)
                {
                    float original = counts[i];

                    // 简单模式的目标是「变少」，所以：先乘系数，再向上取整
                    // （1.2 会变成 2，免得界面上出现「1.5 个钢」这种难看的数），
                    // 然后至少留 1，最后和原值取较小的那一个。
                    //
                    // 最后那一步 Mathf.Min 是为了防住「减半反而变多」的怪事：
                    // 原值 0.5 的时候，0.5 × 0.5 = 0.25，向上取整会变成 1 —— 比原来还大。
                    // 取了 Min 之后就还是 0.5。
                    //
                    // 注意这一切只在简单模式下发生；平衡模式是原样写回抄下来的值，
                    // 包括本来就是小数的那些，不会被取整。
                    float scaled = Mathf.Min(original,
                        Mathf.Max(1f, Mathf.Ceil(original * IngredientFactor)));
                    recipe.ingredients[i].SetBaseCount(simple ? scaled : original);
                }

                // 这一行清的是 Def 的「名字缓存」，跟配方数值没关系。
                // 游戏为了省事，会把 Def 首字母大写的名字（LabelCap）算一次就存着；
                // Verse.Def.ClearCachedData 这个方法从头到尾只干一件事 ——
                // 把 cachedLabelCap 清成 null（核对过，RecipeDef 并没有自己改写这个方法）。
                //
                // 那配方数值为什么不用做类似的「刷新」？因为它压根没被缓存过：
                // 制作清单、材料提示这些界面是在每次画面刷新时，现去读
                // ingredients[i].GetBaseCount() 和 workAmount 的，读到的就是当前值。
                // 所以上面改完，下一帧画出来就已经是新数字了，不需要额外通知谁。
                recipe.ClearCachedData();

                // 记一笔：这一条是**真正改到过**的配方。
                // 少了这一句，LastAppliedCount 会永远是 0，
                // 启动日志就会在「其实改成功了」的情况下报「一个配方都没找到」——
                // 那比不打日志还糟，排查问题的人会被带到沟里去。
                applied++;
            }

            // 把这次的结果记下来，供 MKC_Bootstrap 在启动时打一条日志用。
            // 注意记的是 applied（真正改到的），不是 recipes.Count（匹配到的）——
            // 这两个数不一样，日志才有意义。
            LastAppliedCount = applied;
            LastWasSimpleMode = simple;

            if (skipped > 0)
            {
                // 有配方被跳过了就说一声。正常情况下这个数是 0；
                // 出现说明有模组在启动之后动过这些配方的结构，值得查一下。
                Verse.Log.Warning("[MKC] Skipped " + skipped
                    + " recipe(s) whose structure changed since startup; they keep their current values.");
            }
        }

        /// <summary>
        /// 判断底稿是不是「过期」了：只要清单里有任何一个配方，
        /// 现在的 Def 实例跟当初抄底稿时记下的不是同一个对象，就算过期。
        /// （开发者模式热重载 Defs 之后必然如此。）
        /// </summary>
        private static bool BaselineIsStale(List<RecipeDef> recipes)
        {
            foreach (RecipeDef recipe in recipes)
            {
                if (!capturedDefs.TryGetValue(recipe.defName, out RecipeDef known)) return true;
                if (!ReferenceEquals(known, recipe)) return true;
            }
            return false;
        }

        /// <summary>把旧底稿全部丢掉，按当前这批配方重新抄一份。</summary>
        private static void Recapture(List<RecipeDef> recipes)
        {
            originalWork.Clear();
            originalCounts.Clear();
            capturedDefs.Clear();
            capturedIngredientNames.Clear();
            CaptureOriginals(recipes);
            captured = true;
        }

        /// <summary>
        /// 检查某个配方的材料清单，跟抄底稿时是不是还一一对应：
        /// 个数要一样，每个位置上的材料种类（defName）也要一样。
        /// 对不上就返回 false，调用方会把这条配方整个跳过。
        /// </summary>
        private static bool IngredientsMatch(RecipeDef recipe, int expectedCount)
        {
            if (recipe.ingredients.Count != expectedCount) return false;
            if (!capturedIngredientNames.TryGetValue(recipe.defName, out List<string> names)) return false;
            if (names.Count != recipe.ingredients.Count) return false;

            for (int i = 0; i < names.Count; i++)
            {
                IngredientCount ingredient = recipe.ingredients[i];
                // 没写死材料种类（只给了筛选条件）的记成 "?"；
                // 这种情况本来就无法逐项核对，只要两边都是 "?" 就算对上。
                string now = (ingredient.IsFixedIngredient && ingredient.FixedIngredient != null)
                    ? ingredient.FixedIngredient.defName
                    : "?";
                if (now != names[i]) return false;
            }
            return true;
        }

        /// <summary>
        /// 把每个配方的原始数值抄一份下来，存进 originalWork / originalCounts 两张表，
        /// 同时记下它当前的 Def 实例和材料清单（上面那两个检查要用）。
        /// 只在第一次 Apply、以及底稿过期时各做一次；之后所有改写都拿这份底稿重新算，
        /// 所以反复开关设置也不会把数值越算越小。
        /// </summary>
        /// <param name="recipes">要抄的配方清单（调用方已经筛过「前缀 + 来源归属」两道筛子）。</param>
        private static void CaptureOriginals(List<RecipeDef> recipes)
        {
            foreach (RecipeDef recipe in recipes)
            {
                originalWork[recipe.defName] = recipe.workAmount;
                capturedDefs[recipe.defName] = recipe;

                List<float> counts = new List<float>(recipe.ingredients.Count);
                List<string> names = new List<string>(recipe.ingredients.Count);
                foreach (IngredientCount ingredient in recipe.ingredients)
                {
                    // 材料数量存在 IngredientCount 的 count 里，但 count 是私有字段，外面读不到，
                    // 得走公开的读取口 GetBaseCount()（配对的写入口是 SetBaseCount()）。
                    counts.Add(ingredient.GetBaseCount());

                    names.Add(ingredient.IsFixedIngredient && ingredient.FixedIngredient != null
                        ? ingredient.FixedIngredient.defName
                        : "?");
                }
                originalCounts[recipe.defName] = counts;
                capturedIngredientNames[recipe.defName] = names;
            }
        }
    }

    /// <summary>
    /// 启动时自动跑一遍的钩子：等所有 Def 都读好了，就把玩家的设置应用一次。
    ///
    /// 为什么不干脆把这件事放在 MKC_Mod 的构造函数里？因为那个构造函数跑得太早了 ——
    /// 模组对象刚被创建的时候，Def 数据（配方表）还没读进游戏，去查配方只能查到空的。
    /// 给这个类打上 [StaticConstructorOnStartup] 标记之后，游戏会在配方表准备就绪之后
    /// 再来叫我们一次，这时候查配方才有东西。
    /// </summary>
    [StaticConstructorOnStartup]
    public static class MKC_Bootstrap
    {
        static MKC_Bootstrap()
        {
            // 其实游戏那边已经兜了一层：负责挨个触发这些静态构造函数的
            // StaticConstructorOnStartupUtility.CallAll 自带 try/catch，
            // 出错时会打一条 Log.Error，所以最坏的情况也不会把游戏搞崩。
            // 这里再加一层，是为了把「配方全都没改成、还是老数值」这种
            // 悄无声息的失败，变成日志里一条看得见、能查的记录。
            // （这里不写死「一共有几个配方」这种数字：数量会随版本变化，
            //   所以 Apply 里是按「前缀 + 来源模组」两道筛子去扫的，见那一段。）
            try
            {
                MKC_RecipeTuner.Apply();

                // 成功也要报一声。这看着多余，其实很有用：
                // 如果不打印，「改成功了」和「这个 dll 压根没被加载」在日志里长得一模一样
                //（两者都是什么都不打印），出了问题就无从判断到底是哪种情况。
                int count = MKC_RecipeTuner.LastAppliedCount;
                if (count > 0)
                {
                    Verse.Log.Message("[MKC] Recipe tuning applied: " + count + " recipe(s), mode="
                        + (MKC_RecipeTuner.LastWasSimpleMode ? "simple" : "balanced") + ".");
                }
                else
                {
                    // 一个配方都没找到：可能是米莉拉本体没启用，也可能是配方还没读进来。
                    // 这里用 Warning 而不是 Error —— 没东西可改并不算「出错」，只是需要知道一声。
                    Verse.Log.Warning("[MKC] No GNH_Recipe_ recipes found; no values were changed. "
                        + "(Milira race mod disabled, or recipe defs not loaded yet.)");
                }
            }
            catch (System.Exception ex)
            {
                Verse.Log.Error("[MKC] Recipe tuning failed; all recipes keep vanilla values.\n" + ex);
            }
        }
    }
}
