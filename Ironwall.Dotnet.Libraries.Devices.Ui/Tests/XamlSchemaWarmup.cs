using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Markup;
using System.Xaml;
using XamlReader = System.Windows.Markup.XamlReader;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 시험 호스트에서만 — WPF XAML 스키마 캐시(형식별 멤버 사전)를 시험이 시작되기 전에 한 스레드에서 미리 만든다.
/// </summary>
/// <remarks>
/// <para><b>왜</b>: <c>System.Windows.Baml2006.WpfXamlType</c> 의 <c>Members</c> · <c>AttachableMembers</c> 게터는
/// <c>if (_members == null) _members = new ConcurrentDictionary(); return _members;</c> 로 잠금 없이 늦게 만들고,
/// <c>CacheAndReturnXamlMember</c> 는 <c>Members.TryAdd(name, m)</c> 뒤 <c>Members[name]</c> 을 <b>게터로 다시</b> 읽는다(IL 확인).
/// 두 STA 스레드가 같은 형식의 멤버를 처음 동시에 찾으면 한쪽이 넣은 사전을 다른 쪽 새 사전이 덮어
/// <c>KeyNotFoundException: The given key 'SelectedTimeFormat' was not present in the dictionary</c> 가 난다
/// (조립기 좁은 창 시험이 Styles.Console.xaml 의 DateTimePicker 트리거 Setter 를 읽다가 — 같은 사전을 결선 · 부품 표 시험도 동시에 읽는다).
/// 스키마 문맥은 프로세스 공용이라(<see cref="XamlReader.GetWpfSchemaContext"/> · BAML 공용 문맥) 시험 순서 · 병렬에 따라 간헐로 난다.</para>
/// <para>제품(GIS 앱)은 XAML 을 UI 스레드 하나에서만 읽어 이 경쟁이 없다 — 그래서 시험 쪽에서만 고친다.
/// 사전이 한 번 만들어진 뒤에는 <c>TryAdd</c> · 색인이 같은 사전을 보므로 경쟁이 사라진다(강제 재현: 비운 채 20000회 중 114회 실패 · 미리 만든 채 0회).</para>
/// <para>이 프로젝트는 시험 코드가 제품 어셈블리에 함께 들어가므로 <see cref="TestCrashLog.IsTestHost"/> 일 때만 돈다.
/// 모듈 초기화기 안(형식 초기화 잠금)에서 <b>같은 스레드로</b> 돌린다 — 다른 스레드로 넘기면 이 모듈 형식의 정적 생성자를 기다리다 교착할 수 있다.</para>
/// </remarks>
internal static class XamlSchemaWarmup
{
    /// <summary>미리 만들 어셈블리 이름 머리 — XAML 에 나오는 형식을 가진 것만.</summary>
    private static readonly string[] AssemblyPrefixes =
    {
        "PresentationFramework", "PresentationCore", "WindowsBase", "System.Xaml",
        "MahApps.Metro", "ControlzEx", "MaterialDesign", "Microsoft.Xaml.Behaviors", "Ironwall.",
    };

    private static readonly Type? WpfXamlTypeClass = typeof(System.Windows.Style).Assembly.GetType("System.Windows.Baml2006.WpfXamlType");

    private static readonly MethodInfo[] LazyCacheGetters = new[] { "Members", "AttachableMembers" }
        .Select(name => WpfXamlTypeClass?.GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetGetMethod(nonPublic: true))
        .Where(getter => getter is not null)
        .Select(getter => getter!)
        .ToArray();

    /// <summary>모듈 초기화 때 미리 만든 결과(시험 호스트가 아니면 null).</summary>
    internal static WarmupResult? Result { get; private set; }

    internal sealed record WarmupResult(int Contexts, int Types, int Caches, long ElapsedMs);

    [ModuleInitializer]
    internal static void Initialize()
    {
        if (!TestCrashLog.IsTestHost) return;
        // 진단용 끄기 — 미리 만들기 없이 원래 경쟁을 다시 보고 싶을 때만(IRONWALL_XAML_WARMUP=off)
        if (string.Equals(Environment.GetEnvironmentVariable("IRONWALL_XAML_WARMUP"), "off", StringComparison.OrdinalIgnoreCase)) return;
        try { Result = WarmUp(SharedContexts(), XamlFacingTypes()); }
        catch (Exception ex) { TestCrashLog.Write("XamlSchemaWarmup", ex); }   // 미리 만들기 실패가 시험 전체를 막지 않게
    }

    /// <summary>WPF 가 프로세스 안에서 함께 쓰는 스키마 문맥 둘 — 느슨한 XAML(<c>XamlReader.Parse</c>)용 · BAML(<c>InitializeComponent</c>)용.</summary>
    internal static IReadOnlyList<XamlSchemaContext> SharedContexts()
    {
        var contexts = new List<XamlSchemaContext> { XamlReader.GetWpfSchemaContext() };
        var baml = typeof(XamlReader).GetProperty("BamlSharedSchemaContext", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null) as XamlSchemaContext;
        if (baml is not null && !ReferenceEquals(baml, contexts[0])) contexts.Add(baml);
        return contexts;
    }

    /// <summary>이 어셈블리가 (거슬러) 참조하는 UI 어셈블리의 공개 형식 — 제네릭 정의 · 인터페이스 · 대리자는 뺀다.</summary>
    internal static IReadOnlyList<Type> XamlFacingTypes()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<Assembly>();
        var assemblies = new List<Assembly>();
        void Enqueue(Assembly assembly)
        {
            var name = assembly.GetName().Name ?? string.Empty;
            if (!seen.Add(name)) return;
            queue.Enqueue(assembly);
            if (AssemblyPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))) assemblies.Add(assembly);
        }
        Enqueue(typeof(XamlSchemaWarmup).Assembly);
        // MahApps 처럼 XAML(BAML)에서만 쓰여 메타데이터 참조 사슬에 안 걸리는 어셈블리도 있다 — 출력 폴더의 같은 머리 DLL 을 씨앗으로 더한다
        foreach (var file in System.IO.Directory.EnumerateFiles(AppContext.BaseDirectory, "*.dll"))
        {
            var name = System.IO.Path.GetFileNameWithoutExtension(file);
            if (seen.Contains(name) || !AssemblyPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))) continue;
            try { Enqueue(Assembly.Load(new AssemblyName(name))); }
            catch (Exception) { seen.Add(name); }
        }
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var currentName = current.GetName().Name ?? string.Empty;
            if (!ReferenceEquals(current, typeof(XamlSchemaWarmup).Assembly)
                && !AssemblyPrefixes.Any(prefix => currentName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))) continue;   // UI 쪽 사슬만 따라간다
            foreach (var reference in current.GetReferencedAssemblies())
            {
                if (seen.Contains(reference.Name ?? string.Empty)) continue;
                try { Enqueue(Assembly.Load(reference)); }
                catch (Exception) { seen.Add(reference.Name ?? string.Empty); }       // 못 읽는 참조는 건너뛴다
            }
        }

        var types = new List<Type>();
        foreach (var assembly in assemblies)
        {
            Type[] exported;
            try { exported = assembly.GetExportedTypes(); }
            catch (Exception) { continue; }
            types.AddRange(exported.Where(t => !t.IsGenericTypeDefinition && !t.IsInterface && !typeof(Delegate).IsAssignableFrom(t)));
        }
        return types;
    }

    /// <summary>주어진 문맥마다 형식의 WPF 멤버 사전을 만든다 — 만든(또는 이미 있던) 사전 수를 돌려준다.</summary>
    internal static WarmupResult WarmUp(IReadOnlyCollection<XamlSchemaContext> contexts, IReadOnlyCollection<Type> types)
    {
        var watch = Stopwatch.StartNew();
        var caches = 0;
        if (WpfXamlTypeClass is null || LazyCacheGetters.Length == 0) return new WarmupResult(contexts.Count, types.Count, 0, 0);
        foreach (var context in contexts)
        {
            foreach (var type in types)
            {
                try
                {
                    var xamlType = context.GetXamlType(type);
                    if (xamlType is null || !WpfXamlTypeClass.IsInstanceOfType(xamlType)) continue;
                    foreach (var getter in LazyCacheGetters)
                    {
                        if (getter.Invoke(xamlType, null) is not null) caches++;
                    }
                }
                catch (Exception)
                {
                    // 스키마가 다루지 못하는 형식은 건너뛴다 — XAML 에 나올 수 없는 형식이다
                }
            }
        }
        return new WarmupResult(contexts.Count, types.Count, caches, watch.ElapsedMilliseconds);
    }

    /// <summary>시험용 — 그 문맥에서 그 형식의 WPF 멤버 사전(<c>_members</c>)이 이미 만들어졌는가(만들지 않고 읽기만).</summary>
    internal static bool HasMemberCache(XamlSchemaContext context, Type type)
    {
        var xamlType = context.GetXamlType(type);
        var field = WpfXamlTypeClass?.GetField("_members", BindingFlags.Instance | BindingFlags.NonPublic);
        return xamlType is not null && field is not null && field.GetValue(xamlType) is not null;
    }
}
