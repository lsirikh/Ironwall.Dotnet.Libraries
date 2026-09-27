using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Views.Panels;
using Ironwall.Dotnet.Libraries.Base.Services;
using Ironwall.Dotnet.Libraries.Devices.Providers;
using Ironwall.Dotnet.Libraries.Gateway.Providers;
using Ironwall.Dotnet.Libraries.Gateway.Services;
using Ironwall.Dotnet.Libraries.Gateway.ViewModels;
using Ironwall.Dotnet.Libraries.Gateway.Views;
using Ironwall.Dotnet.Libraries.Reports.Api.Services;
using Ironwall.Dotnet.Libraries.Reports.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Reports.Ui.Views.Panels;
using Ironwall.Dotnet.Libraries.Utils.Consoles;
using Moq;
using Xunit;

namespace Accounts.Ui.ViewTests;

/// <summary>
/// B7(날짜 팝업) · B10(콘솔 안 옛 입력) — window-design-inventory-analysis.md #49 · #50 · #51 · #12a · #18a 를 실제로 띄워 본다.
/// </summary>
/// <remarks>
/// <para>지키는 것: ① 콘솔 안의 날짜 입력이 커널 칸(<see cref="DateTimeField"/>)이고 뷰모델 바인딩 · AutomationId 가 그대로다
/// ② 옛 입력 뷰 둘이 ConsoleSection · ConsoleField(또는 Console.DataGrid) 와 토큰으로 서 있다 ③ x:Name(Caliburn 지시자) · AutomationId 를 하나도
/// 잃지 않았다 ④ 색 리터럴이 없다. 기대 목록은 바꾸기 전(HEAD 67fa0433) XAML 에서 뽑았다.</para>
/// <para>뷰모델 계약은 바꾸지 않았다 — 칸의 <c>DateTime?</c> 값이 <c>DateTime</c> 뷰모델 속성(감사 기간 · 부여 시작)에 그대로 물린다
/// (칸이 비울 수 없게 되어 있으면 null 을 확정하지 않는다).</para>
/// </remarks>
public class ConsoleDateAndLegacyInputViewTests
{
    // ── 바꾸기 전 식별자 ─────────────────────────────────────────────

    public static readonly IReadOnlyDictionary<string, string[]> XNames = new Dictionary<string, string[]>
    {
        [@"Ironwall.Dotnet.Libraries.Accounts.Ui\Views\Panels\AccountConsolePanelView.xaml"] = new[] { "ClickClose" },
        [@"Ironwall.Dotnet.Libraries.Accounts.Ui\Views\Panels\AccountSetupPanelView.xaml"] = new[] { "ClickReload", "ClickSave" },
        [@"Ironwall.Dotnet.Libraries.Reports.Ui\Views\Panels\ReportCreateDetailView.xaml"] = Array.Empty<string>(),
        [@"Ironwall.Dotnet.Libraries.Gateway\Views\GatewaySetupView.xaml"] = Array.Empty<string>(),
    };

    public static readonly IReadOnlyDictionary<string, string[]> DateIds = new Dictionary<string, string[]>
    {
        [@"Ironwall.Dotnet.Libraries.Accounts.Ui\Views\Panels\AccountConsolePanelView.xaml"] = new[]
        {
            "Console.Accounts.Audit.StartDate", "Console.Accounts.Audit.EndDate",
            "Accounts.Detail.Field.grant_from", "Accounts.Detail.Field.grant_until",
        },
        [@"Ironwall.Dotnet.Libraries.Reports.Ui\Views\Panels\ReportCreateDetailView.xaml"] = new[] { "Reports.Create.StartDatePicker", "Reports.Create.EndDatePicker" },
    };

    public static readonly IReadOnlyDictionary<string, string[]> SetupIds = new Dictionary<string, string[]>
    {
        [@"Ironwall.Dotnet.Libraries.Accounts.Ui\Views\Panels\AccountSetupPanelView.xaml"] = new[]
        {
            "Console.Accounts.SessionSetup.LockoutThreshold", "Console.Accounts.SessionSetup.Revert",
            "Console.Accounts.SessionSetup.Save", "Console.Accounts.SessionSetup.Changes",
        },
    };

    public static IEnumerable<object[]> Files() => XNames.Keys.Select(k => new object[] { k });

    // ── 파일 계약 ────────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(Files))]
    public void should_keep_exactly_the_previous_xnames_when_the_view_is_redesigned(string file)
    {
        var names = Load(file).Descendants()
            .Select(e => (string?)e.Attribute(XNs + "Name"))
            .Where(n => n is not null)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(XNames[file].OrderBy(n => n, StringComparer.Ordinal).ToArray(), names);
    }

    [Theory]
    [MemberData(nameof(Files))]
    public void should_use_tokens_only_and_no_legacy_inputs_when_the_view_is_redesigned(string file)
    {
        var doc = Load(file);
        var text = Regex.Replace(File.ReadAllText(System.IO.Path.Combine(RepoRoot(), file)), "<!--.*?-->", string.Empty, RegexOptions.Singleline);

        var literals = doc.Descendants().SelectMany(e => e.Attributes())
            .Where(a => a.Name.NamespaceName != "http://schemas.microsoft.com/expression/blend/2008")
            .Where(a => Regex.IsMatch(a.Value, "^#[0-9A-Fa-f]{3,8}$"))
            .Select(a => $"{a.Parent!.Name.LocalName}.{a.Name.LocalName}={a.Value}")
            .ToList();
        Assert.True(literals.Count == 0, $"{file}: 하드코딩 색 {string.Join(", ", literals)}");

        // 옛 날짜 피커 · 호스트 전용 입력 스타일이 남지 않는다
        Assert.DoesNotContain(doc.Descendants(), e => e.Name.LocalName is "DatePicker" or "DateTimePicker" or "TimePicker");
        Assert.DoesNotContain("Console.DateTimePicker", text, StringComparison.Ordinal);
        Assert.False(Regex.IsMatch(text, @"StaticResource\s+TextBoxInput\}"), $"{file}: 호스트 전용 TextBoxInput 을 찾는다");
    }

    [Theory]
    [MemberData(nameof(Files))]
    public void should_keep_every_previous_date_and_setup_automation_id_when_the_view_is_redesigned(string file)
    {
        var elements = Load(file).Descendants().ToList();
        foreach (var id in DateIds.TryGetValue(file, out var dates) ? dates : Array.Empty<string>())
        {
            var element = Assert.Single(elements, e => (string?)e.Attribute("AutomationProperties.AutomationId") == id);
            Assert.Equal("DateTimeField", element.Name.LocalName);   // 그 id 를 커널 칸이 단다
        }
        foreach (var id in SetupIds.TryGetValue(file, out var setup) ? setup : Array.Empty<string>())
            Assert.Single(elements, e => (string?)e.Attribute("AutomationProperties.AutomationId") == id);
    }

    // ── 계정 콘솔: 감사 기간(#49) · 한시 부여(#50) ─────────────────────

    [Fact]
    public void should_bind_the_kernel_date_fields_to_the_audit_and_grant_view_models_when_the_accounts_console_loads() => AppHost.Run(() =>
    {
        var context = new FakeAccountsConsole();
        var view = new AccountConsolePanelView { Width = 1400, Height = 900, DataContext = context };
        var window = AppHost.Show(view);
        try
        {
            var start = Field(view, "Console.Accounts.Audit.StartDate");
            var end = Field(view, "Console.Accounts.Audit.EndDate");
            var from = Field(view, "Accounts.Detail.Field.grant_from");
            var until = Field(view, "Accounts.Detail.Field.grant_until");

            // 감사 — 날짜만 · 비울 수 없음 · 뷰모델 경로 그대로
            Assert.Equal("AuditLogPanelViewModel.StartDate", Path(start));
            Assert.Equal("AuditLogPanelViewModel.EndDate", Path(end));
            Assert.False(start.IncludeTime);
            Assert.False(start.AllowEmpty);
            Assert.Equal(new DateTime(2026, 9, 1), start.Value);
            Assert.Equal("2026-09-01", start.InputBox!.Text);

            // 부여 — 날짜+시각 · 종료만 비울 수 있음
            Assert.Equal("GrantManagementPanelViewModel.ValidFrom", Path(from));
            Assert.Equal("GrantManagementPanelViewModel.ValidUntil", Path(until));
            Assert.True(from.IncludeTime && until.IncludeTime);
            Assert.False(from.AllowEmpty);
            Assert.True(until.AllowEmpty);
            Assert.Equal("2026-09-27 09:30", from.InputBox!.Text);
            Assert.Equal(string.Empty, until.InputBox!.Text);

            // 두 방향 — 칸에 적은 값이 뷰모델(DateTime / DateTime?)까지 닿는다
            start.InputBox.Text = "2026-08-15";
            until.InputBox.Text = "2026-10-01 18:00";
            AppHost.Pump();
            Assert.Equal(new DateTime(2026, 8, 15), context.AuditLogPanelViewModel.StartDate);
            Assert.Equal(new DateTime(2026, 10, 1, 18, 0, 0), context.GrantManagementPanelViewModel.ValidUntil);

            // 비울 수 없는 칸(DateTime)은 빈 글자를 확정하지 않는다 — 뷰모델은 그대로
            from.InputBox.Text = string.Empty;
            AppHost.Pump();
            Assert.Equal(new DateTime(2026, 9, 27, 9, 30, 0), context.GrantManagementPanelViewModel.ValidFrom);

            // UIA — 칸 자신이 옛 id 로 나오고, 안에 글 칸(Edit)이 있다
            AssertAutomation(start, "Console.Accounts.Audit.StartDate");
            AssertAutomation(until, "Accounts.Detail.Field.grant_until");
        }
        finally { window.Close(); }
    });

    // ── 보고서 생성 기간(#51) ────────────────────────────────────────

    [Fact]
    public void should_bind_the_kernel_date_fields_to_the_create_view_model_when_the_report_form_loads() => AppHost.Run(() =>
    {
        var create = new ReportCreateViewModel(new EventAggregator(), Mock.Of<ILogService>(), Mock.Of<IReportApiService>()) { IsCustomRange = true };
        var view = new ReportCreateDetailView { Width = 420, DataContext = new { CreateViewModel = create } };
        var window = AppHost.Show(view);
        try
        {
            var start = Field(view, "Reports.Create.StartDatePicker");
            var end = Field(view, "Reports.Create.EndDatePicker");

            Assert.Equal("CreateViewModel.StartDate", Path(start));
            Assert.Equal("CreateViewModel.EndDate", Path(end));
            Assert.Equal(DateTimeFieldText.Format(DateTime.Today.AddDays(-7), false), start.InputBox!.Text);
            Assert.Equal(DateTimeFieldText.Format(DateTime.Today, false), end.InputBox!.Text);
            Assert.True(start.ActualWidth > 0 && start.ActualWidth <= 246, $"값 칸(도킹 380 → 약 246)보다 넓다: {start.ActualWidth}");

            // AppSweep T-ACT009 의 길 — 칸 안 Edit 에 ValuePattern 으로 적으면 뷰모델까지 닿는다
            var peer = AssertAutomation(start, "Reports.Create.StartDatePicker");
            var edit = peer.GetChildren()!.Single(c => c.GetAutomationControlType() == AutomationControlType.Edit);
            ((System.Windows.Automation.Provider.IValueProvider)edit.GetPattern(PatternInterface.Value)).SetValue("2026-08-28");
            AppHost.Pump();
            Assert.Equal(new DateTime(2026, 8, 28), create.StartDate);

            // 옛 피커처럼 비울 수 있다(뷰모델 DateTime?) — [생성] 의 "지정하세요" 검증이 그대로 뷰모델에 남는다
            Assert.True(start.AllowEmpty && end.AllowEmpty);
            end.InputBox!.Text = string.Empty;
            AppHost.Pump();
            Assert.Null(create.EndDate);
            Assert.Equal("시작일", AutomationProperties.GetName(start));
        }
        finally { window.Close(); }
    });

    // ── B10 #12a: 세션 설정 ─────────────────────────────────────────

    [Fact]
    public void should_render_session_setup_with_console_sections_fields_and_token_inputs_when_hosted() => AppHost.Run(() =>
    {
        var vm = new AccountSetupPanelViewModel(new EventAggregator(), Mock.Of<ILogService>());
        var (view, window) = HostWithCaliburn(new AccountSetupPanelView(), vm);
        try
        {
            var visuals = SelfService.Visuals(view).ToList();
            Assert.Equal(2, visuals.OfType<ConsoleSection>().Count());
            Assert.True(visuals.OfType<ConsoleField>().Count() >= 9, "칸마다 ConsoleField 로 서야 한다");

            // 글 칸은 전부 토큰 스타일(Acct.TextBox 계열) — 옛 MDIX 밑줄 칸이 아니다
            var boxes = visuals.OfType<TextBox>().Where(b => b.TemplatedParent is null).ToList();   // 콤보 템플릿 안 글 칸은 뺀다
            Assert.Equal(6, boxes.Count);
            var acct = (Style)view.FindResource("Acct.TextBox");
            Assert.All(boxes, b => Assert.True(b.Style == acct || b.Style?.BasedOn == acct, "Acct.TextBox 가 아니다"));

            // x:Name — Caliburn 이 [저장] 을 이었다(CanClickSave 가드가 IsEnabled 로 붙는다)
            Assert.IsType<Button>(view.FindName("ClickSave"));
            Assert.IsType<Button>(view.FindName("ClickReload"));

            // 잠금 임계 칸 — id 로 찾고, 적으면 뷰모델에 닿는다
            var lockout = boxes.Single(b => AutomationProperties.GetAutomationId(b) == "Console.Accounts.SessionSetup.LockoutThreshold");
            lockout.Text = "7";
            AppHost.Pump();
            Assert.Equal(7, vm.LockoutThreshold);

            // UIA — 라벨 글이 기본 보기에 나온다(호스트 EditBesideLabel 폴백 "잠금까지 비밀번호 오류 횟수") · 콤보는 하나(T-ACC034)
            var peers = SelfService.ControlPeers(view).ToList();
            Assert.Contains(peers, p => p.GetAutomationControlType() == AutomationControlType.Text && SelfService.Name(p) == "잠금까지 비밀번호 오류 횟수");
            Assert.Single(peers, p => p.GetAutomationControlType() == AutomationControlType.ComboBox);
            Assert.Equal(6, peers.Count(p => p.GetAutomationControlType() == AutomationControlType.Edit));
            Assert.Contains(peers, p => p.GetAutomationId() == "ClickSave" || p.GetAutomationId() == "Console.Accounts.SessionSetup.Save");
        }
        finally { window.Close(); }
    });

    // ── B10 #18a: 외부 연동 이벤트 ──────────────────────────────────

    [Fact]
    public void should_render_gateway_events_with_the_console_grid_and_keep_the_total_anchor_when_hosted() => AppHost.Run(() =>
    {
        var log = Mock.Of<ILogService>();
        var vm = new GatewaySetupViewModel(new EventAggregator(), log, Mock.Of<IGatewayDbService>(), new GatewayEventProvider(log), new DeviceGroupProvider(log));
        var view = new GatewaySetupView { Width = 760, DataContext = vm };
        var window = AppHost.Show(view);
        try
        {
            var visuals = SelfService.Visuals(view).ToList();
            Assert.Single(visuals.OfType<ConsoleSection>());
            var grid = Assert.Single(visuals.OfType<DataGrid>());
            Assert.Same(view.FindResource("Console.DataGrid"), grid.Style);
            Assert.False(grid.CanUserSortColumns);          // 커널 그리드 — 정렬 · 열 이동 끔
            Assert.False(grid.CanUserReorderColumns);

            // 호스트 SetupSweep T-SET018 앵커 — "전체 개수" 글이 기본 보기에 나온다
            var texts = SelfService.ControlPeers(view).Where(p => p.GetAutomationControlType() == AutomationControlType.Text).Select(SelfService.Name).ToList();
            Assert.Contains(texts, t => t.Contains("전체 개수", StringComparison.Ordinal));

            // 단추 넷은 그대로 커널 단추
            var buttons = visuals.OfType<Button>().Where(b => b.Style == view.FindResource("Console.Button")).ToList();
            Assert.Equal(4, buttons.Count);
        }
        finally { window.Close(); }
    });

    // ── 스냅숏(육안 검토) ────────────────────────────────────────────

    /// <summary><c>B7_SNAPSHOT_DIR</c> 가 있을 때만 라이트 · 다크 PNG 를 남긴다 — 뷰 넷 + 펼친 날짜 팝업 둘(날짜 · 날짜+시각).</summary>
    [Fact]
    public void should_render_changed_views_and_open_date_popups_in_light_and_dark_when_snapshots_are_requested() => AppHost.Run(() =>
    {
        var directory = Environment.GetEnvironmentVariable("B7_SNAPSHOT_DIR");
        var windows = new List<(string Name, Window Window, FrameworkElement Root)>();
        try
        {
            var accounts = new AccountConsolePanelView { Width = 1400, Height = 900, DataContext = new FakeAccountsConsole() };
            windows.Add(("accounts-console", AppHost.Show(accounts), accounts));

            var create = new ReportCreateViewModel(new EventAggregator(), Mock.Of<ILogService>(), Mock.Of<IReportApiService>()) { IsCustomRange = true };
            var report = new ReportCreateDetailView { Width = 420, DataContext = new { CreateViewModel = create } };
            windows.Add(("report-create", AppHost.Show(report), report));

            var setup = new AccountSetupPanelView { Width = 760, Height = 720 };
            var (_, setupWindow) = HostWithCaliburn(setup, new AccountSetupPanelViewModel(new EventAggregator(), Mock.Of<ILogService>()));
            windows.Add(("session-setup", setupWindow, setup));

            var log = Mock.Of<ILogService>();
            var gatewayVm = new GatewaySetupViewModel(new EventAggregator(), log, Mock.Of<IGatewayDbService>(), new GatewayEventProvider(log), new DeviceGroupProvider(log))
            {
                IsVisible = true,
                IsButtonEnable = true,
                ReloadButtonEnable = true,
                SaveButtonEnable = true,
            };
            gatewayVm.ViewModelProvider.Add(new GatewayEventViewModel(new Ironwall.Dotnet.Monitoring.Models.GatewayEvents.GatewayEventModel { EventName = "정문 출입 통제", Description = "게이트웨이 1번 입력" }));
            gatewayVm.ViewModelProvider.Add(new GatewayEventViewModel(new Ironwall.Dotnet.Monitoring.Models.GatewayEvents.GatewayEventModel { EventName = "후문 화재 신호", IsEnable = false, Description = "비상 연동" }));
            var gateway = new GatewaySetupView { Width = 760, DataContext = gatewayVm };
            windows.Add(("gateway-events", AppHost.Show(gateway), gateway));

            var auditStart = Field(accounts, "Console.Accounts.Audit.StartDate");
            var grantFrom = Field(accounts, "Accounts.Detail.Field.grant_from");

            foreach (var theme in new[] { "light", "dark" })
            {
                AppHost.SetDark(theme == "dark");
                foreach (var (name, window, root) in windows)
                {
                    window.UpdateLayout();
                    Assert.True(root.ActualWidth > 0 && root.ActualHeight > 0, $"{name}: 크기 0");
                    if (directory is not null) AppHost.Save(window, System.IO.Path.Combine(directory, $"{theme}-{name}.png"));
                }

                foreach (var (name, field) in new[] { ("popup-date", auditStart), ("popup-datetime", grantFrom) })
                {
                    field.IsDropDownOpen = true;
                    AppHost.Pump();
                    var content = Assert.IsAssignableFrom<FrameworkElement>(field.PopupContent);
                    content.UpdateLayout();
                    Assert.True(content.ActualWidth > 0, $"{name}: 팝업 크기 0");
                    if (directory is not null) SaveElement(content, System.IO.Path.Combine(directory, $"{theme}-{name}.png"));
                    field.IsDropDownOpen = false;
                    AppHost.Pump();
                }
            }
        }
        finally
        {
            AppHost.SetDark(false);
            foreach (var (_, window, _) in windows) window.Close();
        }
    });

    // ── 도우미 ──────────────────────────────────────────────────────

    private static readonly XNamespace XNs = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static string RepoRoot([CallerFilePath] string? thisFile = null)
        => System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(thisFile)!, "..", ".."));

    private static XDocument Load(string relative) => XDocument.Load(System.IO.Path.Combine(RepoRoot(), relative));

    /// <summary>시각 · 논리 트리를 함께 걸어 id 로 칸을 찾는다(콘솔 틀의 칸 속성 안은 논리 트리로만 닿는 곳이 있다).</summary>
    private static DateTimeField Field(DependencyObject root, string id)
    {
        var found = All(root).OfType<DateTimeField>().Distinct().Where(f => AutomationProperties.GetAutomationId(f) == id).ToList();
        var field = Assert.Single(found);
        field.ApplyTemplate();
        return field;
    }

    private static IEnumerable<DependencyObject> All(DependencyObject root)
    {
        var seen = new HashSet<DependencyObject>();
        var stack = new Stack<DependencyObject>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (!seen.Add(node)) continue;
            yield return node;
            if (node is Visual or System.Windows.Media.Media3D.Visual3D)
                for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++) stack.Push(VisualTreeHelper.GetChild(node, i));
            foreach (var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>()) stack.Push(child);
        }
    }

    private static string? Path(DateTimeField field)
        => BindingOperations.GetBinding(field, DateTimeField.ValueProperty)?.Path.Path;

    private static AutomationPeer AssertAutomation(DateTimeField field, string id)
    {
        var peer = Assert.IsType<DateTimeFieldAutomationPeer>(UIElementAutomationPeer.CreatePeerForElement(field));
        Assert.Equal(id, peer.GetAutomationId());
        Assert.NotNull(peer.GetPattern(PatternInterface.ExpandCollapse));
        Assert.NotNull(peer.GetPattern(PatternInterface.Value));
        Assert.Contains(peer.GetChildren()!, c => c.GetAutomationControlType() == AutomationControlType.Edit);
        return peer;
    }

    /// <summary>호스트 ContentControl(cal:View.Model) 과 같은 길로 뷰모델을 붙인다 — x:Name 관례(ClickSave …)가 걸린다.</summary>
    private static (UserControl View, Window Window) HostWithCaliburn(UserControl view, object viewModel)
    {
        PlatformProvider.Current = new XamlPlatformProvider();
        IoC.BuildUp = _ => { };
        IoC.GetAllInstances = _ => Array.Empty<object>();
        IoC.GetInstance = (_, _) => null!;
        ((IViewAware)viewModel).AttachView(view);
        var host = new ContentControl();
        var window = AppHost.Show(host);
        View.SetModel(host, viewModel);
        AppHost.Pump(System.Windows.Threading.DispatcherPriority.Loaded);
        AppHost.Pump();
        return (view, window);
    }

    private static void SaveElement(FrameworkElement element, string path)
    {
        var width = (int)Math.Ceiling(element.ActualWidth);
        var height = (int)Math.Ceiling(element.ActualHeight);
        if (width <= 0 || height <= 0) return;
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        // 팝업 판은 창 밖 층에 떠 있어 바탕이 없다 — 창 바탕 토큰을 먼저 깐다.
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle((Brush)Application.Current.FindResource("BgBrush"), null, new Rect(0, 0, width + 16, height + 16));
            dc.DrawRectangle(new VisualBrush(element), null, new Rect(8, 8, width, height));
        }
        var bitmap = new RenderTargetBitmap(width + 16, height + 16, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path);
        encoder.Save(file);
    }

    // ── 가짜 콘솔 문맥 — 바인딩은 이름으로 풀리므로 계정 콘솔 뷰모델 전체를 세우지 않는다 ──

    public sealed class FakeAudit : PropertyChangedBase
    {
        private DateTime _startDate = new(2026, 9, 1);
        private DateTime _endDate = new(2026, 9, 27);
        public DateTime StartDate { get => _startDate; set { _startDate = value; NotifyOfPropertyChange(); } }
        public DateTime EndDate { get => _endDate; set { _endDate = value; NotifyOfPropertyChange(); } }
    }

    public sealed class FakeGrant : PropertyChangedBase
    {
        private DateTime _validFrom = new(2026, 9, 27, 9, 30, 0);
        private DateTime? _validUntil;
        public DateTime ValidFrom { get => _validFrom; set { _validFrom = value; NotifyOfPropertyChange(); } }
        public DateTime? ValidUntil { get => _validUntil; set { _validUntil = value; NotifyOfPropertyChange(); } }
        public ObservableCollection<object> Accounts { get; } = new();
        public ObservableCollection<object> Groups { get; } = new();
        public bool CanCreateGrant => false;
    }

    public sealed class FakeAccountsConsole : PropertyChangedBase
    {
        public FakeAudit AuditLogPanelViewModel { get; } = new();
        public FakeGrant GrantManagementPanelViewModel { get; } = new();
        public bool IsAuditRail => true;
        public bool IsGrantsRail => true;
        public bool IsDetailRequested => true;
    }
}
