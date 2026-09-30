using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Symbols3D;

/****************************************************************************
   Purpose      : 하우징 재질 토큰 → 브러시 매핑(단일 정본)
   Created By   : Claude Code
   Created On   : 2026-09-08
   Company      : Sensorway Co., Ltd.
****************************************************************************/

/// <summary>
/// OBJ 재질 토큰(<c>mat_body</c> 등)을 실제 브러시로 바꾸는 <b>유일한</b> 자리.
///
/// <para><see cref="HousingVisual"/>(지도 심볼)과 상세 창 3D 프리뷰가 같이 쓴다 —
/// 두 벌로 두면 한쪽만 색이 바뀌어 "지도와 상세 창의 같은 장비가 다른 색"이 된다.</para>
/// </summary>
public static class HousingPalette
{
    /// <summary>재질 토큰 하나를 브러시로 해석한다. 알 수 없는 토큰이 <c>#RRGGBB</c> 면 그 색, 아니면 은색.</summary>
    /// <param name="token">OBJ 재질 이름.</param>
    /// <param name="tint">본체(<c>mat_body</c>)에 섞을 색 — 심볼 채우기 색.</param>
    /// <param name="tintStrength">본체 착색 강도 0~1(<see cref="HousingAppearance.TintStrengthProperty"/>).</param>
    /// <param name="metal">금속부 — 테마 <c>TextSecondaryBrush</c> 연동.</param>
    /// <param name="status">상태 LED(<c>mat_status</c>) — 통문·함체.</param>
    /// <param name="mesh">반투명 철망 패널.</param>
    /// <param name="roof">지붕.</param>
    /// <remarks>
    /// 본체 바탕은 중간 회색(170,178,188)이다. 종전 (220,227,234) 는 거의 흰색이라 채우기 색을 35% 섞어도
    /// 창백한 파스텔이 되어 어두운 지도에서 물 빠져 보였다(2026-09-30 렌더 검토).
    /// <para>제품 외장(<c>mat_olive</c>·<c>mat_black</c>·<c>mat_steel</c>)은 브로셔 실물 색이라 채우기 색을 섞지 않는다 —
    /// 채우기 색은 같은 모델의 명판·띠(<c>mat_body</c>)가 받는다.</para>
    /// </remarks>
    public static Brush Resolve(string token, Color tint, double tintStrength, Brush metal, Brush status, Brush mesh, Brush roof)
        => token switch
        {
            "mat_body" => new SolidColorBrush(Color.FromRgb(
                (byte)(170 * (1 - tintStrength) + tint.R * tintStrength),
                (byte)(178 * (1 - tintStrength) + tint.G * tintStrength),
                (byte)(188 * (1 - tintStrength) + tint.B * tintStrength))),
            "mat_metal" => metal,
            "mat_trim" => new SolidColorBrush(Color.FromRgb(35, 47, 60)),
            "mat_glass" => new SolidColorBrush(Color.FromRgb(18, 57, 78)),
            "mat_led" => new SolidColorBrush(Color.FromRgb(74, 222, 213)),
            "mat_status" => status,
            "mat_mesh" => mesh,
            "mat_roof" => roof,
            "mat_olive" => Olive,
            "mat_black" => Black,
            "mat_steel" => Steel,
            "mat_white" => White,
            "mat_soil" => Soil,
            "mat_grass" => Grass,
            _ => token.StartsWith('#') ? (Brush)new BrushConverter().ConvertFromInvariantString(token)! : Brushes.Silver
        };

    // 브로셔 실물 외장색. 테마와 무관한 고정색이라 Frozen 공유가 안전하다(테마 토큰이 아니다).
    private static readonly Brush Olive = Frozen(0x7A, 0x77, 0x38);   // 스마트 복합센서 II·VBUS 보상 유닛 — 군용 올리브
    private static readonly Brush Black = Frozen(0x3C, 0x41, 0x48);   // 스마트 센서·복합센서·펜스센서·제어기 — 검정 PC/ABS(지도에서 묻히지 않게 차콜로)
    private static readonly Brush Steel = Frozen(0xB4, 0xBC, 0xC4);   // 감시시스템 함체 — 스테인리스
    private static readonly Brush White = Frozen(0xE4, 0xE7, 0xE6);   // PIR 프레넬 렌즈·IR LED 돔
    private static readonly Brush Soil = Frozen(0x8A, 0x69, 0x44);    // 지중 센서 단면의 흙
    private static readonly Brush Grass = Frozen(0x6C, 0x96, 0x46);   // 지표 잔디층
    private static Brush Frozen(byte r, byte g, byte b) { var brush = new SolidColorBrush(Color.FromRgb(r, g, b)); brush.Freeze(); return brush; }

    /// <summary>스스로 빛나는 재질 — LED·상태등. 조명 방향과 무관하게 작은 크기에서도 점으로 살아남는다.</summary>
    public static bool IsEmissive(string token) => token is "mat_led" or "mat_status";

    /// <summary>광택 하이라이트 세기(0 이면 없음). 검정 외장은 하이라이트가 없으면 어두운 지도에서 윤곽이 사라진다.</summary>
    public static double SpecularPower(string token) => token switch
    {
        "mat_glass" => 80,
        "mat_metal" or "mat_body" => 28,
        "mat_black" => 36,
        "mat_steel" => 48,
        "mat_white" => 30,
        "mat_olive" => 14,
        _ => 0
    };

    /// <summary>
    /// 지도 심볼과 상세 창 프리뷰가 같이 쓰는 재질 조립. <paramref name="emissive"/> 는 발광 재질이면 만들어 돌려주고,
    /// 호출자가 재채색 때 <see cref="Resolve"/> 결과를 그대로 넣는다.
    /// </summary>
    public static MaterialGroup Compose(string token, DiffuseMaterial diffuse, out EmissiveMaterial? emissive)
    {
        var group = new MaterialGroup();
        group.Children.Add(diffuse);
        double power = SpecularPower(token);
        if (power > 0) group.Children.Add(new SpecularMaterial(new SolidColorBrush(Color.FromArgb(110, 235, 248, 255)), power));
        emissive = IsEmissive(token) ? new EmissiveMaterial(Brushes.Transparent) : null;
        if (emissive != null) group.Children.Add(emissive);
        return group;
    }
}
