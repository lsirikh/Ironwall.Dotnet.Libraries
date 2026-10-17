using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map.Model;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/****************************************************************************
   Purpose      : 배치 판(layout_version) 올림 경로 — 서버 v8.0.4 · 회신 2026-09-29 §2 · §4.5 (PRD v1.8 FR-07-A)
   Created By   : GHLee
   Created On   : 9/29/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com

   Description  : 서버 시험 3건(tests/test_unit_layout.py)을 가짜 포트로 클라 쪽에 옮겼다 —
                  ① 판 1 문서 + Δ 3 → clear_all + 판 2 → 200 · 항목 0 · 판 2 · 버전(ETag) 변경 · 알림 1
                  ② 판 2 문서에 판 1 쓰기 → 422(내리기 없음) · 문서 불변
                  ③ 판 2 문서에 판 2 일반 쓰기 → 200
                  클라 알고리즘 판은 아직 1 이다(IMPL-73 은 S-3 sort_order 와 한 번에 올린다) — 새 판 클라는
                  UnitMapViewModelOptions.ClientLayoutVersion = 2 로 흉내 낸다(어댑터가 싣는 판과 같게 — MapKit).
****************************************************************************/
public class UnitMapLayoutBumpTests
{
    private const int OldVersion = 1;
    private const int NewVersion = 2;

    private static UnitLayoutRead Doc(int layoutVersion, long version = 7, params (int Id, double Dx, double Dy)[] items)
        => new UnitLayoutRead.Supported(new UnitLayoutSnapshot(version, layoutVersion, null, null,
            items.ToDictionary(i => i.Id, i => new Vector(i.Dx, i.Dy))));

    /// <summary>판 <paramref name="layoutVersion"/> 문서에 다른 운영자가 Δ 를 <paramref name="count"/> 곳 둔 서버.</summary>
    private static FakeUnitLayoutApi ServerWithDeltas(int layoutVersion, int count)
    {
        var api = new FakeUnitLayoutApi(layoutVersion: layoutVersion);
        var ids = UnitMapTestData.Standard200();
        var names = new[] { "6중대", "7중대", "8중대", "9중대" };
        for (var i = 0; i < count; i++) api.ForClient("seed", layoutVersion).WriteAsync(api.Current.Version,
            UnitLayoutChange.SetOne(ids.IdOf(names[i]), new Vector(30 + i, 10))).GetAwaiter().GetResult();
        return api;
    }

    #region - 순수 판정 -
    [Fact]
    public void should_bump_when_server_layout_version_is_lower_and_user_can_edit()
    {
        Assert.True(UnitMapLayoutSync.ShouldBumpLayoutVersion(Doc(OldVersion), canEdit: true, clientLayoutVersion: NewVersion));
    }

    [Theory]
    [InlineData(NewVersion, true)]      // 이미 같은 판
    [InlineData(3, true)]               // 서버가 더 높다 — 내리기 없음, 클라이언트 갱신 필요
    [InlineData(OldVersion, false)]     // 보기 권한뿐 — 올리지 않는다
    public void should_not_bump_when_versions_match_or_server_is_newer_or_user_cannot_edit(int serverLayoutVersion, bool canEdit)
    {
        Assert.False(UnitMapLayoutSync.ShouldBumpLayoutVersion(Doc(serverLayoutVersion), canEdit, NewVersion));
    }

    [Fact]
    public void should_not_bump_when_server_does_not_support_layout()
    {
        Assert.False(UnitMapLayoutSync.ShouldBumpLayoutVersion(new UnitLayoutRead.Unsupported("없음"), canEdit: true, NewVersion));
        Assert.False(UnitMapLayoutSync.ShouldBumpLayoutVersion(new UnitLayoutRead.Failed(UnitLayoutFailureKind.Server, "5xx"), canEdit: true, NewVersion));
    }

    [Fact]
    public void should_flag_server_newer_when_server_layout_version_is_higher_than_client()
    {
        var assessment = UnitMapLayoutSync.Classify(Doc(NewVersion), clientLayoutVersion: OldVersion);

        Assert.Equal(UnitMapLayoutState.VersionMismatch, assessment.State);
        Assert.True(assessment.ServerLayoutNewer);
        Assert.False(assessment.CanWrite);
    }

    [Fact]
    public void should_not_flag_server_newer_when_server_layout_version_is_older_than_client()
    {
        var assessment = UnitMapLayoutSync.Classify(Doc(OldVersion), clientLayoutVersion: NewVersion);

        Assert.Equal(UnitMapLayoutState.VersionMismatch, assessment.State);
        Assert.False(assessment.ServerLayoutNewer);
    }
    #endregion

    #region - 서버 시나리오 ① — 판 올림 -
    [Fact]
    public async Task should_bump_once_with_clear_all_and_if_match_when_server_doc_is_older_and_user_can_edit()
    {
        // Arrange — 판 1 문서에 Δ 3곳(버전 3). 새 판(2) 클라가 편집 권한으로 연다.
        var api = ServerWithDeltas(OldVersion, 3);
        var notices = new List<long>();
        api.Published = notices.Add;

        // Act
        var kit = await MapKit.OpenAsync(api, clientLayoutVersion: NewVersion);

        // Assert — 쓰기 1건: If-Match 3 · clear_all · 판 2. 서버: 항목 0 · 판 2 · 버전 +1 · 알림 1.
        var bump = Assert.Single(api.Writes.Where(w => w.Client == "main"));
        Assert.Equal(3, bump.IfMatch);
        Assert.True(bump.Change.ClearAll);
        Assert.Empty(bump.Change.Set);
        Assert.Equal(NewVersion, bump.RequestedLayoutVersion);
        Assert.IsType<UnitLayoutWrite.Saved>(bump.Result);
        Assert.Equal(NewVersion, api.Current.LayoutVersion);
        Assert.Empty(api.Current.Deltas);
        Assert.Equal(4, api.Current.Version);
        Assert.Equal(new long[] { 4 }, notices);

        // 화면 — 공유 · 자동 배치 그대로 · 막대 한 번(되돌리기 없음)
        Assert.Equal(UnitMapLayoutState.Shared, kit.Vm.LayoutState);
        Assert.Equal(UnitMapLayout.Compute(kit.F.Tree).Positions, kit.Vm.Scene.Positions);
        Assert.Equal(UnitMapText.LayoutVersionBumpedBar(3), kit.Vm.Bar!.Message);
        Assert.False(kit.Vm.Bar!.IsError);
        Assert.False(kit.Vm.Bar!.CanUndo);
    }

    [Fact]
    public async Task should_skip_own_bump_echo_when_notice_carries_the_new_version()
    {
        // 회신 §4.4 — resource_id(새 판)가 기억한 판 이하면 다시 읽지 않는다(자기 저장 알림).
        var api = ServerWithDeltas(OldVersion, 1);
        var kit = await MapKit.OpenAsync(api, clientLayoutVersion: NewVersion);
        var reads = api.ReadCount;

        await kit.Vm.HandleAsync(new Ironwall.Dotnet.Libraries.ViewModel.Models.UnitLayoutChangedMessage(api.Current.Version), default);
        kit.Delay.ElapseAll();
        await kit.Vm.WhenIdleAsync();

        Assert.Equal(reads, api.ReadCount);
    }

    [Fact]
    public async Task should_not_bump_again_when_another_new_client_opens_after_the_bump()
    {
        var api = ServerWithDeltas(OldVersion, 2);
        await MapKit.OpenAsync(api, clientLayoutVersion: NewVersion);
        var writes = api.Writes.Count;

        var second = await MapKit.OpenAsync(api, clientLayoutVersion: NewVersion);

        Assert.Equal(writes, api.Writes.Count);
        Assert.Equal(UnitMapLayoutState.Shared, second.Vm.LayoutState);
        Assert.Null(second.Vm.Bar);
    }

    [Fact]
    public async Task should_let_only_one_client_bump_when_two_new_clients_race()
    {
        // 회신 §2 — 판 올림도 If-Match 가 필요하다: 둘이 동시에 올려도 한쪽만 성공하고 다른 쪽은 412.
        var api = ServerWithDeltas(OldVersion, 2);
        var kit = MapKit.Create(api, clientLayoutVersion: NewVersion);
        kit.Gate.HoldWrites = true;

        var open = kit.Vm.OpenAsync();
        await kit.Gate.WhenWritesStartedAsync(1);                               // 내 판 올림이 If-Match 2 로 떠났다(아직 서버 전)
        var other = api.ForClient("B", NewVersion);
        var otherBump = await other.WriteAsync(api.Current.Version, UnitLayoutChange.LayoutVersionBump());
        kit.Gate.HoldWrites = false;
        kit.Gate.ReleaseNextWrite();
        await open;
        await kit.Vm.WhenIdleAsync();

        Assert.IsType<UnitLayoutWrite.Saved>(otherBump);
        var mine = Assert.Single(api.Writes.Where(w => w.Client == "main"));
        Assert.IsType<UnitLayoutWrite.Conflict>(mine.Result);                   // 412 — 두 번째 올림 없음
        Assert.Equal(NewVersion, api.Current.LayoutVersion);
        Assert.Equal(UnitMapLayoutState.Shared, kit.Vm.LayoutState);           // 다시 읽어 같은 판 = 공유
    }

    [Fact]
    public async Task should_stay_read_only_without_writing_when_server_doc_is_older_and_user_cannot_edit()
    {
        var api = ServerWithDeltas(OldVersion, 1);
        var writes = api.Writes.Count;

        var kit = await MapKit.OpenAsync(api, canEdit: false, clientLayoutVersion: NewVersion);

        Assert.Equal(writes, api.Writes.Count);
        Assert.Equal(OldVersion, api.Current.LayoutVersion);
        Assert.Equal(UnitMapLayoutState.VersionMismatch, kit.Vm.LayoutState);
        Assert.Equal(UnitMapText.LayoutStatusOldVersionViewOnly, kit.Vm.LayoutStatusText);
        Assert.Equal(UnitMapLayout.Compute(kit.F.Tree).Positions, kit.Vm.Scene.Positions);   // 옛 판 Δ 는 그리지 않는다
    }
    #endregion

    #region - 서버 시나리오 ② — 내리기 없음(클라이언트 갱신 필요) -
    [Fact]
    public async Task should_show_client_update_needed_and_never_write_when_server_layout_version_is_higher()
    {
        // 판 2 문서(새 클라가 이미 올렸다)를 옛 판(1) 클라가 연다 — 읽기 전용 · 쓰기 0.
        var api = ServerWithDeltas(NewVersion, 1);
        var writes = api.Writes.Count;

        var kit = await MapKit.OpenAsync(api, clientLayoutVersion: OldVersion);

        Assert.Equal(writes, api.Writes.Count);
        Assert.Equal(UnitMapLayoutState.VersionMismatch, kit.Vm.LayoutState);
        Assert.Equal(UnitMapText.LayoutStatusClientOutdated, kit.Vm.LayoutStatusText);
        Assert.Equal(UnitMapText.LayoutVersionBlocked, kit.Vm.Classify(kit.Id("6중대"), null, false).Reason);
    }

    [Fact]
    public async Task should_say_client_update_needed_when_write_is_rejected_for_layout_version()
    {
        // 열었을 때는 판이 같았는데 그 사이 새 클라가 올렸다 — 내 쓰기가 422(layout_version)로 거절되면
        // "클라이언트 갱신 필요" 를 말하고, 다시 읽은 문서(판 2)로 읽기 전용이 된다.
        var api = new FakeUnitLayoutApi(layoutVersion: OldVersion);
        var kit = await MapKit.OpenAsync(api, clientLayoutVersion: OldVersion);
        await api.ForClient("B", NewVersion).WriteAsync(api.Current.Version, UnitLayoutChange.LayoutVersionBump());
        api.FailNextWrite(new UnitLayoutWrite.Rejected("배치 판 1 은 서버 판 2 보다 낮습니다", UnitLayoutRejectKind.ClientOutdated));

        kit.Vm.CompleteDrag(new UnitMapDropRequest(kit.Id("6중대"), 40, 0, null, false));
        await kit.Vm.WhenIdleAsync();

        Assert.Equal(UnitMapText.LayoutWriteFailedBar("6중대", UnitMapText.LayoutClientOutdatedReason), kit.Vm.Bar!.Message);
        Assert.Equal(UnitMapLayoutState.VersionMismatch, kit.Vm.LayoutState);
        Assert.Equal(UnitMapText.LayoutStatusClientOutdated, kit.Vm.LayoutStatusText);
    }
    #endregion

    #region - 서버 시나리오 ③ — 올린 뒤 일반 쓰기 -
    [Fact]
    public async Task should_write_normally_with_the_new_layout_version_after_bump()
    {
        var api = ServerWithDeltas(OldVersion, 2);
        var kit = await MapKit.OpenAsync(api, clientLayoutVersion: NewVersion);

        kit.Vm.CompleteDrag(new UnitMapDropRequest(kit.Id("6중대"), 40, 0, null, false));
        await kit.Vm.WhenIdleAsync();

        var last = api.Writes.Last();
        Assert.Equal("main", last.Client);
        Assert.Equal(NewVersion, last.RequestedLayoutVersion);
        Assert.IsType<UnitLayoutWrite.Saved>(last.Result);
        Assert.Equal(new Vector(40, 0), api.Current.DeltaOf(kit.Id("6중대")));
    }
    #endregion
}
