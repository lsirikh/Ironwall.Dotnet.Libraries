using Xunit;

namespace Ironwall.Dotnet.Libraries.CameraPopup.Tests.Survival;

/// <summary>실제 호스트 프로세스를 쓰는 시험은 하나씩 — 시간 측정이 서로 간섭하지 않게.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SurvivalCollection
{
    public const string Name = "CameraPopupHostProcess";
}
