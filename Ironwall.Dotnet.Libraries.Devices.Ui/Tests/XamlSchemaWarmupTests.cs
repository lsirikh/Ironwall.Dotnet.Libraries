using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Markup;
using System.Xaml;
using XamlReader = System.Windows.Markup.XamlReader;
using Xunit;
using Xunit.Abstractions;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/// <summary>
/// 순서 의존 실패(ASM-304 · 'SelectedTimeFormat' KeyNotFound) 재발 막기 — <see cref="XamlSchemaWarmup"/> 가 시험 전에
/// WPF 형식 멤버 사전을 만들어 두었는지, 그리고 만들어 둔 문맥에서는 동시 첫 조회가 깨지지 않는지를 본다.
/// 경쟁 재현은 <b>이 시험만 쓰는 새 스키마 문맥</b>에서 한다 — 공용 문맥을 건드리면 다른 시험이 깨진다.
/// </summary>
public class XamlSchemaWarmupTests
{
    private readonly ITestOutputHelper _out;
    public XamlSchemaWarmupTests(ITestOutputHelper output) => _out = output;

    private static Type MahAppsType(string name) => Assembly.Load("MahApps.Metro").GetType(name, throwOnError: true)!;

    /// <summary>공용과 같은 종류(WpfSharedXamlSchemaContext)의 새 문맥 — 캐시가 비어 있다.</summary>
    private static XamlSchemaContext FreshWpfContext()
    {
        var type = XamlReader.GetWpfSchemaContext().GetType();
        var ctor = type.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                       .First(c => c.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[] { typeof(XamlSchemaContextSettings), typeof(bool) }));
        return (XamlSchemaContext)ctor.Invoke(new object[] { new XamlSchemaContextSettings(), false });
    }

    /// <summary>STA 스레드 n 개가 매 판 새 문맥에서 같은 멤버를 동시에 처음 찾는다 — KeyNotFound 가 난 판 수.</summary>
    private static (int Failures, string? Sample) RaceFirstLookup(Type owner, string member, int rounds, bool warm)
    {
        const int n = 4;
        int failures = 0;
        string? sample = null;
        XamlSchemaContext? context = null;
        using var barrier = new Barrier(n + 1);
        var threads = Enumerable.Range(0, n).Select(_ => new Thread(() =>
        {
            for (var r = 0; r < rounds; r++)
            {
                barrier.SignalAndWait();
                try { context!.GetXamlType(owner).GetMember(member); }
                catch (KeyNotFoundException ex) { Interlocked.Increment(ref failures); sample ??= ex.Message; }
                barrier.SignalAndWait();
            }
            System.Windows.Threading.Dispatcher.FromThread(Thread.CurrentThread)?.InvokeShutdown();   // STA 정리 계약(StaCleanupTests) — 문맥 조회가 디스패처를 만들었으면 닫는다
        })).ToList();
        threads.ForEach(t => { t.SetApartmentState(ApartmentState.STA); t.Start(); });
        for (var r = 0; r < rounds; r++)
        {
            context = FreshWpfContext();
            if (warm) XamlSchemaWarmup.WarmUp(new[] { context }, new[] { owner });
            barrier.SignalAndWait();
            barrier.SignalAndWait();
        }
        threads.ForEach(t => t.Join());
        return (failures, sample);
    }

    [Fact]
    public void should_precreate_wpf_member_caches_in_both_shared_contexts_before_any_test_runs()
    {
        var result = XamlSchemaWarmup.Result;
        Assert.NotNull(result);
        _out.WriteLine(result!.ToString());
        Assert.True(result.Types > 1000, result.ToString());                                 // PresentationFramework · MahApps · MaterialDesign 까지 닿았다
        Assert.True(result.Caches > 0, result.ToString());

        var contexts = XamlSchemaWarmup.SharedContexts();
        Assert.Equal(2, contexts.Count);                                                     // 느슨한 XAML · BAML 공용 문맥 둘 다
        var mustBeWarm = new[]
        {
            MahAppsType("MahApps.Metro.Controls.DateTimePicker"),                           // 실패한 그 Setter 의 대상
            MahAppsType("MahApps.Metro.Controls.TimePickerBase"),
            MahAppsType("MahApps.Metro.Controls.RangeSlider"),                              // 어떤 시험도 쓰지 않는 형식 — 미리 만들기만이 채운다
            typeof(System.Windows.Setter), typeof(System.Windows.Trigger), typeof(System.Windows.Style),
            typeof(System.Windows.Controls.ListBox), typeof(System.Windows.Controls.TextBox), typeof(MaterialDesignThemes.Wpf.PackIcon),
            typeof(Consoles.Assembly.AssemblyView),
        };
        foreach (var context in contexts)
            foreach (var type in mustBeWarm)
                Assert.True(XamlSchemaWarmup.HasMemberCache(context, type), $"{context.GetType().Name} · {type.Name} 멤버 사전이 아직 없다");
    }

    [Fact]
    public void should_resolve_the_trigger_setter_member_from_many_sta_threads_at_once_when_the_context_is_warmed_first()
    {
        var dateTimePicker = MahAppsType("MahApps.Metro.Controls.DateTimePicker");

        var cold = RaceFirstLookup(dateTimePicker, "SelectedTimeFormat", rounds: 3000, warm: false);
        var warmed = RaceFirstLookup(dateTimePicker, "SelectedTimeFormat", rounds: 3000, warm: true);
        _out.WriteLine($"미리 안 만듦: KeyNotFound {cold.Failures}/3000 ({cold.Sample}) · 미리 만듦: {warmed.Failures}/3000");

        Assert.Equal(0, warmed.Failures);                                                    // 미리 만든 문맥은 동시 첫 조회에도 깨지지 않는다
    }
}
