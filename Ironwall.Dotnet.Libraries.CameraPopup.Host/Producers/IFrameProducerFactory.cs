using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Host.Producers;

/// <summary>타일 · 오버레이 생산자를 만드는 곳(시험은 가짜 생산자를 끼운다). 어느 스레드에서나 불린다.</summary>
internal interface IFrameProducerFactory
{
    /// <param name="cameraId">제공자 캐시 · PTZ 와 같은 카메라 키. 비우면 <paramref name="name"/>.</param>
    /// <param name="priority">연결 줄 앞에 선다(사람이 방금 연 오버레이 · "다시 시도").</param>
    /// <param name="attempt">몇 번째 재시도인가(0 = 첫 시도).</param>
    IFrameProducer Create(VideoProviderInfo provider, int width, int height, string name, string? cameraId = null, bool priority = false, int attempt = 0);
}
