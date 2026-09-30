namespace Ironwall.Dotnet.Libraries.CameraPopup.Host.EventWindow;

/// <summary>시간 추상화(testing-patterns I-02) — 타이머 닫기 · delay 표시를 헤드리스로 시험한다.</summary>
internal interface IHostClock
{
    DateTime UtcNow { get; }
}
