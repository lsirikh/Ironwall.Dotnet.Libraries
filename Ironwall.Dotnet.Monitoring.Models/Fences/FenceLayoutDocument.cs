using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Ironwall.Dotnet.Monitoring.Models.Fences;

/// <summary>
/// 제어기 한 대의 펜스 구성 — GIS <b>로컬</b>에 저장하는 한 벌(fence-wiring-editor FR-11 · FR-16): 망 목록 · 센서 설치 자리 · 번호 대역 · 간격.
/// 번호 · 순서 · 방향의 정본은 서버(<c>number_device</c> · <c>spec.wiring</c>)이고, 이 문서는 그 위에 <b>모양</b>만 얹는다.
/// 지도(PIDS 3D 펜스)가 나중에 같은 문서를 읽을 수 있게 모델 계층에 둔다.
/// </summary>
public sealed class FenceLayoutDocument
{
    /// <summary>
    /// 본문 형식 판. 읽는 쪽은 모르는 판을 버리지 않고 아는 칸만 읽는다.
    /// 2 = 줄(레인 · 자리마다 <c>lane</c>) · 제어기 위치(<c>controller_end</c>) · VBus 틈(<c>vbus_gap</c>) — 1 은 읽을 때 모두 아래 줄 · 왼쪽 · VBus 기본으로 옮긴다.
    /// </summary>
    public const int SCHEMA = 2;

    [JsonProperty("schema")]
    public int Schema { get; init; } = SCHEMA;

    [JsonProperty("controller_id")]
    public int ControllerId { get; init; }

    /// <summary>망 목록 — Ch1(A) 쪽 끝이 첫 칸.</summary>
    [JsonProperty("panels")]
    public IReadOnlyList<FencePanelSpec> Panels { get; init; } = Array.Empty<FencePanelSpec>();

    /// <summary>센서 서버 id → 설치 자리. 서버에 아직 없는 센서(새 줄)는 싣지 않는다.</summary>
    [JsonProperty("mounts")]
    public IReadOnlyDictionary<int, SensorMountSpec> Mounts { get; init; } = new Dictionary<int, SensorMountSpec>();

    /// <summary>번호 대역 — 고르지 않았으면 <c>null</c>(번호를 자동으로 매기지 않는다).</summary>
    [JsonProperty("bands", NullValueHandling = NullValueHandling.Ignore)]
    public NumberBandSet? Bands { get; init; }

    /// <summary>펜스센서 현장 간격(m) — 새 센서의 기본 칸 폭 · 제안에 쓴다. 없으면 기준(3m).</summary>
    [JsonProperty("fence_spacing_m", NullValueHandling = NullValueHandling.Ignore)]
    public double? FenceSpacingM { get; init; }

    /// <summary>제어기(<c>C</c>)가 펜스 어느 끝에 있는가(FR-19) — 사슬 방향 · 번호 방향을 정한다.</summary>
    [JsonProperty("controller_end")]
    public FenceControllerEnd ControllerEnd { get; init; } = FenceControllerEnd.Left;

    /// <summary>
    /// VBus 표지 자리(FR-21 · 스마트 복합센서2 링만) — 사슬 틈 번호(0…N, k = k번째 센서 뒤). 없으면 기본(가운데 두 센서 사이).
    /// </summary>
    [JsonProperty("vbus_gap", NullValueHandling = NullValueHandling.Ignore)]
    public int? VbusGap { get; init; }

    /// <summary>저장소의 행 판(동시 저장 충돌 판정) — 본문에는 싣지 않는다. 처음 저장이면 0.</summary>
    [JsonIgnore]
    public int Revision { get; init; }

    /// <summary>저장소가 마지막으로 고친 시각(알림용).</summary>
    [JsonIgnore]
    public DateTime? UpdatedAt { get; init; }
}
