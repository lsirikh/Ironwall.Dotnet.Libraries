using Ironwall.Dotnet.Libraries.Devices.Api.Services;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map;
using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Tests;

/****************************************************************************
   Purpose      : 공유 배치 가짜 서버 — S-1(GET/PATCH /api/units/layout + SYNC_UNIT_LAYOUT)을 메모리로 흉내 낸다
   Created By   : GHLee
   Created On   : 9/28/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com

   Description  : TEST-13(자체 계약) · TEST-14(동기화 · 2클라이언트) · TEST-25 · TEST-26 · 미리보기(IMPL-35)가 공유한다.
                  - 모든 호출을 기록한다(Reads · Writes — If-Match · 변경 · 결과 · 클라이언트 표지).
                  - If-Match 가 현재 버전과 다르면 412(쓰지 않음), 맞으면 적용 + 버전 +1 + 알림.
                  - 알림(Published)은 쓰기 응답을 돌려주기 **전에** 부른다 — 실서버에서 메아리가 응답보다
                    약 5 ms 먼저 온다(probe log V-11).
                  - 다른 운영자 흉내(SimulateOther*) · 다음 쓰기 결과 주입(FailNextWrite*) · 모드 전환(Mode).
                  - ForClient("A") 로 같은 서버를 보는 클라이언트 창구를 여럿 만든다(2클라이언트 시뮬레이션).
****************************************************************************/

/// <summary>가짜 서버가 흉내 내는 판본.</summary>
public enum FakeLayoutServerMode
{
    /// <summary>S-1 이 있는 서버 — 200 · 412 · 알림.</summary>
    Supported,

    /// <summary>S-1 이 없는 서버(현 8.0.x · 422 라우트 가림 / 404) — 읽기 · 쓰기 모두 미지원.</summary>
    Unsupported,

    /// <summary>읽기 · 쓰기가 실패한다(<see cref="FakeUnitLayoutApi.FailureKind"/>).</summary>
    Failing,
}

/// <summary>기록된 쓰기 한 건.</summary>
/// <param name="SnapshotBefore">쓰기 직전 서버 문서.</param>
internal sealed record FakeLayoutWrite(string Client, long IfMatch, UnitLayoutChange Change, UnitLayoutWrite Result, UnitLayoutSnapshot SnapshotBefore)
{
    public bool Succeeded => Result is UnitLayoutWrite.Saved;
    public long VersionBefore => SnapshotBefore.Version;
    public long? VersionAfter => SnapshotAfter?.Version;

    /// <summary>쓰기 직후 서버 문서(성공했을 때만).</summary>
    public UnitLayoutSnapshot? SnapshotAfter => (Result as UnitLayoutWrite.Saved)?.Snapshot;
}

/// <summary>공유 배치 가짜 서버 겸 기본 클라이언트 창구(<c>"main"</c>).</summary>
internal sealed class FakeUnitLayoutApi : IUnitLayoutApi
{
    private readonly object _gate = new();
    private readonly Queue<UnitLayoutWrite> _injectedWrites = new();
    private UnitLayoutSnapshot _current;

    public FakeUnitLayoutApi(int layoutVersion = UnitMapLayout.LayoutVersion, long version = 0)
    {
        _current = UnitLayoutSnapshot.Empty(layoutVersion) with { Version = version };
    }

    #region - 설정 -
    public FakeLayoutServerMode Mode { get; set; } = FakeLayoutServerMode.Supported;

    /// <summary><see cref="FakeLayoutServerMode.Failing"/> 일 때 돌려줄 실패 종류.</summary>
    public UnitLayoutFailureKind FailureKind { get; set; } = UnitLayoutFailureKind.Timeout;

    /// <summary>쓰기가 성공할 때 기록할 변경자 이름(클라이언트 표지 → 이름). 없으면 표지 그대로.</summary>
    public Func<string, string>? ActorName { get; set; }

    /// <summary>
    /// 버전이 바뀔 때마다(쓰기 성공 · 다른 운영자 흉내) 부른다 — <c>SYNC_UNIT_LAYOUT {version}</c> 알림 흉내.
    /// 쓰기 응답보다 <b>먼저</b> 불린다(V-11).
    /// </summary>
    public Action<long>? Published { get; set; }
    #endregion

    #region - 관찰 -
    /// <summary>지금 서버 문서.</summary>
    public UnitLayoutSnapshot Current { get { lock (_gate) return _current; } }

    public int ReadCount { get; private set; }

    public List<FakeLayoutWrite> Writes { get; } = new();

    public int CallCount => ReadCount + Writes.Count;
    #endregion

    #region - 조작(시험이 부른다) -
    /// <summary>다음 쓰기 한 번을 버전과 무관하게 412 로 돌려준다(쓰지 않는다).</summary>
    public void FailNextWriteWithConflict() => FailNextWrite(new UnitLayoutWrite.Conflict(Current.Version));

    /// <summary>다음 쓰기 한 번의 결과를 정해 둔다(적용하지 않는다).</summary>
    public void FailNextWrite(UnitLayoutWrite result)
    {
        lock (_gate) _injectedWrites.Enqueue(result);
    }

    /// <summary>다른 운영자가 그 부대를 옮겼다 — 적용 · 버전 +1 · 알림.</summary>
    public long SimulateOtherWrite(int unitId, double dx, double dy, string by = "다른 운영자")
        => SimulateOther(UnitLayoutChange.SetOne(unitId, new Vector(dx, dy)), by);

    /// <summary>다른 운영자가 그 부대 배치를 초기화했다.</summary>
    public long SimulateOtherClear(int unitId, string by = "다른 운영자")
        => SimulateOther(UnitLayoutChange.ClearOne(unitId), by);

    /// <summary>다른 운영자가 전체 배치를 초기화했다.</summary>
    public long SimulateOtherClearAll(string by = "다른 운영자")
        => SimulateOther(UnitLayoutChange.ClearEverything(), by);

    /// <summary>서버가 버전만 올렸다(예: 상위 변경으로 배치 행 삭제 · 부대 삭제 CASCADE — S-1 ⑦).</summary>
    public long SimulateOther(UnitLayoutChange change, string by = "다른 운영자")
    {
        long version;
        lock (_gate)
        {
            _current = Bump(change.ApplyTo(_current), by);
            version = _current.Version;
        }
        Published?.Invoke(version);
        return version;
    }

    /// <summary>같은 서버를 보는 다른 클라이언트 창구(쓰기 기록에 <paramref name="client"/> 표지가 남는다).</summary>
    public IUnitLayoutApi ForClient(string client) => new ClientView(this, client);
    #endregion

    #region - IUnitLayoutApi -
    public Task<UnitLayoutRead> ReadAsync(CancellationToken token = default) => ReadCore();

    public Task<UnitLayoutWrite> WriteAsync(long ifMatchVersion, UnitLayoutChange change, CancellationToken token = default)
        => WriteCore("main", ifMatchVersion, change);
    #endregion

    #region - 서버 흉내 -
    private Task<UnitLayoutRead> ReadCore()
    {
        UnitLayoutRead result;
        lock (_gate)
        {
            ReadCount++;
            result = Mode switch
            {
                FakeLayoutServerMode.Unsupported => new UnitLayoutRead.Unsupported("이 서버는 배치 저장을 지원하지 않습니다."),
                FakeLayoutServerMode.Failing => new UnitLayoutRead.Failed(FailureKind, "가짜 서버 실패"),
                _ => new UnitLayoutRead.Supported(_current),
            };
        }
        return Task.FromResult(result);
    }

    private Task<UnitLayoutWrite> WriteCore(string client, long ifMatch, UnitLayoutChange change)
    {
        ArgumentNullException.ThrowIfNull(change);
        UnitLayoutWrite result;
        long? published = null;
        lock (_gate)
        {
            var before = _current;
            if (_injectedWrites.Count > 0)
            {
                result = _injectedWrites.Dequeue();
            }
            else if (Mode == FakeLayoutServerMode.Unsupported)
            {
                result = new UnitLayoutWrite.Unsupported("이 서버는 배치 저장을 지원하지 않습니다.");
            }
            else if (Mode == FakeLayoutServerMode.Failing)
            {
                result = new UnitLayoutWrite.Failed(FailureKind, "가짜 서버 실패");
            }
            else if (ifMatch != _current.Version)
            {
                result = new UnitLayoutWrite.Conflict(_current.Version);
            }
            else
            {
                _current = Bump(change.ApplyTo(_current), ActorName?.Invoke(client) ?? client);
                result = new UnitLayoutWrite.Saved(_current);
                published = _current.Version;
            }
            Writes.Add(new FakeLayoutWrite(client, ifMatch, change, result, before));
        }
        if (published is long v) Published?.Invoke(v);   // 메아리가 응답보다 먼저(V-11)
        return Task.FromResult(result);
    }

    private static UnitLayoutSnapshot Bump(UnitLayoutSnapshot snapshot, string by)
        => snapshot with
        {
            Version = snapshot.Version + 1,
            UpdatedByName = by,
            UpdatedAt = new DateTimeOffset(2026, 9, 28, 9, 0, 0, TimeSpan.FromHours(9)).AddMinutes(snapshot.Version + 1),
        };

    private sealed class ClientView : IUnitLayoutApi
    {
        private readonly FakeUnitLayoutApi _server;
        private readonly string _client;

        public ClientView(FakeUnitLayoutApi server, string client)
        {
            _server = server;
            _client = client;
        }

        public Task<UnitLayoutRead> ReadAsync(CancellationToken token = default) => _server.ReadCore();

        public Task<UnitLayoutWrite> WriteAsync(long ifMatchVersion, UnitLayoutChange change, CancellationToken token = default)
            => _server.WriteCore(_client, ifMatchVersion, change);
    }
    #endregion
}
