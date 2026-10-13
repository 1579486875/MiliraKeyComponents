using System.Reflection;

// 本文件补齐程序集元数据。
// csproj 里设置了 <GenerateAssemblyInfo>false</GenerateAssemblyInfo>（避免自动生成
// 一大堆无关属性），因此版本号在这里手工维护 —— 请与 About\About.xml 的
// modVersion 保持一致，方便日后排查问题时一眼看出加载的是哪个版本。
[assembly: AssemblyTitle("MiliraKeyComponents")]
[assembly: AssemblyProduct("米莉拉：关键件制造 / Milira: Key Component Crafting")]
[assembly: AssemblyVersion("1.6.13.0")]
[assembly: AssemblyFileVersion("1.6.13.0")]
// 第三个版本号，与另外三个补丁模组保持一致（2026-10-11 审计补齐）：
// AssemblyVersion 是 CLR 用来判断「能不能兼容加载」的，AssemblyFileVersion 是
// 资源管理器里那一栏「文件版本」，而这个 InformationalVersion 才是「产品版本」。
// 三者一起写，双击 dll 看属性时不会出现某一栏空白。
[assembly: AssemblyInformationalVersion("1.6.13")]
