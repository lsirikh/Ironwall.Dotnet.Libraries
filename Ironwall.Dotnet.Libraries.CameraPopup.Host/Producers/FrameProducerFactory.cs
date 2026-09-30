using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Messages;
using Ironwall.Dotnet.Libraries.CameraPopup.Contracts.Protocol;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Host.Producers;

/// <summary>제공자 종류 → 생산자.</summary>
internal sealed class FrameProducerFactory
{
    private readonly HostLog _log;

    public FrameProducerFactory(HostLog log) => _log = log;

    public IFrameProducer Create(VideoProviderInfo provider, int width, int height, string name)
        => provider.Kind == VideoProviderKind.TestPattern
            ? new TestPatternProducer(name)
            : new LibVlcFrameProducer(provider, width, height, name, _log);
}
