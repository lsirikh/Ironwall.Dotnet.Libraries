using System;

namespace Ironwall.Dotnet.Libraries.Reports.Ui.Consoles.Preview;

/// <summary>
/// ★ 공역 차단 게이트 — 같은 창에 WPF 팝업을 띄우기 <b>전에</b> 미리보기(WebView2)를 내린다.
/// </summary>
/// <remarks>
/// <para>WebView2 는 네이티브 창이라 같은 최상위 창 안에서 <b>WPF 팝업 위에</b> 그려진다.
/// 그래서 확인 · 안내 · 진행 팝업을 그냥 띄우면 팝업이 미리보기 뒤로 깔려 <b>클릭조차 안 된다</b>
/// — 보고서 다운로드 실패 안내에서 실제로 당한 결함이다.</para>
/// <para>쓰는 쪽은 <c>using var _ = gate.Block();</c> 한 줄이면 된다. 되돌리는 것을 잊을 수 없게
/// <see cref="IDisposable"/> 로 돌려준다(겹쳐 잠가도 안전하다 — 안쪽이 풀려도 바깥이 잠겨 있으면 내려간 채다).</para>
/// </remarks>
public interface IPreviewAirspaceGate
{
    /// <summary>미리보기를 자리표시자로 내린다. 돌려준 것을 버리면 원래대로 돌아온다.</summary>
    IDisposable Block();
}

/// <summary>게이트가 없을 때(단위 테스트 · 디자인 타임) 쓰는 아무 일도 안 하는 게이트.</summary>
public sealed class NullPreviewAirspaceGate : IPreviewAirspaceGate
{
    public static IPreviewAirspaceGate Instance { get; } = new NullPreviewAirspaceGate();

    public IDisposable Block() => NoopScope.Instance;

    private sealed class NoopScope : IDisposable
    {
        public static readonly NoopScope Instance = new();
        public void Dispose() { }
    }
}
