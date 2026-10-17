namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Services;

/// <summary>
/// 지도 조립 카드(L3, component-display-unify FR-05)의 [장비 콘솔에서 열기] · [결선에서 보기] 가 부르는 입구 — <b>선택</b>.
/// </summary>
/// <remarks>
/// <para>장비 콘솔 창은 호스트(좌측 메뉴)가 연다 — 라이브러리 안에는 "이 장비를 콘솔에서 열어라"는 경로가 아직 없다.
/// 그래서 호스트가 이 인터페이스를 등록할 때만 단추가 보인다. 등록이 없으면 카드는 두 단추를 <b>숨긴다</b>(누를 수 없는 단추를 두지 않는다).</para>
/// <para>호출 스레드: UI.</para>
/// </remarks>
public interface IDeviceConsoleNavigator
{
    /// <summary>이 장비를 장비 콘솔에서 열 수 있는가(권한 · 창 가용).</summary>
    bool CanOpenDevice(int deviceId);

    /// <summary>장비 콘솔을 열고 그 장비를 고른다.</summary>
    void OpenDevice(int deviceId);

    /// <summary>이 장비(센서)를 결선 창에서 보일 수 있는가.</summary>
    bool CanOpenWiring(int deviceId);

    /// <summary>결선 창을 열고 그 센서를 고른다.</summary>
    void OpenWiring(int deviceId);
}
