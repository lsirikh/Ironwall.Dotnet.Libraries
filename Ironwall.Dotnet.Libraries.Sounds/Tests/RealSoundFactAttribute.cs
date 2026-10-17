using System;
using Xunit;

namespace Ironwall.Dotnet.Libraries.Sounds.Tests;

/// <summary>
/// 실제 스피커로 소리를 내는 시험 — 환경변수 <c>IRONWALL_REAL_SOUND_TESTS=1</c> 일 때만 돈다.
/// 사람이 쓰는 PC 에서 전수 시험을 돌리면 소리가 났다(2026-10-14 "야 소리 꺼라").
/// 같은 판정은 소리 없는 가짜 재생기 판본(<see cref="SoundServiceFakePlayerTests"/>)이 늘 돈다.
/// </summary>
public sealed class RealSoundFactAttribute : FactAttribute
{
    public const string ENV = "IRONWALL_REAL_SOUND_TESTS";

    public RealSoundFactAttribute()
    {
        if (Environment.GetEnvironmentVariable(ENV) != "1")
            Skip = $"실제 소리가 난다 — {ENV}=1 일 때만 돈다";
    }
}
