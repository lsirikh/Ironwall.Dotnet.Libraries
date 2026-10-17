using MaterialDesignThemes.Wpf;

namespace Ironwall.Dotnet.Libraries.Theme.Services;

/// <summary>
/// 런타임 Dark/Light 테마 전환 서비스 (FR-03).
/// 토큰 딕셔너리 원자 스왑 + MaterialDesign(PaletteHelper) + MahApps(ThemeManager) 듀얼 엔진 동기화.
/// <para>호출 스레드: UI 스레드 가정. 비-UI 스레드 호출 시 내부에서 Dispatcher로 마샬링한다.</para>
/// </summary>
public interface IThemeService
{
    /// <summary>현재 적용된 베이스 테마(Light/Dark).</summary>
    BaseTheme Current { get; }

    /// <summary>
    /// 테마 전환 완료 후 발화. WPF 리소스에 도달하지 못하는 경로(SkiaSharp 차트 등)의
    /// 수동 재색칠 트리거. 이벤트는 리소스 스왑 완료 후, 락 밖에서 발화한다.
    /// </summary>
    event EventHandler<BaseTheme>? ThemeChanged;

    /// <summary>지정 베이스 테마(Light/Dark)를 적용한다. 동일 테마 재적용은 무시(R-17).</summary>
    void ApplyTheme(BaseTheme theme);

    /// <summary>Light ↔ Dark 토글.</summary>
    void Toggle();

    /// <summary>설정 저장소에서 마지막 테마를 복원해 적용한다. 저장소 없으면 Light 기본.</summary>
    void InitializeFromSettings();
}

/// <summary>
/// 테마 선택 영속화 seam. 구현체(앱 설정/appsettings.json 등)는 Phase 2에서 주입.
/// leaf 어셈블리 무의존 유지를 위해 인터페이스만 여기 둔다.
/// </summary>
public interface IThemeSettingsStore
{
    /// <summary>저장된 테마를 읽는다. 없으면 null(→ ThemeService가 Light 기본 적용).</summary>
    BaseTheme? Load();

    /// <summary>선택 테마를 저장한다.</summary>
    void Save(BaseTheme theme);
}
