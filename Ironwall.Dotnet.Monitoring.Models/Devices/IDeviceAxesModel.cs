namespace Ironwall.Dotnet.Monitoring.Models.Devices;

/// <summary>
/// 장비의 v7.0+ <b>표현 축 묶음</b> — 접속(<c>connection</c>) · 형상(<c>hardware_spec</c> + 부품) ·
/// 관측(<c>device_status</c>) · 의도(<c>device_config</c>) + 그 응답이 무엇을 실었는지(<c>meta</c>).
/// </summary>
/// <remarks>
/// <para><b>왜 한 묶음인가</b> — 축을 장비 모델에 낱개 속성으로 두면 재조회·<c>SYNC_DEVICE</c> 의
/// 속성 복사(<c>UpdateDeviceProperties</c>)가 새 축이 생길 때마다 한 줄씩 늘고, 한 줄을 빠뜨리면
/// 그 축이 조용히 옛 값으로 남는다(device-console-v8 ISSUE-17). 묶음 참조 하나를 바꾸면 빠뜨릴 수 없다.</para>
/// <para><b><c>null</c> 의 뜻</b> — 묶음 자체가 <c>null</c> 이면 "축을 하나도 받지 못했다"(6.3 응답).
/// 묶음 안의 축이 <c>null</c> 이면 "그 절이 이 응답에 실리지 않았다"(<c>view=basic</c>) — <b>빈 값이 아니다</b>.
/// 무엇이 실렸는지의 유일한 기준은 <see cref="Meta"/> 의 <c>sections</c> 다(서버 D15).</para>
/// </remarks>
public interface IDeviceAxesModel
{
    /// <summary>접속 축 — 1차에서 유일하게 편집 가능한 축(서버가 객체 병합으로 받는다).</summary>
    IConnectionAxisModel? Connection { get; set; }

    /// <summary>형상 축 — 제원 + 부품 선언. <b>읽기 전용</b>(부품 배열은 서버가 통째 교체한다).</summary>
    IHardwareSpecModel? HardwareSpec { get; set; }

    /// <summary>관측 축 — 부품별 상태·건강. <b>읽기 전용</b>(요청에 실으면 422 <c>OBSERVED_FIELD</c>).</summary>
    IDeviceStatusModel? DeviceStatus { get; set; }

    /// <summary>의도 축 — 임계치·모드·부품 덮어쓰기. 1차는 <b>읽기 전용</b>.</summary>
    IDeviceConfigModel? DeviceConfig { get; set; }

    /// <summary>이 축들을 실어 온 응답의 <c>meta.view</c>·<c>meta.sections</c>.</summary>
    ResponseMeta? Meta { get; set; }

    /// <summary>
    /// 그 절이 응답에 <b>실려 왔는가</b>. <c>meta.sections</c> 에 이름이 있을 때만 <c>true</c> —
    /// 값이 비었는지와 무관하다. meta 가 없으면(6.3) 어떤 절도 받았다고 하지 않는다.
    /// </summary>
    bool IsSectionReceived(string section);

    /// <summary>
    /// 부품 <b>유형</b>(<c>DOOR_ACTUATOR</c> 등)으로 관측 상태를 찾는다.
    /// 부품 <c>key</c> 는 장비마다 자유라 코드에 박을 수 없다 — 형상 축의 선언에서 유형으로 key 를 얻어 관측 축을 읽는다.
    /// 선언이 없거나 아직 관측이 없으면 <c>null</c>.
    /// </summary>
    ComponentStatusModel? FindStatusByType(string componentType);
}
