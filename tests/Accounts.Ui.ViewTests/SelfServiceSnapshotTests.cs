using System.IO;
using Ironwall.Dotnet.Libraries.Accounts.Ui.ViewModels.Panels;
using Xunit;

namespace Accounts.Ui.ViewTests;

/// <summary>
/// 육안 검토용 라이트 · 다크 스냅숏 — <c>B3_SNAPSHOT_DIR</c> 가 있을 때만 PNG 를 남긴다(없으면 렌더만 하고 끝).
/// </summary>
/// <remarks>
/// 창은 화면 밖(-20000)에 뜬다. 테마는 호스트 ThemeService 와 같은 세 가지(토큰 · MD 바탕 · MahApps 크롬)를 Application 수준에서 바꾼다 —
/// 창 안의 요소만 DynamicResource 가 다시 풀리므로 뷰는 반드시 창에 올린 채 바꾼다(memory offscreen_panel_render_needs_application).
/// </remarks>
public class SelfServiceSnapshotTests
{
    [Fact]
    public void should_render_every_self_service_view_in_light_and_dark_when_snapshots_are_requested() => AppHost.Run(() =>
    {
        var directory = Environment.GetEnvironmentVariable("B3_SNAPSHOT_DIR");
        var hosted = new List<(string Name, Hosted Host)>();
        try
        {
            foreach (var name in SelfServiceViewTests.Names.Keys)
                hosted.Add((name, SelfService.Host(name, canSelfDelete: name != "MyPage")));

            // 로그인 실패 상태 하나 더 — 결과 띠(막대 + 글리프 + 글)
            var failed = SelfService.Host("Login");
            var login = (LoginPanelViewModel)failed.ViewModel;
            login.Username = "operator01";
            login.Result = "아이디 또는 비밀번호가 일치하지 않습니다. (5회 중 2회 실패, 3회 남음)";
            login.IsLoginFailed = true;
            hosted.Add(("Login-failed", failed));
            AppHost.Pump();

            foreach (var theme in new[] { "light", "dark" })
            {
                AppHost.SetDark(theme == "dark");
                foreach (var (name, host) in hosted)
                {
                    host.Window.UpdateLayout();
                    Assert.True(host.View.ActualWidth > 0 && host.View.ActualHeight > 0, $"{name}: 뷰 크기 0");
                    if (directory is not null)
                        AppHost.Save(host.Window, Path.Combine(directory, $"{theme}-{name}.png"));
                }
            }
        }
        finally
        {
            AppHost.SetDark(false);
            foreach (var (_, host) in hosted) host.Window.Close();
        }
    });
}
