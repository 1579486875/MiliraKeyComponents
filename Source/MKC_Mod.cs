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
        /// <summary>
        /// 是否打开简单模式：打开后工作量降到原来的 20%，
        /// 材料数量按「上取整的一半」算 —— 至少留 1 个，而且绝不会变得比原值多
        /// （15 变 8、45 变 23、1 还是 1、0.5 也还是 0.5）。
        /// </summary>
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
        /// 免得界面上冒出一串谁也看不懂的东西。
        ///
        /// 两个值得知道的情况：
        ///   · 本模组确实有这种 key（例如「设置暂时不可用」那句），所以这层保险并非摆设；
        ///   · 开发者模式下游戏会把界面文字「伪翻译」成带音标的怪样子（PseudoTranslated），
        ///     那时 text 不再等于 key，下面那句判断就失效了 —— 只影响 DevMode，正常玩不受影响。
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
    /// （底稿过期时会整份重抄 —— 什么时候算过期见 BaselineIsStale。）
    /// 所以你把简单模式的开关拨来拨去一百次，结果也和只拨一次一模一样，
    /// 不会出现「越调越小、越调越离谱」的情况。
    /// </summary>
    public static class MKC_RecipeTuner
    {
        /// <summary>简单模式下工作量打几折：0.2 就是乘以 0.2，也就是降到原来的 20%。</summary>
        private const float WorkFactor = 0.2f;

        /// <summary>简单模式下材料数量的系数：0.5 就是减半。</summary>
        private const float IngredientFactor = 0.5f;

        /// <summary>工作量算出来的下限：再小也不能低于这个数，否则会把配方变成「瞬间完成」（负数不适用，见 ExpectedWork）。</summary>
        private const float MinWorkAmount = 1f;

        /// <summary>
        /// 材料数量算出来的下限。**当前是冗余层：对任何输入都不改变结果**
        ///（50 万随机样本实测，去掉它与保留它逐位相同）：
        /// c &gt; 0 时 Ceil(c * 0.5f) 已经 &gt;= 1，下限没参与；
        /// c &lt;= 0 时它把中间值抬成 1，又被外层 Mathf.Min 抹回 c。
        /// 保留它是为了将来调整 IngredientFactor 时有个显式的兜底位 —— 别误以为它现在真在起作用。
        /// 另外它也防不住「配方不要材料」：原值 0 走的是 c &lt;= 0 那条路，根本不经过这一层。
        /// </summary>
        private const float MinIngredientCount = 1f;

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
        /// 有没有「配方被整批换成新对象」的情况，光看 defName 是看不出来的，得比对象本身。
        ///
        /// 注意它**发现不了开发者模式的「重载 Defs」**：热重载时游戏会把新读出来的 XML 数值
        /// 就地写进原来那个 Def 对象（对象不变、数值被覆盖），所以这里比对象毫无反应 ——
        /// 热重载要靠 NeedsReapply 的数值比对来兜。
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
        ///
        /// 补充：过期重抄时若中途出错，这个标记仍是 true，但四张表已经被清空了 ——
        /// BaselineIsStale 会因查不到实例而返回 true，下次调用还会重抄，一样能恢复。
        /// </summary>
        private static bool baselineCaptured;

        /// <summary>
        /// 最近一次 Apply 里**成功处理**了几个配方（匹配到、但被跳过的那些不算）。
        /// 注意：平衡模式下这一步是把原值写回去，数值本身没变，但同样计入 ——
        /// 所以它准确来说是「处理了几条」，而不是「有几条的数值真的变了」。
        /// 专门留着给启动日志报数用，免得日志里只有一句「跑过了」。
        /// </summary>
        public static int LastAppliedCount { get; private set; }

        /// <summary>
        /// 最近一次 Apply 里**匹配到**几条本模组的配方（其中可能有被跳过的）。
        /// 单看 LastAppliedCount 是分不清「一条都没找到」和「找到了但全被跳过」的，
        /// 而有这个数，日志才能把两种完全不同的情况说清楚。
        /// </summary>
        public static int LastMatchedCount { get; private set; }

        /// <summary>最近一次 Apply 的时候，玩家是不是开着简单模式。同样只给启动日志报数用。</summary>
        public static bool LastWasSimpleMode { get; private set; }

        /// <summary>
        /// 诊断日志是否已经打过。它只在「一条配方都没匹配到」时才会打印，
        /// 而 Apply 会被设置窗口里每次拨动开关、每次关窗各触发一次 ——
        /// 没有这个开关的话，玩家来回拨几下就会连着刷好几条。
        /// </summary>
        private static bool diagLogged;

        /// <summary>
        /// 「结构变化导致跳过」的警告是否已经打过。同样是为了防刷屏 ——
        /// 这段逻辑现在挂在每 5 秒一次的定期复查上（见 MKC_GameComponent.RecheckIntervalTicks），
        /// 只要有一条配方结构对不上，不去重的话一局下来能刷出成百上千条一模一样的警告。
        /// 只打第一遍、并在文案里说明「重启游戏前都保持现状」，玩家照样知道发生了什么。
        /// </summary>
        private static bool skippedLogged;

        /// <summary>
        /// 「逐条处理出错」的错误日志是否已经打过。理由同 skippedLogged ——
        /// 复查是每 5 秒一次的（见 MKC_GameComponent.RecheckIntervalTicks），
        /// 同一条坏配方会反复失败，不去重的话一局下来就是几千条一模一样的红字，
        /// 把真正有用的错误淹掉。只报第一条（带配方名和异常），之后静默。
        /// </summary>
        private static bool tuneErrorLogged;

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

        /// <summary>
        /// 把「当前这一批本模组的配方」整个收集出来。
        /// Apply 与 NeedsReapply 都调它 —— 两处各写一份 Where(IsOurRecipe).ToList()
        /// 看着一样，但将来筛子一变（比如再加一条判定），漏改一处就会出现
        /// 「应用的是一个集合、复查的是另一个集合」这种极难发现的错位。
        /// </summary>
        private static List<RecipeDef> CollectOurRecipes() =>
            DefDatabase<RecipeDef>.AllDefsListForReading.Where(IsOurRecipe).ToList();

        /// <summary>
        /// 一条配方的原始工作量在当前设置下应该是多少。
        /// Apply 与 NeedsReapply 必须都调这一个方法 —— 公式写两份的话，改了一处忘了另一处，
        /// 就会出现「复查永远认为不对、每次进游戏白跑」或「永远认为对、该重算时不重算」。
        /// 负数是游戏约定（工作量跟着产物走），必须原样保留，不能被 Max 抬成 1。
        /// </summary>
        private static float ExpectedWork(float originalWork, bool simple)
        {
            if (!simple || originalWork <= 0f) return originalWork;
            return Mathf.Min(originalWork, Mathf.Max(MinWorkAmount, originalWork * WorkFactor));
        }

        /// <summary>一种材料的原始数量在当前设置下应该是多少（同上，公式只此一处）。</summary>
        private static float ExpectedCount(float originalCount, bool simple)
        {
            if (!simple) return originalCount;
            return Mathf.Min(originalCount, Mathf.Max(MinIngredientCount, Mathf.Ceil(originalCount * IngredientFactor)));
        }

        public static void Apply()
        {
            List<RecipeDef> recipes = CollectOurRecipes();

            LastMatchedCount = recipes.Count;

            if (recipes.Count == 0)
            {
                LastAppliedCount = 0;

                // 一个都没找到时，把「为什么」一次问清楚，免得来回猜。
                // 这一段只在出问题时才会执行，正常游戏不会看到它；
                // 而且只打第一次（diagLogged），后面重复调用就安静了。
                if (diagLogged)
                {
                    return;
                }
                diagLogged = true;
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
                        // 上面已经打了一条诊断说明原因（且只打一次），这里就直接返回；
                        // 启动那一次的汇总信息由 MKC_Bootstrap 负责（见本文件末尾）。
            }

            // 抄底稿的时机：第一次进来抄一份；或者发现底稿已经过期，就丢掉重抄。
            //
            // 抄的时候是先抄完、再立 baselineCaptured 标记的（见 Recapture），
            // 这样万一抄到一半抛了异常，标记还是 false，下次调用还有机会重来，
            // 不会落得「以后永远没有原始值可用、配方再也改不动」的地步。
            //
            // 怎么算「过期」：当配方表的 Def 被换成一批新对象时（例如在游戏里切换语言：
            // 游戏会清空并重建全部 Def，配方换成一批新对象），旧底稿里记的实例就对不上了。
            // 判断方式见 BaselineIsStale。
            //
            // 热重载不在这个分支覆盖范围内 —— 它不换对象、只是把 XML 原值就地写回同一个对象，
            // BaselineIsStale 看不出来，得靠 NeedsReapply 的数值比对。
            if (!baselineCaptured || BaselineIsStale(recipes))
            {
                Recapture(recipes);
            }

            // 看看玩家有没有勾上简单模式。设置对象还没建好（Settings 为 null）时，就当成没勾。
            bool simple = MKC_Mod.Settings != null && MKC_Mod.Settings.simpleMode;

            int applied = 0;
            int skipped = 0;

            foreach (RecipeDef recipe in recipes)
            {
                // 每条配方各自包一层 try/catch。理由：要是让异常冒到整个循环外面，
                // 前面已经改过的那些配方是「已生效」状态、后面那些却一点没动，
                // 而计数还停在旧值上 —— 日志和实际状态对不上，排查的人会被彻底带偏。
                // 包在这里，「一条失败」最多只影响它自己，计数也立刻跟得上实际进度。
                //
                // 注意保护范围只到循环为止：循环之外还有 Recapture() → CaptureOriginals()，
                // 它们同样会读 ingredient.IsFixedIngredient，却没有任何保护 ——
                // 一旦在那边抛，整个 Apply 直接失败、所有配方一条都改不动
                //（MKC_Bootstrap 会打 "Recipe tuning failed; all recipes keep vanilla values."）。
                // 更麻烦的是 Recapture 已经把四张表 Clear() 了：baselineCaptured 若仍为 true，
                // 下次 BaselineIsStale 会因查不到实例而返回 true → 再抄 → 再抛，
                // 形成「每次必败」的稳定状态。
                try
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
                    //
                    // 为什么不干脆「删掉底稿重抄一份」把它救回来？因为重抄会把它「当前」的值
                    // 当成原始值；如果它是在我们改小过之后才变的结构，那个「减半后的值」就会被
                    // 永久记成原值，玩家以后关掉简单模式也回不到作者设计的数值，而且全程没有提示。
                    // 宁可这条暂时按原值算。
                    if (!IngredientsMatch(recipe, counts.Count))
                    {
                        skipped++; continue;
                    }

                    // 工作量：简单模式就乘 0.2。公式统一在 ExpectedWork 里，
                    // NeedsReapply 用的是同一份，不会出现两边算得不一样的情况。
                    //
                    // 但要注意 work 可能是**负数**，那不是「没填」，而是游戏的一个约定：
                    // workAmount 小于 0 表示「工作量跟着产物走」（取产物的 WorkToMake 属性）。
                    // ExpectedWork 开头那句 originalWork <= 0f 的特判其实**是冗余的** ——
                    // 外层 Mathf.Min 已经能把 0 / 负数兜回原值（50 万随机样本实测：带与不带逐位相同）。
                    // 留着它单纯是为了让「负数跟产物走」的意图在代码里明摆着，
                    // 不必依赖 Mathf 的求值顺序去推。
                    // 不是简单模式就直接写回抄下来的原始值，也就是恢复成原来的样子。
                    // 这里最后一层 Mathf.Min 防的是另一件事，即「缩小反而变大」：
                    // work 落在 (0, 1) 时，乘 0.2 之后不足 1，被 Mathf.Max 抬到 1 就比原值还大，
                    // 取 Min 之后最多等于原值。
                    // 顺带纠正一个容易想当然的地方：工作量侧的下限（MinWorkAmount）是**真在起作用**的
                    //（w 在 0~5 之间时会把结果拉回 1），材料侧那个不是 —— 见 MinIngredientCount 的说明。
                    recipe.workAmount = ExpectedWork(work, simple);
                    for (int i = 0; i < counts.Count; i++)
                    {
                        // 简单模式的目标是「变少」，所以：先乘系数，结果若带小数就向上取整
                        // （乘完是 1.2 的话取整成 2，免得界面上出现「1.5 个钢」这种难看的数），
                        // 然后至少留 1，最后和原值取较小的那一个。
                        //
                        // 最后那一步 Mathf.Min 是为了防住「减半反而变多」的怪事：
                        // 原值 0.5 的时候，0.5 × 0.5 = 0.25，向上取整会变成 1 —— 比原来还大。
                        // 取了 Min 之后就还是 0.5。
                        //
                        // 注意这一切只在简单模式下发生；平衡模式是原样写回抄下来的值，
                        // 包括本来就是小数的那些，不会被取整。
                        // 公式同样统一在 ExpectedCount 里。
                        recipe.ingredients[i].SetBaseCount(ExpectedCount(counts[i], simple));
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

                    // 这一条**真正改到了**：就地更新计数与模式。
                    // 就地更新（而不是只靠循环结束后那一次赋值）真正起作用的场景只有一个：
                    // foreach 的迭代器自己抛异常 —— 例如 recipes 列表在遍历中被并发 / 重入修改，
                    // MoveNext() 抛 InvalidOperationException。这类异常发生在循环体**之外**，
                    // 内层 try/catch 拦不住，循环之后的那次赋值也执行不到；
                    // 此时就地更新能保住「实际已经处理过几条」这个数，日志不至于退回 0 骗人。
                    //（某条配方自己出错的情形，内层 catch 会让循环继续、循环后的赋值照常执行，
                    //  两种写法结果完全相同 —— 所以这段的价值只在那一种极端情形。）
                    applied++;
                    LastAppliedCount = applied;
                    LastWasSimpleMode = simple;
                }
                catch (System.Exception ex)
                {
                    // 一条配方出错只影响它自己：它保持当前数值（不会留下半拉子状态），
                    // 其余配方照常处理，计数也照常往下走。
                    skipped++;
                    // 去重：复查每 5 秒跑一次（见 MKC_GameComponent.RecheckIntervalTicks），
                    // 同一条坏配方会反复失败；不去重就是一局几千条红字，把真正有用的错误淹掉。
                    // 只报第一条，风格与 skippedLogged / diagLogged 一致。
                    if (!tuneErrorLogged)
                    {
                        tuneErrorLogged = true;
                        Verse.Log.Error("[MKC] Failed to tune recipe " + recipe.defName
                            + "; it keeps its current values.\n" + ex);
                    }
                }
            }

            // 把这次的结果再记一遍，供 MKC_Bootstrap 在启动时打一条日志用。
            // 注意记的是 applied（真正改到的），不是 recipes.Count（匹配到的）——
            // 这两个数不一样，日志才有意义。
            // （循环内已经在每条成功后就地更新过了，这里是为了「一条都没成功」时也落到 0。）
            LastAppliedCount = applied;
            LastWasSimpleMode = simple;

            // 有配方被跳过了就说一声 —— 但一整局只打一次（skippedLogged）。
            // 正常情况下这个数是 0；出现说明有模组在启动之后动过这些配方的结构，值得查一下。
            // 之所以要去重：这段逻辑现在挂在每 5 秒一次的定期复查上
            //（见 MKC_GameComponent.RecheckIntervalTicks），结构一旦对不上就是每 5 秒一条，
            // 刷屏比不报还糟。
            if (skipped > 0 && !skippedLogged)
            {
                skippedLogged = true;
                Verse.Log.Warning("[MKC] Skipped " + skipped
                    + " recipe(s) whose structure changed since startup; they keep their current values until the game is restarted.");
            }
        }

        /// <summary>
        /// 确认「玩家设置」和「配方当前数值」是对得上的，对不上就重新应用一次。
        /// 返回 true 表示这次真的执行了 Apply。
        ///
        /// 为什么需要它：本模组的自动应用挂在 [StaticConstructorOnStartup] 上，
        /// 而 CLR 保证静态构造函数一个进程只跑一次。进游戏之后还有两条路会让
        /// 「配方当前数值」和玩家设置重新对不上，静态构造函数都管不到：
        ///   · 在游戏里切换语言：游戏会清空并重建全部 Def，配方换成一批新对象 ——
        ///     MKC_GameComponent 每次进游戏的首检能兜住这一种；
        ///   · 开发者模式「重载 Defs」：它不换实例、只把数值就地写回 XML 原值 ——
        ///     本组件每局只做一次首检，兜不住，所以它还会**定期复查**
        ///    （见 MKC_GameComponent.RecheckIntervalTicks）。
        /// 结果就是：玩家明明开着简单模式，配方却按原价算，而且全程没有任何提示。
        /// 这个方法由 MKC_GameComponent 定期调用，把这一步补上。
        /// </summary>
        internal static bool EnsureApplied()
        {
            if (!NeedsReapply())
            {
                return false;
            }
            Apply();
            return true;
        }

        /// <summary>
        /// 判断「现在需要重新应用一次吗」。三种情况会返回 true：
        ///   1. 还没抄过底稿；
        ///   2. 底稿里记的 Def 实例和现在不是同一批（说明配方表被整个重建过）；
        ///   3. 逐条比对后，发现至少有一条配方的当前数值跟「当前设置下应有的值」不符。
        /// 第 3 条必须遍历**全部**配方，不能只抽查第一条：
        /// 数值完全可能只被改回去一部分（例如热重载只覆盖了其中几条，
        /// 或者某条配方在处理时出过错），只抽查一条时，只要那条恰好是对的，
        /// 整批偏差就会被当成「没问题」放过 —— 玩家开着简单模式却按原价造东西，
        /// 而且没有任何提示。全量扫一遍的开销是每条两个 float 比较，可以忽略。
        /// </summary>
        private static bool NeedsReapply()
        {
            List<RecipeDef> recipes = CollectOurRecipes();

            if (recipes.Count == 0)
            {
                return false;   // 一条都没有，没什么可应用的
            }
            if (!baselineCaptured || BaselineIsStale(recipes))
            {
                return true;
            }

            bool simple = MKC_Mod.Settings != null && MKC_Mod.Settings.simpleMode;

            // 逐条比对：工作量、以及每种材料的数量，全都要等于「当前设置下应有的值」。
            // 公式一律走 ExpectedWork / ExpectedCount，跟 Apply 用的是同一份 ——
            // 两边各写一份的话，一处改了另一处没改，就会出现「永远认为不对、每次白跑」
            // 或者「永远认为对、该重算时不重算」。
            //
            // 这一整段包 try/catch：下面的 IngredientsMatch 会解引用 recipe.ingredients /
            // ingredient.filter，按 Verse.RecipeDef 与 Verse.IngredientCount 的反编译，
            // 这两个字段都没有初始化器，第三方 patch 把结构改坏时可能是 null（→ NPE）。
            // 这里一抛，EnsureApplied 会连 Apply() 都走不到，「自动重应用」就彻底失效了
            //（开发者重载 Defs、切换语言之后再也恢复不了），而且每 300 tick 一条红字。
            //
            // 出异常为什么返回 true 而不是 false？false 的语义是「不用重算」，
            // 等于永久放弃恢复；true 只是让调用方走一趟 Apply —— Apply 内部每条配方都有
            // 自己的 try/catch（见 Apply 的循环），坏配方会被单独跳过，其余配方照样能纠正回来。
            // 复查阶段宁可多跑一次，也不能因为「查不出来」就当作「不用查」。
            try
            {
                foreach (RecipeDef recipe in recipes)
                {
                    if (!originalWork.TryGetValue(recipe.defName, out float work))
                    {
                        return true;   // 查不到底稿，只能重来一遍
                    }
                    if (!Mathf.Approximately(recipe.workAmount, ExpectedWork(work, simple)))
                    {
                        return true;
                    }

                    if (!originalCounts.TryGetValue(recipe.defName, out List<float> counts))
                    {
                        return true;
                    }
                    if (!IngredientsMatch(recipe, counts.Count))
                    {
                        return true;   // 材料结构变了，Apply 里也会跳过它，同样按「需要重新应用」处理
                    }

                    for (int i = 0; i < counts.Count; i++)
                    {
                        if (!Mathf.Approximately(recipe.ingredients[i].GetBaseCount(),
                                ExpectedCount(counts[i], simple)))
                        {
                            return true;
                        }
                    }
                }
                return false;
            }
            catch (System.Exception ex)
            {
                Verse.Log.Warning("[MKC] Re-check failed, forcing re-apply: " + ex.Message);
                return true;
            }
        }

        /// <summary>
        /// 判断底稿是不是「过期」了：只要清单里有任何一个配方，
        /// 现在的 Def 实例跟当初抄底稿时记下的不是同一个对象，就算过期。
        /// （例如在游戏里切换语言：游戏会清空并重建全部 Def，配方换成一批新对象。）
        ///
        /// 注意这里**查不出开发者模式的「重载 Defs」**：热重载不换实例，
        /// 只是把新读出来的 XML 数值就地写进原来那个对象 —— 对象始终是同一个。
        /// 那条路径由 NeedsReapply 的数值比对负责。
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
            baselineCaptured = true;
        }

        /// <summary>
        /// 检查某个配方的材料清单，跟抄底稿时是不是还一一对应：
        /// 个数要一样，每个位置上的材料种类（defName）也要一样。
        /// 对不上就返回 false，调用方会把这条配方整个跳过。
        /// </summary>
        private static bool IngredientsMatch(RecipeDef recipe, int expectedCount)
        {
            // 结构防御：Verse.RecipeDef.ingredients 反编译是 public List<IngredientCount> ingredients;
            // （没有初始化器），XML 缺 <ingredients> 节点时它就是 null —— 直接取 .Count 会 NPE。
            // 这里当作「对不上」返回 false，调用方本来就有对应的跳过逻辑。
            if (recipe.ingredients == null) return false;
            if (recipe.ingredients.Count != expectedCount) return false;
            if (!capturedIngredientNames.TryGetValue(recipe.defName, out List<string> names)) return false;
            if (names.Count != recipe.ingredients.Count) return false;

            for (int i = 0; i < names.Count; i++)
            {
                IngredientCount ingredient = recipe.ingredients[i];
                // 同样挡两种结构性损坏：列表里夹着 null 元素、以及 filter 为 null
                //（IngredientCount.filter 是 public ThingFilter filter;，构造函数是空的）。
                // 下面读 IsFixedIngredient 会去解引用 filter，所以先挡掉。
                // 注意这跟「材料里的 def 没解析出来」不是一回事：那种情况走的是 "?" 分支
                //（下面本来就判了 FixedIngredient != null），不会抛。
                if (ingredient == null || ingredient.filter == null) return false;
                // 没写死材料种类（只给了筛选条件）的记成 "?"；
                // 这种情况本来就无法逐项核对，只要两边都是 "?" 就算对上。
                // 风险：如果当时那个材料的 Def 根本没解析成功（例如本体没启用），
                // 这里也会算成 "?"；等 Def 恢复正常后名字就对不上了，这条配方会被一直跳过。
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
    /// 游戏组件：每次「进入游戏」都确认「玩家设置真的生效了」，之后还会定期复查。
    ///
    /// 为什么需要它：本模组的自动应用挂在 [StaticConstructorOnStartup] 上，
    /// 而 CLR 保证静态构造函数一个进程只跑一次，进游戏之后就不再触发。之后有两条路
    /// 会让配方数值和玩家设置重新对不上，静态构造函数都管不到：
    ///   · 在游戏里切换语言：游戏会清空并重建全部 Def（配方换成一批新对象）——
    ///     本组件每次进游戏的首检能兜住这一种；
    ///   · 开发者模式「重载 Defs」：不换实例、只把数值就地写回 XML 原值 ——
    ///     首检只有一次，兜不住，所以本组件还会**定期复查**（见 RecheckIntervalTicks）。
    /// 少了这个组件，玩家开着简单模式、配方却按原价算，而且没有任何提示。
    ///
    /// 开销：进游戏后第一个 tick 做一次完整检查，之后每 300 tick（约 5 秒）复查一次；
    /// 两次复查之间每 tick 只剩一次 bool 判断和一次自增。
    /// </summary>
    public class MKC_GameComponent : GameComponent
    {
        /// <summary>
        /// 本局是否已经做过首检。每次进游戏游戏都会重新构造一个组件
        /// （新游戏走 Game.FillComponents → Activator.CreateInstance；
        /// 读档走 ScribeExtractor.CreateInstance），所以这个值必然是 false ——
        /// 也就是说这道保险其实用不上，留着无害。
        ///
        /// [Unsaved] 的真实作用跟存档无关：Verse.GameComponent 自己没有覆写 ExposeData
        /// （那是个空方法体），本类也没覆写，所以字段根本进不了存档；
        /// 这个特性是让 XML 注入 / DefInjected 翻译跳过该字段。
        /// </summary>
        [Unsaved]
        private bool checkedOnce;

        /// <summary>距离上一次复查过去了多少个 tick。</summary>
        private int ticksSinceCheck;

        /// <summary>
        /// 复查间隔：300 tick ≈ 游戏内 5 秒。
        /// 为什么必须定期复查：开发者模式「重载 Defs」时，游戏会把新读出来的 XML 数值
        /// **就地写回原来那个配方对象** —— 对象还是同一个，只是数值变回了原值。
        /// 所以「比对象」发现不了，只能「比数值」，而只比一次是远远不够的。
        /// 开销：每 5 秒扫一遍 21 条配方，可以忽略。
        /// </summary>
        private const int RecheckIntervalTicks = 300;

        /// <summary>
        /// RimWorld 用 Activator.CreateInstance(type, game) 创建 GameComponent
        /// （见 Verse.Game.FillComponents），所以这个带 Game 参数的构造函数必须留着。
        /// 读档时也一样：Game.ExposeSmallComponents 里
        /// Scribe_Collections.Look(ref components, "components", LookMode.Deep, this)
        /// 的最后一个参数就是构造参数。
        /// 构造函数里什么都不做也是故意的：重活留到 tick 里做。
        /// </summary>
        public MKC_GameComponent(Game game)
        {
        }

        public override void GameComponentTick()
        {
            if (checkedOnce)
            {
                // 首检已经做过了，接下来按固定间隔复查。
                // 复查是必须的：开发者模式「重载 Defs」不换 Def 实例，
                // 而是把 XML 原值就地写回同一个对象，只有比数值才看得出来（见 RecheckIntervalTicks）。
                ticksSinceCheck++;
                if (ticksSinceCheck < RecheckIntervalTicks)
                {
                    return;   // 两次复查之间每 tick 的全部开销就是这一次 bool 判断和一次自增
                }
            }

            // 配方表还没就绪时先不检查，也不置位 —— 留到下一个 tick 再看。
            if (DefDatabase<RecipeDef>.AllDefsListForReading.Count == 0)
            {
                return;
            }

            checkedOnce = true;
            ticksSinceCheck = 0;
            try
            {
                MKC_RecipeTuner.EnsureApplied();
            }
            catch (System.Exception ex)
            {
                // 这里出了错也不该影响游戏运行，记一条就够了。
                // 下次复查还会再试一遍，所以不会失败一次就永久放弃。
                Verse.Log.Error("[MKC] Re-applying settings on game start failed.\n" + ex);
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
                int matched = MKC_RecipeTuner.LastMatchedCount;
                int applied = MKC_RecipeTuner.LastAppliedCount;
                string mode = MKC_RecipeTuner.LastWasSimpleMode ? "simple" : "balanced";

                if (matched == 0)
                {
                    // 一条都没匹配到：配方表可能没加载，或者模组被禁用了。
                    // 用 Warning 而不是 Error —— 没东西可改并不算「出错」，只是需要知道一声。
                    Verse.Log.Warning("[MKC] No GNH_Recipe_ recipes found; no values were changed. "
                        + "(Milira race mod disabled, or recipe defs not loaded yet.)");
                }
                else if (applied < matched)
                {
                    // 匹配到了、但有配方被跳过：说明有模组在启动之后动过这些配方的结构，
                    // 值得查一下。这里必须和「一条都没找到」分开报，否则会被误导。
                    Verse.Log.Warning("[MKC] Recipe tuning partly applied: " + applied + " of " + matched
                        + " recipe(s) processed, mode=" + mode
                        + ". The rest were skipped because their structure changed.");
                }
                else
                {
                    Verse.Log.Message("[MKC] Recipe tuning applied: " + applied
                        + " recipe(s), mode=" + mode + ".");
                }
            }
            catch (System.Exception ex)
            {
                Verse.Log.Error("[MKC] Recipe tuning failed; all recipes keep vanilla values.\n" + ex);
            }
        }
    }
}
