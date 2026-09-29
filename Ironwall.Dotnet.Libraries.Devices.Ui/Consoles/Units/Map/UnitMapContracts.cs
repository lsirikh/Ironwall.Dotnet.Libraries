using Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map;

/****************************************************************************
   Purpose      : 부대 관계도 — 캔버스 ↔ 관계도 뷰모델 ↔ 부대 콘솔 사이의 계약 (SETUP-04 · FR-02 · FR-29 · FR-32)
   Created By   : GHLee
   Created On   : 9/28/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

// ─────────────────────────────────────────────────────────────────────────────
//  세 레인이 서로의 구현을 기다리지 않게 하는 형식만 둔다 — 동작은 없다.
//
//    부대 콘솔 VM ──IUnitMapCommands──▶ (관계도 VM 이 부른다)        레인 A 가 콘솔에서 구현
//    관계도 VM   ──IUnitMapSurface───▶ (캔버스가 구현, VM 이 부른다)  레인 C
//    캔버스      ──IUnitMapInteraction▶ (관계도 VM 이 구현, 캔버스가 부른다) 레인 A
//    관계도 VM   ──UnitMapScene──────▶ (캔버스의 Scene 속성으로 흘러간다)
//
//  관계도 VM 은 캔버스(컨트롤)를 모르고 IUnitMapSurface 만 안다. 캔버스는 VM 을 모르고
//  IUnitMapInteraction 만 안다. 둘 다 UI 스레드 전용이다(NFR-12).
// ─────────────────────────────────────────────────────────────────────────────

#region - 단계 · 판정 · 요청 -
/// <summary>의미 줌 단계(FR-19). 경계 판정은 <c>UnitMapLod.Resolve(scale, current)</c>(레인 A).</summary>
public enum UnitMapLevel
{
    /// <summary>개요 — 배율 &lt; 0.40(내려올 때 &lt; 0.36). 작은 틀만, 글자 없음, 끌기 없음.</summary>
    L0 = 0,

    /// <summary>부대 — 0.40 ≤ 배율 &lt; 0.80. 틀 30×20 + 표지 + 짧은 이름.</summary>
    L1 = 1,

    /// <summary>상세 — 배율 ≥ 0.80(내려올 때 ≥ 0.72). 카드 132×56.</summary>
    L2 = 2,
}

/// <summary>드롭 한 번의 뜻(FR-29).</summary>
public enum UnitMapDropKind
{
    /// <summary>빈 곳 · <c>Ctrl</c> — 공유 배치의 Δ 만 바뀐다(편제 불변).</summary>
    Position = 0,

    /// <summary>상위 제대 위 — 확인 → <see cref="IUnitMapCommands.MoveAsync"/> 1회.</summary>
    Reparent = 1,

    /// <summary>같은 제대 위 — 확인 → <see cref="IUnitMapCommands.ChangeAdjacencyAsync"/> 1회(양방향).</summary>
    Adjoin = 2,

    /// <summary>놓을 수 없다 — <see cref="UnitMapDropDecision.Reason"/> 가 화면에 그대로 뜬다.</summary>
    Blocked = 3,
}

/// <summary>
/// 드롭 판정 결과(<c>UnitMapDropClassifier.Classify</c> 가 만든다 — 레인 A).
/// </summary>
/// <param name="Kind">뜻.</param>
/// <param name="TargetId">상위 바꾸기 · 인접이면 포인터 아래 부대 id. 위치 · 막힘이면 <c>null</c> 일 수 있다.</param>
/// <param name="Reason">막힘 사유 — <see cref="UnitMapDropKind.Blocked"/> 일 때만 채운다.</param>
public sealed record UnitMapDropDecision(UnitMapDropKind Kind, int? TargetId = null, string? Reason = null)
{
    public static UnitMapDropDecision Position() => new(UnitMapDropKind.Position);
    public static UnitMapDropDecision Reparent(int targetId) => new(UnitMapDropKind.Reparent, targetId);
    public static UnitMapDropDecision Adjoin(int targetId) => new(UnitMapDropKind.Adjoin, targetId);
    public static UnitMapDropDecision Blocked(string reason, int? targetId = null) => new(UnitMapDropKind.Blocked, targetId, reason);

    public bool IsAllowed => Kind != UnitMapDropKind.Blocked;
}

/// <summary>
/// 캔버스가 끌기를 <b>놓았을 때</b> 뷰모델로 올리는 요청 하나.
/// </summary>
/// <param name="UnitId">끈 부대(예하는 Δ 전파로 함께 움직인다 — 항목은 이 하나).</param>
/// <param name="WorldDx">놓은 자리까지의 이동량(월드 단위 = 화면 Δ ÷ 배율). 포인터는 캔버스 <b>루트</b> 기준으로 잰다(FR-28 · VER-01).</param>
/// <param name="WorldDy">위와 같다(세로).</param>
/// <param name="HoverUnitId">놓은 순간 <b>포인터 아래</b> 부대(끌리는 사본 위치가 아니다 — FR-29). 끈 부대와 그 예하는 제외. 빈 곳이면 <c>null</c>.</param>
/// <param name="Ctrl">놓는 순간 <c>Ctrl</c> 을 누르고 있었다 — 판정과 무관하게 위치.</param>
public sealed record UnitMapDropRequest(int UnitId, double WorldDx, double WorldDy, int? HoverUnitId, bool Ctrl);

/// <summary>끄는 동안 노드 하나의 대상 표시(FR-30). 노드의 <c>DropState</c> 의존 속성 값.</summary>
public enum UnitMapNodeDropState
{
    /// <summary>끄는 중이 아니다(또는 끌리는 부대 · 그 예하).</summary>
    None = 0,

    /// <summary>상위 후보 — 파선 윤곽(Primary 1.5 · "6 4").</summary>
    ParentCandidate = 1,

    /// <summary>인접 후보 — 같은 파선 + 연결 표지.</summary>
    AdjoinCandidate = 2,

    /// <summary>받을 수 없다 — 사선 해치 + 50%.</summary>
    Blocked = 3,

    /// <summary>포인터가 머문 노드 — 굵은 실선 3 + 칩. 후보 · 막힘 위에 겹친다.</summary>
    Hover = 4,

    /// <summary>끌리는 부대와 그 예하의 원래 자리 — 편제 35% 잔상.</summary>
    Origin = 5,
}

/// <summary>레이어 토글(FR-26). 개인 표시 설정 — 공유 배치가 아니다.</summary>
public sealed record UnitMapLayers(bool Hierarchy = true, bool Adjacency = true, bool DeviceBadges = true)
{
    public static UnitMapLayers All { get; } = new();
}
#endregion

#region - 장면(뷰모델 → 캔버스) -
/// <summary>
/// 노드 하나에 붙는 <b>편제 밖</b> 사실 — 이름 · 코드 · 제대 · 운용 중지는 <see cref="UnitTreeNode"/> 에서 읽는다.
/// </summary>
/// <param name="UnitId">부대 id.</param>
/// <param name="DeviceCount">이 부대에 직접 매인 장비 수(읽은 시점 스냅샷).</param>
/// <param name="ErrorCount">그중 상태가 오류인 장비 수(▲).</param>
/// <param name="IsMine">이 앱이 붙은 부대(★).</param>
/// <param name="IsMoved">공유 배치에 이 부대의 Δ 가 있다(핀).</param>
/// <param name="IsDimmed">제대 칩 강조에서 빠졌다 — 50% 흐림(FR-16, 노드 수는 불변).</param>
public sealed record UnitMapNodeFacts(int UnitId, int DeviceCount = 0, int ErrorCount = 0, bool IsMine = false, bool IsMoved = false, bool IsDimmed = false)
{
    public static UnitMapNodeFacts Default(int unitId) => new(unitId);
}

/// <summary>
/// 캔버스가 그리는 한 장 — 편제 · <b>Δ 가 입혀진</b> 월드 위치 · 노드 사실 · 레이어.
/// </summary>
/// <remarks>
/// <para>뷰모델이 편제를 다시 읽거나 배치가 바뀔 때마다 <b>새 인스턴스로 갈아 끼운다</b>(불변). 캔버스는 id 로
/// 기존 노드 요소를 다시 쓴다 — 200 요소를 매번 새로 만들지 않는다.</para>
/// <para>위치는 <b>월드 단위</b>(배율 100% 의 DIU)이고 자동 배치 + 조상 Δ 합 + 자기 Δ 다(<c>UnitMapLayout</c> — 레인 A).
/// 선택은 장면에 넣지 않는다 — 자주 바뀌므로 캔버스의 별도 속성(<c>SelectedUnitId</c>)으로 흐른다.</para>
/// </remarks>
public sealed record UnitMapScene(
    UnitTreeModel Tree,
    IReadOnlyDictionary<int, Point> Positions,
    IReadOnlyDictionary<int, UnitMapNodeFacts> Facts,
    UnitMapLayers Layers)
{
    public static UnitMapScene Empty { get; } = new(
        UnitTreeModel.Empty,
        new Dictionary<int, Point>(),
        new Dictionary<int, UnitMapNodeFacts>(),
        UnitMapLayers.All);

    /// <summary>그 부대의 사실 — 없으면 기본값(장비 0 · 표지 없음).</summary>
    public UnitMapNodeFacts FactsOf(int unitId)
        => Facts.TryGetValue(unitId, out var facts) ? facts : UnitMapNodeFacts.Default(unitId);

    /// <summary>모든 노드 위치를 감싸는 월드 사각형(노드 크기는 뺀 점들의 경계). 노드가 없으면 <see cref="Rect.Empty"/>.</summary>
    public Rect WorldBounds
    {
        get
        {
            if (Positions.Count == 0) return Rect.Empty;
            var minX = Positions.Values.Min(p => p.X);
            var minY = Positions.Values.Min(p => p.Y);
            var maxX = Positions.Values.Max(p => p.X);
            var maxY = Positions.Values.Max(p => p.Y);
            return new Rect(new Point(minX, minY), new Point(maxX, maxY));
        }
    }
}
#endregion

#region - 키(캔버스가 해석 → 뷰모델이 뜻을 정한다) -
/// <summary>
/// 캔버스가 <c>PreviewKeyDown</c> 에서 해석한 키(FR-36~39). 뜻 · 권한 · 확인은 뷰모델이 정한다.
/// </summary>
/// <remarks><c>Alt+↑/↓</c> 는 <c>e.Key == Key.System &amp;&amp; e.SystemKey == Key.Up/Down</c> 으로만 잡는다(DF).
/// <c>Alt+Shift</c>+화살표는 여기에 없다 — 한국어 Windows 입력 언어 전환 단축키(R-16).
/// 줌(<c>+</c> <c>−</c> <c>0</c>) · <c>Ctrl</c>+화살표 팬은 캔버스가 스스로 처리하므로 여기에 없다.</remarks>
public enum UnitMapKeyCommand
{
    Up,
    Down,
    Left,
    Right,
    Home,
    Enter,
    Escape,
    MoveMode,       // M
    ParentUp,       // Alt+↑
    ParentDown,     // Alt+↓
    Undo,           // Ctrl+Z
    LocateOnMap,    // L
}
#endregion

#region - 인터페이스 -
/// <summary>
/// 부대 콘솔 VM 이 구현하고 관계도 VM 이 부른다 — 편제 쓰기 · 선택은 <b>기존 경로 한 벌</b>(D-5).
/// </summary>
/// <remarks>
/// <para>관계도는 편제를 직접 쓰지 않는다. 트리와 관계도가 같은 규칙 · 재조회 · 권한 · 상태 문구를 쓰게 하려는 것이다.
/// 되돌리기 표는 나눠 쓰지 않는다 — 관계도는 자기 표(<c>UnitMapUndoStack</c>)로 반대 이동을 보내고, 트리의 <c>_lastMove</c> 는 트리 이동만 담는다.
/// 모든 멤버는 UI 스레드에서 부른다.</para>
/// <para>콘솔 VM 에는 이름이 같은 공개 메서드가 이미 있다(<c>MoveAsync(int, int?, …)</c> 등) —
/// 이 인터페이스는 <b>명시적 구현</b>으로 붙이면 기존 시그니처와 부딪히지 않는다.</para>
/// </remarks>
public interface IUnitMapCommands
{
    /// <summary>지금 고른 부대(트리와 같은 선택 — FR-02). 없으면 <c>null</c>.</summary>
    int? SelectedUnitId { get; }

    /// <summary>선택이 바뀌었다(트리에서 · 관계도에서 · 재조회로). UI 스레드에서 발화.</summary>
    event EventHandler? SelectedUnitChanged;

    /// <summary><c>units:view</c> — 관계도를 볼 수 있다.</summary>
    bool CanView { get; }

    /// <summary><c>units:edit</c> — 상위 · 인접 · 공유 배치 쓰기(FR-05).</summary>
    bool CanEdit { get; }

    /// <summary>
    /// 그 부대를 고른다 — <b>네비게이션 가드</b>(<c>Detail.Guard.TryNavigate</c>)를 거친다.
    /// 가드가 거절하면(상세에 손댄 칸이 있고 운영자가 머물기를 택함) <c>false</c> 이고 선택은 그대로다.
    /// </summary>
    bool TrySelect(int unitId);

    /// <summary>
    /// 상위 바꾸기 — 확인이 끝난 뒤 1회. <paramref name="targetId"/> 가 <c>null</c> 이면 최상위로(되돌리기가 옛 상위 없음으로 돌려보낼 때).
    /// 성공이면 <c>true</c>(실패 복구 재조회는 콘솔이 한다). 실패 사유는 <see cref="IUnitMapConsoleBridge.LastWriteFailureReason"/>.
    /// </summary>
    /// <remarks>
    /// 관계도의 되돌리기는 이 메서드로 <b>정확한 반대 이동</b>을 직접 보낸다 — 트리 툴바 [이동 되돌리기]의 공유 표(<c>_lastMove</c>)는
    /// 쓰지도 채우지도 않는다(REVIEW-01 HIGH-1: 그 표는 하나뿐이라 최상위 부대 되돌리기가 엉뚱한 부대를 옮기거나 거짓 성공을 냈다).
    /// </remarks>
    Task<bool> MoveAsync(int movingId, int? targetId, CancellationToken token = default);

    /// <summary>
    /// 인접 추가 · 해제 — <paramref name="unitId"/> 기준(선택을 몰래 바꾸지 않는다 — FR-32).
    /// 보내기 직전 재조회는 콘솔의 기존 경로가 한다.
    /// </summary>
    Task<bool> ChangeAdjacencyAsync(int unitId, int? add, int? remove, CancellationToken token = default);

    /// <summary>편제 · 장비를 다시 읽는다. <paramref name="quiet"/> 면 상태 문구를 흔들지 않는다(실패 복구 · 알림 반영).</summary>
    Task ReloadAsync(bool quiet, CancellationToken token = default);
}

/// <summary>
/// 캔버스(<c>UnitMapCanvas</c>)가 구현하고 관계도 VM 이 부른다 — 뷰를 옮기는 일만 한다.
/// </summary>
/// <remarks>애니메이션 없음 — 전부 즉시(NFR-05 · R-18). 단계를 강제로 고정하는 멤버는 두지 않는다(단계는 배율이 정한다).</remarks>
public interface IUnitMapSurface
{
    /// <summary>지금 배율(0.10 ~ 1.60).</summary>
    double Scale { get; }

    /// <summary>지금 단계.</summary>
    UnitMapLevel Level { get; }

    /// <summary>뷰 가운데의 월드 좌표 — 개인 뷰 저장(FR-15)에 쓴다.</summary>
    Point CenterWorld { get; }

    /// <summary>배율 · 중심이 바뀌었다(줌 · 팬 · 맞춤). 저장은 VM 이 정한다.</summary>
    event EventHandler? ViewChanged;

    /// <summary>그 부대를 뷰 가운데로. <paramref name="scale"/> 이 있으면 그 배율로(범위 고정), 없으면 배율 유지.</summary>
    void CenterOn(int unitId, double? scale = null);

    /// <summary>개인 뷰 복원 — 배율과 가운데 월드 점.</summary>
    void SetView(double scale, Point centerWorld);

    /// <summary>전체 보기 — 여백 16 DIU, 최소 배율에서 멈춘다(FR-15).</summary>
    void Fit();

    /// <summary>그 부대 노드가 지금 뷰 안에 보이는가(화살표 선택 이동 뒤 팬 여부 — FR-36).</summary>
    bool IsInView(int unitId);
}

/// <summary>
/// 관계도 VM 이 구현하고 <b>부대 콘솔 VM 이</b> 편제 재조회(<c>SYNC_UNIT</c> — FR-48) · 배치 재조회(FR-53) 전에 묻는다.
/// </summary>
/// <remarks>
/// <para>끌기 · 팬 제스처는 캔버스가 <c>DragSession.Begin()</c> 토큰으로 이미 알린다(콘솔의 기존 <c>_isDragging</c> 이 본다).
/// 이 문은 <b>끌기가 아닌데 다시 읽으면 안 되는 상태</b> — 확인 오버레이가 떠 있음(상위 · 인접 확정 대기) ·
/// <c>M</c> 위치 이동 모드 — 를 콘솔에 알린다. <c>DragSession</c> 에 싣지 않는 까닭: 그것은 프로세스 전역이라
/// 오래 머무는 확인 오버레이가 <b>다른 콘솔</b>의 다시 읽기까지 막는다.</para>
/// <para>콘솔은 미룬 뒤 <see cref="DefersReloadChanged"/>(→ <c>false</c>) 또는 합침 재시도로 한 번 다시 읽는다. UI 스레드 전용.</para>
/// </remarks>
public interface IUnitMapReloadGate
{
    /// <summary>지금 다시 읽으면 안 된다 — 확인 오버레이 대기 · <c>M</c> 모드 · (캔버스가 알린) 끌기 · 팬 중.</summary>
    bool DefersReload { get; }

    /// <summary><see cref="DefersReload"/> 가 바뀌었다(특히 <c>false</c> 로 — 미룬 재조회를 이제 해도 된다).</summary>
    event EventHandler? DefersReloadChanged;
}

/// <summary>
/// 관계도 VM 이 구현하고 캔버스가 부른다 — 캔버스가 본 입력을 <b>뜻 없이</b> 올린다.
/// </summary>
/// <remarks>
/// 캔버스는 판정 · 서버 · 권한을 모른다. 끌기 중 표시(후보 · 막힘 · 칩)도 <see cref="Classify"/> 의 답을 그대로 그린다.
/// 모든 멤버는 UI 스레드에서 불린다.
/// </remarks>
public interface IUnitMapInteraction
{
    /// <summary>캔버스가 붙거나(자기 자신) 떨어질 때(<c>null</c>) 부른다. VM 은 이것으로만 뷰를 옮긴다.</summary>
    void AttachSurface(IUnitMapSurface? surface);

    /// <summary>노드 클릭(데드존 미만) · UIA <c>SelectionItem.Select()</c> — 좌표 없는 선택 요청.</summary>
    void RequestSelect(int unitId);

    /// <summary>빈 곳 클릭(데드존 미만) — 선택 해제 요청.</summary>
    void RequestClearSelection();

    /// <summary>
    /// 끌리는 <paramref name="movingUnitId"/> 를 <paramref name="hoverUnitId"/>(빈 곳이면 <c>null</c>) 위에 놓으면 무엇이 되는가.
    /// 끌기 시작 때 모든 노드에 1회 · 머문 노드가 바뀔 때 1회 부른다 — 서버를 부르지 않는 순수 판정이어야 한다.
    /// </summary>
    UnitMapDropDecision Classify(int movingUnitId, int? hoverUnitId, bool ctrl);

    /// <summary>끌기가 데드존을 넘었다(FR-53 — 알림 반영을 미룬다).</summary>
    void BeginDrag(int unitId);

    /// <summary>놓았다 — 위치면 먼저 그리고 PATCH, 상위 · 인접이면 확인 오버레이.</summary>
    void CompleteDrag(UnitMapDropRequest request);

    /// <summary><c>Esc</c> · 캡처 상실 · 창 비활성화 — 원위치 · 서버 0(FR-33).</summary>
    void CancelDrag(int unitId);

    /// <summary>해석된 키 하나. 처리했으면 <c>true</c>(캔버스가 <c>e.Handled</c> 를 세운다).</summary>
    bool HandleKey(UnitMapKeyCommand command, bool shift);
}
#endregion
