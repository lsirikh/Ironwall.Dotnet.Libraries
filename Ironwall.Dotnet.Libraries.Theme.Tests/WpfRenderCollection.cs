using Xunit;

namespace Ironwall.Dotnet.Libraries.Theme.Tests;

/// <summary>
/// WPF 요소를 실제로 만들어 재는 시험(전용 STA 스레드)을 한 줄로 세운다.
/// <para><b>왜</b>: 열 머리 시험(<see cref="ConsoleDataGridHeaderTests"/>)은 컴파일된 테마를 pack:// 로 읽고, 버튼 · 칩 시험은 같은
/// 스타일을 XamlReader 로 읽는다. xUnit 기본값대로 클래스끼리 병렬로 돌면 두 적재 경로가 WPF 내부 잠금을 서로 반대 순서로 잡아
/// 교착한다 — 그 뒤의 STA 시험은 전부 30초 시간 초과로 떨어졌다(2026-09-27 실측: 전체 실행 3회 중 1회 25건 실패, 단독은 늘 통과).</para>
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class WpfRenderCollection
{
    public const string Name = "WPF 렌더 시험(직렬)";
}
