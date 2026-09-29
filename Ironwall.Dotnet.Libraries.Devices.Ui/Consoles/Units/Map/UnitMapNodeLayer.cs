using System;
using System.Windows;
using System.Windows.Controls;

namespace Ironwall.Dotnet.Libraries.Devices.Ui.Consoles.Units.Map;

/****************************************************************************
   Purpose      : 부대 관계도 노드 층 — 첫 뷰가 정해지기 전에는 노드를 재지 않는다 (NFR-01)
   Created By   : GHLee
   Created On   : 9/29/2026
   Department   : SW Team
   Company      : Sensorway Co., Ltd.
   Email        : lsirikh@naver.com
****************************************************************************/

/// <summary>
/// 노드 요소를 담는 <see cref="Canvas"/>. <paramref name="isDeferred"/> 가 참인 동안(캔버스 크기가 아직 없어 첫 뷰 요청이 미뤄져 있다)
/// 자식을 재지도 놓지도 않는다 — 노드의 템플릿은 첫 측정에서 입혀지므로, 그때는 아직 단계가 정해지지 않았다.
/// </summary>
/// <remarks>
/// <para><b>왜</b>(2026-09-29 벤치 분해): 레일 전환마다 노드 200개가 처음 단계(L1)로 템플릿을 한 번 입고, 첫 <c>SizeChanged</c> 에서
/// 미뤄 둔 뷰(전체 보기 → L0)가 돌면서 200개를 <b>다시</b> 갈아 끼웠다 — 첫 그림의 레이아웃 373 ms 가운데 절반이 버려지는 템플릿이었다.</para>
/// <para>캔버스는 미뤄 둔 뷰를 적용한 뒤 <see cref="Resume"/> 을 부른다. <c>SizeChanged</c> 는 같은 레이아웃 차례(LayoutManager 반복) 안에서
/// 불리므로 노드는 첫 그림 전에 재어지고 놓인다 — 빈 화면이 한 프레임도 보이지 않는다.</para>
/// </remarks>
internal sealed class UnitMapNodeLayer : Canvas
{
    private readonly Func<bool> _isDeferred;
    private bool _skipped;

    public UnitMapNodeLayer(Func<bool> isDeferred) => _isDeferred = isDeferred ?? throw new ArgumentNullException(nameof(isDeferred));

    /// <summary>미룬 동안 재기를 건너뛴 적이 있는가(시험용).</summary>
    internal bool HasSkipped => _skipped;

    protected override Size MeasureOverride(Size constraint)
    {
        if (_isDeferred()) { _skipped = true; return new Size(); }
        return base.MeasureOverride(constraint);
    }

    protected override Size ArrangeOverride(Size arrangeSize)
    {
        if (_isDeferred()) { _skipped = true; return arrangeSize; }
        return base.ArrangeOverride(arrangeSize);
    }

    /// <summary>미룸이 끝났다 — 건너뛴 적이 있으면 다시 재고 놓게 한다.</summary>
    public void Resume()
    {
        if (!_skipped) return;
        _skipped = false;
        InvalidateMeasure();
        InvalidateArrange();
    }
}
