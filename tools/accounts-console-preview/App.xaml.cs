using Caliburn.Micro;
using Ironwall.Dotnet.Libraries.Accounts.Api.Services;
using Ironwall.Dotnet.Libraries.Accounts.Providers;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Consoles;
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels;
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Dialogs;
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Panels;
using Ironwall.Dotnet.Libraries.Accounts.Ui.Views.Panels;
using Ironwall.Dotnet.Libraries.Enums;
using Ironwall.Dotnet.Libraries.Messages.Dto.Accounts;
using Ironwall.Dotnet.Monitoring.Models.Accounts;
using MaterialDesignThemes.Wpf;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AccountsConsolePreview;

/// <summary>
/// 계정 콘솔 미리보기 — <b>진짜 뷰 + 진짜 뷰모델</b>을 가짜 데이터 위에 띄운다(호스트 앱 · 서버 없음).
/// <c>--snapshot &lt;폴더&gt;</c> 면 입력 없이 여러 상태를 PNG 로 떠 놓고 끝낸다. <c>--dark</c> 면 다크로 시작한다.
/// </summary>
public partial class App : Application
{
    private const string DarkTokens = "pack://application:,,,/Ironwall.Dotnet.Libraries.Theme;component/Themes/Tokens.Dark.xaml";

    private AccountConsolePanelViewModel _viewModel = null!;
    private AccountConsolePanelView _view = null!;
    private Window _window = null!;
    private bool _surface;

    private async void OnStartup(object sender, StartupEventArgs e)
    {
        var at = Array.IndexOf(e.Args, "--snapshot");
        var directory = at >= 0 && at + 1 < e.Args.Length ? e.Args[at + 1] : null;

        try
        {
            if (e.Args.Contains("--dark")) ApplyDark();

            _viewModel = Build();
            _view = new AccountConsolePanelView { DataContext = _viewModel };
            _window = new Window
            {
                Title = "계정 콘솔 미리보기",
                Width = 1320,
                Height = 820,
                Background = (Brush)FindResource("SurfaceBrush"),
                Content = new Border { Padding = new Thickness(12), Background = (Brush)FindResource("SurfaceBrush"), Child = _view },
            };
            _surface = PreviewTools.Shared.OffscreenStage.ApplySurface(e.Args, _view, _window);
            PreviewTools.Shared.OffscreenStage.Hide(_window).Show();

            await ((IActivate)_viewModel).ActivateAsync();

            if (directory is null) return;

            Directory.CreateDirectory(directory);
            await RunSnapshotsAsync(directory);
        }
        catch (Exception ex)
        {
            if (directory is null) MessageBox.Show(ex.ToString());
            else File.WriteAllText(Path.Combine(directory, "snapshot-error.txt"), ex.ToString());
        }

        if (directory is not null) Shutdown();
    }

    #region - Composition -
    private static AccountConsolePanelViewModel Build()
    {
        var events = new EventAggregator();
        var log = new PreviewLog();
        var permission = new PreviewPermission();
        var api = new PreviewApi();
        var directory = new PreviewDirectory();

        // 패널이 정적 IoC 로 의존을 찾는다(권한 · API) — 컨테이너 대신 여기서 대 준다.
        IoC.GetInstance = (type, _) =>
            type == typeof(IPermissionService) ? permission
            : type == typeof(IAccountApiService) ? api
            : type == typeof(IEventAggregator) ? events
            : null!;
        IoC.GetAllInstances = _ => Array.Empty<object>();
        IoC.BuildUp = _ => { };

        // 호스트는 부트스트래퍼가 해 준다 — 없으면 Execute.BeginOnUIThread 가 제자리(작업 스레드)에서 돌아
        // 콘솔이 교차 스레드로 화면을 만진다.
        PlatformProvider.Current = new XamlPlatformProvider();

        // 뷰 찾기(ViewLocator)는 부트스트래퍼가 등록해 주는 어셈블리 목록을 본다 — 없으면 뷰 대신 타입 이름이 찍힌다.
        if (!AssemblySource.Instance.Contains(typeof(AccountConsolePanelViewModel).Assembly))
            AssemblySource.Instance.Add(typeof(AccountConsolePanelViewModel).Assembly);

        PreviewData.Fill(directory, api);

        var provider = new AccountProvider();
        var login = new LoginViewModel(events, log, new AccountModel());
        var editor = new EditorDialogViewModel(events, log, new AccountViewModel(events, log, new AccountModel()),
                                               directory, new PreviewSessionConfig(), new PreviewProfileImages(), new PreviewProfileGateway());

        return new AccountConsolePanelViewModel(
            events, log, permission, api, directory,
            new AccountManagerPanelViewModel(events, log, editor, provider, login, directory),
            new PermissionMatrixPanelViewModel(events, log, api),
            new UserSessionPanelViewModel(events, log, api, new PreviewTokenStore()),
            new AuditLogPanelViewModel(events, log, api),
            new AccountSetupPanelViewModel(events, log),
            new GrantManagementPanelViewModel(events, log, api));
    }
    #endregion

    #region - Snapshots -
    private async Task RunSnapshotsAsync(string directory)
    {
        foreach (var theme in new[] { "light", "dark" })
        {
            if (theme == "dark")
            {
                ApplyDark();
                _window.Background = (Brush)FindResource("SurfaceBrush");
                await Settle();
            }

            // ① 사용자 — 선택 없음
            await _viewModel.SelectRailAsync(AccountConsoleKeys.Users);
            await Settle();
            Save(directory, $"{theme}-01-users-empty");

            // ② 한 명 선택(상세 칸 · 잠금 · 세션 요약 · 파괴 동작)
            var grid = FindGrid("Console.Accounts.Grid.Users");
            grid.SelectedItem = grid.Items[0];
            await Settle();
            Save(directory, $"{theme}-02-users-single");

            // ③ 미적용 변경
            _viewModel.Form.Fields.First(f => f.Key == "department").Text = "경비1과(수정)";
            await Settle();
            Save(directory, $"{theme}-03-users-dirty");
            _viewModel.Revert();

            // ④ 여러 명 — "— 여러 값 —" · 식별 칸 잠금
            grid.SelectedItems.Clear();
            foreach (var row in grid.Items.Cast<object>().Take(3)) grid.SelectedItems.Add(row);
            await Settle();
            Save(directory, $"{theme}-04-users-multi");

            // ⑤ 사용자 → 권한 그룹 Draft(드래그의 결과 · 칩 클릭 폴백과 같은 경로)
            _viewModel.AssignSelectionToGroup(_viewModel.GroupChips.First(c => c.Name == "야간 관제"));
            await Settle();
            Save(directory, $"{theme}-05-users-draft");
            _viewModel.RevertDraft();

            // ⑤-b 자기 계정을 끌면 막힌다(스쳐 고른 뒤 떨어뜨려 스스로 권한을 잃는 사고 방지)
            grid.SelectedItems.Clear();
            grid.SelectedItem = grid.Items.Cast<object>()
                .First(r => ((Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.AccountViewModel)r).Username == "admin");
            await Settle();
            _viewModel.AssignSelectionToGroup(_viewModel.GroupChips.First(c => c.Name == "조회 전용"));
            await Settle();
            Save(directory, $"{theme}-05b-users-self-blocked");
            _viewModel.RevertDraft();
            grid.SelectedItems.Clear();
            await Settle();

            // ⑥ 권한 설정 — 가운데 칸 = 그룹 칩 + [그룹|구성원] + 매트릭스, 상세 300 = 요약 · 주의 · 저장(목업 L1205-L1234)
            await _viewModel.SelectRailAsync(AccountConsoleKeys.Permissions);
            await Settle();
            _viewModel.Matrix.SelectedGroup = _viewModel.Matrix.Groups.FirstOrDefault();
            await Settle();
            Save(directory, $"{theme}-06-permissions-matrix");

            // ⑦ 드래그 페인팅의 결과(같은 행을 한 번에 켠 상태) — 미적용 변경
            _viewModel.Matrix.ToggleRow(0);
            await Settle();
            Save(directory, $"{theme}-07-permissions-painted");

            // ⑧ 미적용 변경 상태에서 다른 그룹을 누르면 막힌다(칠해 둔 것이 조용히 사라지지 않는다)
            _viewModel.Matrix.SelectedGroup = _viewModel.Matrix.Groups.Last();
            await Settle();
            Save(directory, $"{theme}-08-permissions-blocked");
            _viewModel.Revert();

            // ⑨ 구성원 칩
            _viewModel.Matrix.ShowMembers = true;
            await Settle();
            Save(directory, $"{theme}-09-permissions-members");
            _viewModel.Matrix.ShowMembers = false;

            // ⑨ 세션 관리
            await _viewModel.SelectRailAsync(AccountConsoleKeys.Sessions);
            await Settle();
            var sessions = FindGrid("Console.Accounts.Grid.Sessions");
            if (sessions.Items.Count > 0) sessions.SelectedItem = sessions.Items[0];
            await Settle();
            Save(directory, $"{theme}-10-sessions");

            // ⑩ 권한 부여
            await _viewModel.SelectRailAsync(AccountConsoleKeys.Grants);
            await Settle();
            Save(directory, $"{theme}-11-grants");

            // ⑪ 감사 로그
            await _viewModel.SelectRailAsync(AccountConsoleKeys.Audit);
            await Settle();
            var audit = FindGrid("Console.Accounts.Grid.Audit");
            if (audit.Items.Count > 0) audit.SelectedItem = audit.Items[0];
            await Settle();
            Save(directory, $"{theme}-12-audit");

            // ⑫ 세션 설정(상세 칸 없음)
            await _viewModel.SelectRailAsync(AccountConsoleKeys.SessionSetup);
            await Settle();
            Save(directory, $"{theme}-13-session-setup");

            // 좁은 폭 — 서랍(960~1279) · 접힘(<960), 상세 열림/닫힘. --surface 면 콘솔 폭 자체를 줄인다.
            var wideView = _view.Width;
            var wideWindow = _window.Width;
            await _viewModel.SelectRailAsync(AccountConsoleKeys.Users);
            await Settle();
            var users = FindGrid("Console.Accounts.Grid.Users");
            foreach (var width in new[] { 1150.0, 900.0 })
            {
                if (_surface && width >= wideView) continue;       // 표면보다 넓은 "좁은 폭" 은 뜻이 없다
                PreviewTools.Shared.OffscreenStage.SetWidth(_window, _view, width);
                users.SelectedItem = users.Items[0];
                await Settle();
                Save(directory, $"{theme}-14-users-{width:0}-open");
                users.SelectedItems.Clear();
                await Settle();
                Save(directory, $"{theme}-15-users-{width:0}-closed");
            }
            if (_surface) PreviewTools.Shared.OffscreenStage.SetWidth(_window, _view, wideView);
            else _window.Width = wideWindow;
            await Settle();
        }
    }

    private void ApplyDark()
    {
        if (Resources.MergedDictionaries.Any(d => d.Source?.OriginalString == DarkTokens)) return;
        Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(DarkTokens) });
        foreach (var bundled in Resources.MergedDictionaries.OfType<BundledTheme>()) bundled.BaseTheme = BaseTheme.Dark;
        // 호스트(ThemeService.SyncMaterialDesignAndMahApps)는 MD 색만이 아니라 MahApps 크롬도 같이 바꾼다
        // (ThemeManager.Current.ChangeTheme(app, "Dark.Cyan")). 여기선 그 한 줄만 그대로 거울처럼 부른다 —
        // 안 부르면 이 콘솔이 쓰는 MahApps 스타일 컨트롤이 다크에서도 라이트 크롬으로 남는다.
        ControlzEx.Theming.ThemeManager.Current.ChangeTheme(this, "Dark.Cyan");
    }

    private static Task Settle() => Task.Delay(420);

    private DataGrid FindGrid(string automationId)
    {
        return Find(_view) ?? throw new InvalidOperationException($"{automationId} 를 찾지 못했다");

        DataGrid? Find(DependencyObject parent)
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is DataGrid grid && System.Windows.Automation.AutomationProperties.GetAutomationId(grid) == automationId) return grid;
                if (Find(child) is { } found) return found;
            }
            return null;
        }
    }

    private void Save(string directory, string name)
    {
        var content = (FrameworkElement)_window.Content;
        var width = (int)Math.Ceiling(content.ActualWidth);
        var height = (int)Math.Ceiling(content.ActualHeight);
        if (width <= 0 || height <= 0) return;

        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);

        // 바탕을 먼저 깔고(창 배경), 그 위에 시각 트리를 그대로 그린다.
        // VisualBrush 로 옮겨 그리면 창 상태에 따라 아무것도 안 실릴 때가 있다(실측) — 직접 Render 가 안전하다.
        var backdrop = new DrawingVisual();
        using (var dc = backdrop.RenderOpen())
            dc.DrawRectangle(_window.Background, null, new Rect(0, 0, width, height));
        bitmap.Render(backdrop);

        bitmap.Render(content);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(Path.Combine(directory, name + ".png")))
            encoder.Save(stream);

        PreviewTools.Shared.ClipAudit.Frame(directory, name, _view);
    }
    #endregion
}

/// <summary>미리보기 데이터 — 계정 · 그룹 · 세션 · 감사 · 부여.</summary>
public static class PreviewData
{
    public static void Fill(PreviewDirectory directory, PreviewApi api)
    {
        var people = new (int Id, string Login, string Name, EnumUserRole Role, string Dept, string Pos, bool Locked, int? Group)[]
        {
            (1, "admin", "김관리", EnumUserRole.ADMIN, "운영지원과", "과장", false, 10),
            (2, "op_night", "이야간", EnumUserRole.USER, "상황실", "반장", false, 10),
            (3, "op_day", "박주간", EnumUserRole.USER, "상황실", "주임", false, 11),
            (4, "maint01", "최정비", EnumUserRole.USER, "정비과", "대리", true, 11),
            (5, "guard07", "정경비", EnumUserRole.USER, "경비과", "사원", false, null),
            (6, "guard08", "한경비", EnumUserRole.USER, "경비과", "사원", false, null),
            (7, "viewer01", "오조회", EnumUserRole.USER, "지휘통제실", "주임", false, 12),
            // 실서버에 있는 긴 값(2026-09-26 실창 캡처) — 아이디 · 성명 열이 잘리는지 보려면 이 길이가 필요하다.
            (8, "BroadcastingManager", "Broadcasting 매니저", EnumUserRole.ADMIN, "", "", false, null),
            (9, "EnclosureManager", "Enclosure 매니저", EnumUserRole.ADMIN, "", "", false, null),
        };

        foreach (var p in people)
        {
            directory.Accounts.Add(new AccountModel
            {
                Id = p.Id,
                Username = p.Login,
                Name = p.Name,
                Role = p.Role,
                Level = RoleMappingHelper.ToLevel(p.Role),
                Used = EnumUsedType.USED,
                Department = p.Dept,
                Position = p.Pos,
                EmployeeNumber = $"S-{p.Id:0000}",
                Phone = $"010-2400-{1000 + p.Id}",
                EMail = $"{p.Login}@sensorway.co.kr",
                IsLocked = p.Locked,
                LockReason = p.Locked ? "로그인 5회 연속 실패" : null,
            });
            api.Users.Add(new AuthUserDto { Id = p.Id, LoginId = p.Login, Name = p.Name, GroupId = p.Group, Role = p.Role.ToString() });
        }

        api.Groups.Add(Group(10, "야간 관제", devices: true, events: true));
        api.Groups.Add(Group(11, "정비 지원", devices: true, events: false));
        api.Groups.Add(Group(12, "조회 전용", devices: false, events: false));

        for (var i = 0; i < 6; i++)
        {
            api.Sessions.Add(new UserSessionDto
            {
                Id = 9000 + i,
                UserId = 1 + (i % 4),
                LoginId = people[i % people.Length].Login,
                Role = people[i % people.Length].Role.ToString(),
                ClientId = i % 2 == 0 ? "central-ui" : "gis-ingest",
                IpAddress = $"10.20.4.{20 + i}",
                UserAgent = "WPF/2.8.0",
                IsActive = i < 4,
                CreatedAt = $"2026-09-{10 + i:00}T09:1{i}:00+09:00",
                ExpiresAt = $"2026-09-{11 + i:00}T09:1{i}:00+09:00",
                LogoutReason = i < 4 ? null : "DUPLICATE",
            });
        }

        var actions = new[] { "USER_LOGIN", "USER_CREATED", "PASSWORD_RESET", "SESSION_FORCED_LOGOUT", "USER_DELETED" };
        for (var i = 0; i < 8; i++)
        {
            api.AuditLogs.Add(new AuditLogDto
            {
                Id = 500 + i,
                CreatedAt = $"2026-09-{12 + (i % 6):00} 14:2{i}:11",
                ActionType = actions[i % actions.Length],
                ActionStatus = i % 4 == 3 ? "FAILURE" : "SUCCESS",
                ResourceType = "User",
                ResourceName = people[i % people.Length].Login,
                ActorLoginId = "admin",
                IpAddress = "10.20.4.11",
                Description = i % 4 == 3 ? "비밀번호가 정책에 맞지 않습니다" : "정상 처리",
            });
        }

        for (var i = 0; i < 3; i++)
        {
            api.Grants.Add(new GrantDto
            {
                Id = 700 + i,
                UserId = 2 + i,
                UserLogin = people[2 + i].Login,
                GroupId = 10,
                GroupName = "야간 관제",
                ValidFrom = new DateTime(2026, 9, 18, 18, 0, 0),
                ValidUntil = i == 2 ? null : new DateTime(2026, 9, 21 + i, 6, 0, 0),
                Status = i == 0 ? "ACTIVE" : i == 1 ? "PENDING" : "ACTIVE",
                CreatedAt = new DateTime(2026, 9, 17, 10, 0, 0),
            });
        }
    }

    private static UserGroupDto Group(int id, string name, bool devices, bool events)
    {
        var permissions = new PermissionsDto();
        permissions.Modules["devices"] = new ModulePermissionDto { View = true, Edit = devices, Control = devices };
        permissions.Modules["events"] = new ModulePermissionDto { View = true, Edit = events };
        permissions.Modules["reports"] = new ModulePermissionDto { View = true };
        return new UserGroupDto { Id = id, Name = name, IsActive = true, Permissions = permissions };
    }
}
