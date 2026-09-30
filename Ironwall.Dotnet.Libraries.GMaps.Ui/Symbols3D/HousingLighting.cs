using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Symbols3D;

/****************************************************************************
   Purpose      : 하우징 스튜디오 조명(단일 정본)
   Created By   : Claude Code
   Created On   : 2026-09-30
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// 지도 심볼(<see cref="HousingVisual"/>)과 상세 창 3D 프리뷰가 같이 쓰는 조명. 두 벌로 두면 같은 장비가 다르게 보인다.
///
/// <para><b>평탄광을 낮춘 이유</b>: 종전 AmbientLight(112,125,145)는 전체 밝기의 약 44%라 윗면·옆면 명암 차가 눌려
/// 입체감이 죽었다. 입체감은 빛의 세기가 아니라 <b>빛의 차이</b>에서 나온다(2026-09-30 렌더 검토).</para>
/// <para>주광은 왼쪽 위 앞에서, 보조광은 반대쪽에서 차갑게, 지면 반사광은 아래에서 약하게 — 밑면이 새까맣게 꺼지지 않도록.</para>
/// </summary>
public static class HousingLighting
{
    /// <summary>장면에 조명 4개를 넣는다. 물체 그룹은 호출자가 <b>마지막</b>에 넣는다(테스트가 <c>Children.Last()</c> 로 찾는다).</summary>
    public static void AddTo(Model3DGroup scene)
    {
        scene.Children.Add(new AmbientLight(Color.FromRgb(72, 80, 94)));
        scene.Children.Add(new DirectionalLight(Color.FromRgb(255, 250, 238), new Vector3D(-2.0, -3.4, 1.1)));   // 주광
        scene.Children.Add(new DirectionalLight(Color.FromRgb(106, 146, 200), new Vector3D(2.2, -.6, -1.6)));   // 보조광
        scene.Children.Add(new DirectionalLight(Color.FromRgb(46, 56, 74), new Vector3D(0, 1, .25)));           // 지면 반사
    }
}
