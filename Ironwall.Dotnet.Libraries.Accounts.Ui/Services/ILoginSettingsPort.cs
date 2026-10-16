namespace Ironwall.Dotnet.Libraries.Accounts.Ui.Services;

/// <summary>
/// 로그인 설정 창(로그인 화면의 톱니)이 앱 설정을 읽고 쓰는 창구 — 라이브러리는 앱 설정 파일을 모른다.
/// </summary>
/// <remarks>
/// <para>호스트가 구현해 DI 에 등록한다(appsettings <c>AppSettings:IsSsoEnabled</c> 원자 저장). 등록하지 않으면 톱니 단추가 숨는다.</para>
/// <para>SSO 연동은 시작할 때 읽는다 — 바꾼 값은 다시 시작해야 적용된다.</para>
/// <para>호출 스레드: UI.</para>
/// </remarks>
public interface ILoginSettingsPort
{
    /// <summary>저장된 'SSO 연동 사용' 값(지금 돌고 있는 값이 아니라 설정 파일 값). 못 읽으면 예외.</summary>
    bool ReadIsSsoEnabled();

    /// <summary>'SSO 연동 사용' 을 설정 파일에 쓴다. 못 쓰면 예외.</summary>
    void WriteIsSsoEnabled(bool isSsoEnabled);

    /// <summary>앱이 스스로 다시 시작할 수 있는가 — 거짓이면 창은 '다시 시작하면 적용됩니다' 안내만 한다.</summary>
    bool CanRestart { get; }

    /// <summary>앱을 다시 시작한다(<see cref="CanRestart"/> 가 참일 때만 불린다). 못 하면 예외.</summary>
    void Restart();
}
