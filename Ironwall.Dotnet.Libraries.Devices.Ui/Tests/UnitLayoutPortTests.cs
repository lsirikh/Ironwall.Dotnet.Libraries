using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map;
using Ironwall.Dotnet.Libraries.Messages.Dto.Units;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/****************************************************************************
   Purpose      : TEST-13 — 공유 배치 좁은 포트 · 서비스 어댑터 · 세션 전용 저장소 · 가짜 서버 자체 계약
   Created By   : GHLee
   Created On   : 9/28/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com

   Description  : 시나리오 SIM-P001 · P009~P014 · P043 · P044 · P047 · F056 · F059 · F078 · F089 · Q001(412 기록).
****************************************************************************/
public class UnitLayoutPortTests
{
    #region - 변경 모델 (UnitLayoutChange) -
    [Fact]
    public void should_apply_clear_all_then_clear_then_set_when_change_has_all_three()
    {
        // Arrange
        var before = Snapshot(5, (1, 10, 10), (2, 20, 20), (3, 30, 30));
        var change = new UnitLayoutChange(
            new Dictionary<int, Vector> { [2] = new Vector(-5, 7), [4] = new Vector(1, 1) },
            new[] { 2, 3 },
            ClearAll: true);

        // Act
        var after = change.ApplyTo(before);

        // Assert — clear_all 이 먼저라 1 · 3 은 사라지고, clear(2) 뒤 set(2) 가 이긴다.
        Assert.Equal(new[] { 2, 4 }, after.Deltas.Keys.OrderBy(k => k));
        Assert.Equal(new Vector(-5, 7), after.Deltas[2]);
        Assert.Equal(5, after.Version);          // 버전 · 변경자는 서버 몫 — 적용은 건드리지 않는다
    }

    [Fact]
    public void should_hold_exactly_one_set_item_when_change_is_a_single_move()
    {
        var change = UnitLayoutChange.SetOne(27, new Vector(-51.1, 64.4));

        Assert.Equal(new[] { 27 }, change.Set.Keys);
        Assert.Empty(change.Clear);
        Assert.False(change.ClearAll);
        Assert.Equal(new[] { 27 }, change.UnitIds);
    }

    [Fact]
    public void should_list_cleared_and_set_units_without_clear_all_when_asked_for_unit_ids()
    {
        Assert.Equal(new[] { 31 }, UnitLayoutChange.ClearOne(31).UnitIds);
        Assert.Empty(UnitLayoutChange.ClearEverything().UnitIds);
        Assert.True(UnitLayoutChange.ClearEverything().ClearAll);
    }
    #endregion

    #region - 어댑터 (서비스 결과 → 포트 모델) -
    [Fact]
    public async Task should_map_supported_document_to_snapshot_when_service_reads_200()
    {
        var service = new StubLayoutService
        {
            Read = new UnitLayoutReadResult.Supported(Doc(13, 1, "김○○", "2026-09-27T14:02:11.120000+09:00", (27, -51.1, 64.4)), 13),
        };

        var read = await new UnitLayoutApiAdapter(service).ReadAsync();

        var supported = Assert.IsType<UnitLayoutRead.Supported>(read);
        Assert.Equal(13, supported.Snapshot.Version);
        Assert.Equal(1, supported.Snapshot.LayoutVersion);
        Assert.Equal("김○○", supported.Snapshot.UpdatedByName);
        Assert.Equal(new DateTimeOffset(2026, 9, 27, 14, 2, 11, 120, TimeSpan.FromHours(9)), supported.Snapshot.UpdatedAt);
        Assert.Equal(new Vector(-51.1, 64.4), supported.Snapshot.DeltaOf(27));
        Assert.Null(supported.Snapshot.DeltaOf(28));
    }

    [Fact]
    public async Task should_keep_last_item_when_document_repeats_a_unit()
    {
        var service = new StubLayoutService
        {
            Read = new UnitLayoutReadResult.Supported(Doc(2, 1, null, null, (5, 1, 1), (5, 2, 2)), null),
        };

        var read = await new UnitLayoutApiAdapter(service).ReadAsync();

        Assert.Equal(new Vector(2, 2), Assert.IsType<UnitLayoutRead.Supported>(read).Snapshot.Deltas[5]);
    }

    [Fact]
    public async Task should_map_unsupported_and_failed_when_service_says_so()
    {
        var unsupported = await new UnitLayoutApiAdapter(new StubLayoutService { Read = new UnitLayoutReadResult.Unsupported(422, "가림") }).ReadAsync();
        var failed = await new UnitLayoutApiAdapter(new StubLayoutService { Read = new UnitLayoutReadResult.ReadFailed(UnitLayoutFailureKind.Timeout, 504, "t") }).ReadAsync();

        Assert.IsType<UnitLayoutRead.Unsupported>(unsupported);
        Assert.Equal(UnitLayoutFailureKind.Timeout, Assert.IsType<UnitLayoutRead.Failed>(failed).Kind);
    }

    [Fact]
    public async Task should_report_unsupported_without_service_when_not_registered()
    {
        var adapter = new UnitLayoutApiAdapter(null);

        Assert.IsType<UnitLayoutRead.Unsupported>(await adapter.ReadAsync());
        Assert.IsType<UnitLayoutWrite.Unsupported>(await adapter.WriteAsync(3, UnitLayoutChange.SetOne(1, new Vector(1, 1))));
    }

    [Fact]
    public async Task should_build_patch_with_client_layout_version_and_sorted_items_when_writing()
    {
        var service = new StubLayoutService { Write = new UnitLayoutWriteResult.Ok(Doc(14, 1, "나", null), 14) };
        var change = new UnitLayoutChange(
            new Dictionary<int, Vector> { [9] = new Vector(3, 4), [2] = new Vector(-1, 0) },
            new[] { 31, 7 },
            ClearAll: false);

        var write = await new UnitLayoutApiAdapter(service).WriteAsync(13, change);

        Assert.Equal(13, service.LastIfMatch);
        var patch = service.LastPatch!;
        Assert.Equal(1, patch.LayoutVersion);
        Assert.Equal(new[] { 2, 9 }, patch.Set.Select(i => i.UnitId));
        Assert.Equal((-1.0, 0.0), (patch.Set[0].Dx, patch.Set[0].Dy));
        Assert.Equal(new[] { 7, 31 }, patch.Clear);
        Assert.False(patch.ClearAll);
        Assert.Equal(14, Assert.IsType<UnitLayoutWrite.Saved>(write).Snapshot.Version);
    }

    [Fact]
    public async Task should_send_clear_all_when_resetting_everything()
    {
        var service = new StubLayoutService { Write = new UnitLayoutWriteResult.Ok(Doc(14, 1, null, null), null) };

        await new UnitLayoutApiAdapter(service).WriteAsync(13, UnitLayoutChange.ClearEverything());

        Assert.True(service.LastPatch!.ClearAll);
        Assert.Empty(service.LastPatch.Set);
        Assert.Empty(service.LastPatch.Clear);
    }

    [Fact]
    public async Task should_map_every_write_outcome_when_service_answers()
    {
        async Task<UnitLayoutWrite> Run(UnitLayoutWriteResult r)
            => await new UnitLayoutApiAdapter(new StubLayoutService { Write = r }).WriteAsync(1, UnitLayoutChange.ClearOne(1));

        Assert.Equal(15, Assert.IsType<UnitLayoutWrite.Conflict>(await Run(new UnitLayoutWriteResult.Conflict(15))).CurrentVersion);
        Assert.IsType<UnitLayoutWrite.Rejected>(await Run(new UnitLayoutWriteResult.Rejected("x")));
        Assert.IsType<UnitLayoutWrite.Unsupported>(await Run(new UnitLayoutWriteResult.Unsupported(404, "x")));
        Assert.Equal(UnitLayoutFailureKind.Server,
            Assert.IsType<UnitLayoutWrite.Failed>(await Run(new UnitLayoutWriteResult.Failed(UnitLayoutFailureKind.Server, 500, "x"))).Kind);
    }

    [Fact]
    public async Task should_carry_the_new_write_outcomes_to_the_port_as_sub_cases()
    {
        // TEST-65 — 새 갈래가 포트까지 닿는다. 하위 갈래라 옛 소비자(case Unsupported / Failed)도 그대로 받는다.
        async Task<UnitLayoutWrite> Run(UnitLayoutWriteResult r)
            => await new UnitLayoutApiAdapter(new StubLayoutService { Write = r }).WriteAsync(1, UnitLayoutChange.ClearOne(1));

        var gone = await Run(new UnitLayoutWriteResult.EndpointGone(404, "x"));
        var precondition = await Run(new UnitLayoutWriteResult.PreconditionRequired(428, "x"));
        var unknown = await Run(new UnitLayoutWriteResult.Unknown(504, "x"));

        Assert.IsType<UnitLayoutWrite.EndpointGone>(gone);
        Assert.IsAssignableFrom<UnitLayoutWrite.Unsupported>(gone);
        Assert.Equal(UnitLayoutFailureKind.PreconditionRequired, Assert.IsType<UnitLayoutWrite.PreconditionRequired>(precondition).Kind);
        Assert.Equal(UnitLayoutFailureKind.Timeout, Assert.IsType<UnitLayoutWrite.Unknown>(unknown).Kind);
        Assert.IsAssignableFrom<UnitLayoutWrite.Failed>(unknown);
    }
    #endregion

    #region - 세션 전용 저장소 (FR-51 · NFR-14) -
    [Fact]
    public void should_apply_set_clear_and_clear_all_in_memory_without_versioning_when_session_only()
    {
        var store = new SessionOnlyUnitLayoutStore();

        store.Apply(UnitLayoutChange.SetOne(1, new Vector(10, 0)));
        store.Apply(UnitLayoutChange.SetOne(2, new Vector(0, 5)));
        store.Apply(UnitLayoutChange.ClearOne(1));

        Assert.Equal(new[] { 2 }, store.Snapshot.Deltas.Keys);
        Assert.Equal(0, store.Snapshot.Version);          // 버전을 흉내 내지 않는다
        Assert.Null(store.Snapshot.UpdatedByName);

        store.Apply(UnitLayoutChange.ClearEverything());
        Assert.Empty(store.Snapshot.Deltas);
        Assert.Equal(0, store.Snapshot.Version);
    }

    [Fact]
    public async Task should_make_no_port_call_after_the_probe_when_session_only()
    {
        // 창을 열 때의 읽기 1회가 미지원이면, 그 뒤의 위치 조작은 세션 저장소만 쓴다 — 서버 호출 0(SIM-P047).
        var server = new FakeUnitLayoutApi { Mode = FakeLayoutServerMode.Unsupported };
        var probe = await server.ReadAsync();
        Assert.IsType<UnitLayoutRead.Unsupported>(probe);

        var store = new SessionOnlyUnitLayoutStore();
        store.Apply(UnitLayoutChange.SetOne(7, new Vector(20, 0)));
        store.Apply(UnitLayoutChange.ClearEverything());

        Assert.Equal(1, server.CallCount);
        Assert.Empty(server.Writes);
    }

    [Fact]
    public void should_not_reference_file_system_apis_when_scanning_port_source()
    {
        // NFR-14 — 공유 배치를 디스크에 남기지 않는다(캐시 · 임시 · 대체 파일 모두). 코드 검사 절반.
        var source = File.ReadAllText(PortSourcePath());
        var code = Regex.Replace(source, @"//.*?$|/\*.*?\*/", string.Empty, RegexOptions.Multiline | RegexOptions.Singleline);

        Assert.DoesNotMatch(@"\bFile\.|\bFileStream\b|\bIsolatedStorage|\bDirectory\.|\bStreamWriter\b|\bFileInfo\b", code);
    }
    #endregion

    #region - 가짜 서버 자체 계약 (TEST-14 · 25 · 26 · 미리보기가 기댄다) -
    [Fact]
    public async Task should_apply_bump_version_and_publish_before_answering_when_if_match_is_current()
    {
        var server = new FakeUnitLayoutApi(version: 13);
        var published = new List<long>();
        var answered = false;
        server.Published = v => { Assert.False(answered); published.Add(v); };

        var write = await server.WriteAsync(13, UnitLayoutChange.SetOne(27, new Vector(1, 2)));
        answered = true;

        var saved = Assert.IsType<UnitLayoutWrite.Saved>(write);
        Assert.Equal(14, saved.Snapshot.Version);
        Assert.Equal(new[] { 14L }, published);
        Assert.Equal(new Vector(1, 2), server.Current.DeltaOf(27));
        var record = Assert.Single(server.Writes);
        Assert.Equal(("main", 13L, 13L), (record.Client, record.IfMatch, record.VersionBefore));
    }

    [Fact]
    public async Task should_answer_412_and_not_write_when_if_match_is_stale()
    {
        var server = new FakeUnitLayoutApi(version: 15);

        var write = await server.WriteAsync(13, UnitLayoutChange.SetOne(27, new Vector(1, 2)));

        Assert.Equal(15, Assert.IsType<UnitLayoutWrite.Conflict>(write).CurrentVersion);
        Assert.Equal(15, server.Current.Version);
        Assert.Null(server.Current.DeltaOf(27));
    }

    [Fact]
    public async Task should_answer_injected_conflict_once_when_asked()
    {
        var server = new FakeUnitLayoutApi(version: 3);
        server.FailNextWriteWithConflict();

        var first = await server.WriteAsync(3, UnitLayoutChange.SetOne(1, new Vector(1, 1)));
        var second = await server.WriteAsync(3, UnitLayoutChange.SetOne(1, new Vector(1, 1)));

        Assert.IsType<UnitLayoutWrite.Conflict>(first);
        Assert.IsType<UnitLayoutWrite.Saved>(second);
    }

    [Fact]
    public async Task should_bump_version_and_publish_when_another_operator_writes()
    {
        var server = new FakeUnitLayoutApi(version: 13);
        var published = new List<long>();
        server.Published = published.Add;

        server.SimulateOtherWrite(6, 30, -12, by: "박○○");
        var read = Assert.IsType<UnitLayoutRead.Supported>(await server.ReadAsync());

        Assert.Equal(14, read.Snapshot.Version);
        Assert.Equal("박○○", read.Snapshot.UpdatedByName);
        Assert.Equal(new Vector(30, -12), read.Snapshot.DeltaOf(6));
        Assert.Equal(new[] { 14L }, published);
        Assert.Empty(server.Writes);                       // 다른 운영자 흉내는 이 클라이언트의 쓰기가 아니다
    }

    [Fact]
    public async Task should_tag_writes_by_client_when_two_clients_share_the_server()
    {
        var server = new FakeUnitLayoutApi();
        var a = server.ForClient("A");
        var b = server.ForClient("B");

        await a.WriteAsync(0, UnitLayoutChange.SetOne(1, new Vector(1, 0)));
        var late = await b.WriteAsync(0, UnitLayoutChange.SetOne(2, new Vector(0, 1)));

        Assert.Equal(new[] { "A", "B" }, server.Writes.Select(w => w.Client));
        Assert.IsType<UnitLayoutWrite.Conflict>(late);
    }

    [Theory]
    [InlineData(FakeLayoutServerMode.Unsupported)]
    [InlineData(FakeLayoutServerMode.Failing)]
    public async Task should_refuse_reads_and_writes_when_server_mode_is_not_supported(FakeLayoutServerMode mode)
    {
        var server = new FakeUnitLayoutApi { Mode = mode };

        var read = await server.ReadAsync();
        var write = await server.WriteAsync(0, UnitLayoutChange.SetOne(1, new Vector(1, 1)));

        Assert.IsNotType<UnitLayoutRead.Supported>(read);
        Assert.IsNotType<UnitLayoutWrite.Saved>(write);
        Assert.Empty(server.Current.Deltas);
    }
    #endregion

    #region - Helpers -
    private static UnitLayoutSnapshot Snapshot(long version, params (int Id, double Dx, double Dy)[] items)
        => new(version, 1, null, null, items.ToDictionary(i => i.Id, i => new Vector(i.Dx, i.Dy)));

    private static UnitLayoutDocumentDto Doc(long version, int layoutVersion, string? by, string? at, params (int Id, double Dx, double Dy)[] items) => new()
    {
        Version = version,
        LayoutVersion = layoutVersion,
        UpdatedAt = at,
        UpdatedBy = by is null ? null : new UnitLayoutActorDto { Id = 7, Name = by },
        Items = items.Select(i => new UnitLayoutItemDto { UnitId = i.Id, Dx = i.Dx, Dy = i.Dy }).ToList(),
    };

    private static string PortSourcePath([CallerFilePath] string here = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "Consoles", "Units", "Map", "UnitLayoutPort.cs"));

    private sealed class StubLayoutService : IUnitLayoutApiService
    {
        public UnitLayoutReadResult Read { get; set; } = new UnitLayoutReadResult.Unsupported(404, "없음");
        public UnitLayoutWriteResult Write { get; set; } = new UnitLayoutWriteResult.Unsupported(404, "없음");
        public long? LastIfMatch { get; private set; }
        public UnitLayoutPatchDto? LastPatch { get; private set; }

        public Task<UnitLayoutReadResult> GetAsync(CancellationToken token = default) => Task.FromResult(Read);

        public Task<UnitLayoutWriteResult> PatchAsync(long ifMatchVersion, UnitLayoutPatchDto patch, CancellationToken token = default)
        {
            LastIfMatch = ifMatchVersion;
            LastPatch = patch;
            return Task.FromResult(Write);
        }
    }
    #endregion
}
