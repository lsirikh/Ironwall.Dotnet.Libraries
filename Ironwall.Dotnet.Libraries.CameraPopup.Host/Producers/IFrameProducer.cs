using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;
using Ironwall.Dotnet.Libraries.CameraPopup.Host.Frames;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Host.Producers;

/// <summary>프레임 생산자(시험 무늬 · LibVLC). 상태 변화는 콜백으로 알린다(FR-26 — 실패는 그 스트림만).</summary>
internal interface IFrameProducer : IDisposable
{
    void Start(IFrameSink sink, Action<StreamState, string?> onState);
}
