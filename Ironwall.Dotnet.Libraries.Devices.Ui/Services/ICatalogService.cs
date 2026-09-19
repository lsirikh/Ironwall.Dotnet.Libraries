using Ironwall.Dotnet.Libraries.Enums;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Services;

/// <summary>카탈로그 어휘 한 행 — 저장·전송되는 <see cref="Code"/> 와 화면용 <see cref="Label"/>.</summary>
/// <param name="Code">서버에 보내는 값(원문 그대로).</param>
/// <param name="Label">표시명. 서버가 비워 보냈으면 <paramref name="Code"/> 와 같다.</param>
/// <param name="IsDeprecated"><c>deprecated_at</c> 이 찍힌 어휘 — 새로 고를 수 없지만 이미 쓰는 장비의 표시는 해야 한다.</param>
public sealed record CatalogOption(string Code, string Label, bool IsDeprecated = false)
{
    /// <summary>라벨이 코드와 같으면 코드를 따로 보일 필요가 없다(목업 규칙: "라벨+코드 병기, 같으면 코드 숨김").</summary>
    public bool HasDistinctLabel => !string.Equals(Code, Label, StringComparison.Ordinal);

    public override string ToString() => HasDistinctLabel ? $"{Label} ({Code})" : Code;
}

/// <summary>카테고리의 종류축(<c>type_&lt;category&gt;</c>) 규칙.</summary>
/// <param name="Field">요청·응답 키 이름(<c>type_camera</c> 등).</param>
/// <param name="RequiredOnCreate">생성 시 필수인가 — 제어기·센서·카메라는 필수, 형상 4축(스피커·함체·경광등·통문)은 생략 시 서버가 <paramref name="DefaultCode"/> 를 배정한다.</param>
/// <param name="UnknownCode">"현장 미확인"을 뜻하는 값(형상 4축의 <c>Unknown</c>). 없으면 <c>null</c>.</param>
/// <param name="DefaultCode">생략 시 서버 기본값.</param>
public sealed record CatalogTypeAxis(string Field, bool RequiredOnCreate, string? UnknownCode, string? DefaultCode, IReadOnlyList<CatalogOption> Values);

/// <summary>종류축 밖의 추가 축 — 현재는 스피커의 <c>speaker_role</c> 하나.</summary>
public sealed record CatalogExtraAxis(string Field, string Label, string? DefaultCode, IReadOnlyList<CatalogOption> Values);

/// <summary>
/// 장비 어휘 카탈로그(<c>GET /api/devices/spec</c>) 캐시 — 종류축 콤보·부품 어휘·접속 방식의 <b>단일 원천</b>.
/// </summary>
/// <remarks>
/// <para>어휘를 코드 상수로 두지 않는다(device-console-v8 AD-10). 서버 어휘는 클라 enum 보다 앞서 가고
/// (<c>SPEED_DOME</c>·<c>SmartController</c>), enum 을 거치면 모델이 <c>NONE</c> 으로 떨어져 저장 왕복에서 종류가 바뀐다.</para>
/// <para>세션당 <b>1회</b> 읽고, <c>SYNC_CATALOG</c> 통지가 오면 <b>판 번호를 비교하지 않고</b> 전량 다시 읽는다.
/// 6.3 서버에는 이 경로가 없다 — 부르지 않고 빈 카탈로그로 남으며 화면은 옛 enum 콤보를 그대로 쓴다.</para>
/// </remarks>
public interface ICatalogService
{
    /// <summary>카탈로그를 한 번이라도 성공적으로 읽었는가.</summary>
    bool IsLoaded { get; }

    /// <summary>카탈로그가 새로 적재될 때마다(최초·갱신) 발화. 구독자는 콤보 목록을 다시 만든다.</summary>
    event EventHandler? CatalogChanged;

    /// <summary>아직 없으면 읽는다. 동시에 여러 곳에서 불러도 요청은 한 번만 나간다. 6.3 이면 부르지 않고 <c>false</c>.</summary>
    Task<bool> EnsureLoadedAsync(CancellationToken token = default);

    /// <summary>무조건 다시 읽는다(<c>SYNC_CATALOG</c>). 실패하면 <b>옛 카탈로그를 유지</b>하고 <c>false</c>.</summary>
    Task<bool> RefreshAsync(CancellationToken token = default);

    /// <summary>카테고리의 종류축 규칙. 카탈로그에 없으면 <c>null</c>.</summary>
    CatalogTypeAxis? TypeAxis(EnumDeviceCategory category);

    /// <summary>카테고리의 종류축 값 목록. 없으면 빈 목록.</summary>
    IReadOnlyList<CatalogOption> TypeAxisValues(EnumDeviceCategory category);

    /// <summary>그 코드가 이 카테고리의 유효한 종류축 값인가(대소문자 무시).</summary>
    bool IsTypeAxisValue(EnumDeviceCategory category, string? code);

    /// <summary>종류축 코드의 표시명. 카탈로그에 없는 값(승계 불가·미대응)은 <b>원값 그대로</b>, <c>null</c> 은 빈 문자열.</summary>
    string TypeAxisLabel(EnumDeviceCategory category, string? code);

    /// <summary>카테고리의 추가 축들. 없으면 빈 목록.</summary>
    IReadOnlyList<CatalogExtraAxis> ExtraAxes(EnumDeviceCategory category);

    /// <summary>
    /// 이름으로 어휘를 얻는다 — 열린 어휘(<c>component_type</c>·<c>component_state</c>·<c>metric_key</c>…)와
    /// 엄격 어휘(<c>connection_type</c>·<c>component_health</c>…)를 같은 이름 공간에서 찾는다.
    /// </summary>
    /// <param name="includeDeprecated"><c>false</c>(기본)면 폐기 표지된 값은 뺀다 — 새 선택 목록용.</param>
    /// <param name="appliesTo">주면 그 카테고리에 달 수 있는 값만(<c>applies_to</c>). 제한이 없는 어휘는 전부 통과.</param>
    IReadOnlyList<CatalogOption> Vocabulary(string name, bool includeDeprecated = false, EnumDeviceCategory? appliesTo = null);

    /// <summary>어휘 코드의 표시명(폐기된 값 포함). 없으면 원값 그대로.</summary>
    string LabelOf(string vocabularyName, string? code);
}
