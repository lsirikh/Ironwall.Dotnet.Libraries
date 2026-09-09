using System.Windows.Media;

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
    public static Brush Resolve(string token, Color tint, double tintStrength, Brush metal, Brush status, Brush mesh, Brush roof)
        => token switch
        {
            "mat_body" => new SolidColorBrush(Color.FromRgb(
                (byte)(220 * (1 - tintStrength) + tint.R * tintStrength),
                (byte)(227 * (1 - tintStrength) + tint.G * tintStrength),
                (byte)(234 * (1 - tintStrength) + tint.B * tintStrength))),
            "mat_metal" => metal,
            "mat_trim" => new SolidColorBrush(Color.FromRgb(35, 47, 60)),
            "mat_glass" => new SolidColorBrush(Color.FromRgb(18, 57, 78)),
            "mat_led" => new SolidColorBrush(Color.FromRgb(74, 222, 213)),
            "mat_status" => status,
            "mat_mesh" => mesh,
            "mat_roof" => roof,
            _ => token.StartsWith('#') ? (Brush)new BrushConverter().ConvertFromInvariantString(token)! : Brushes.Silver
        };
}
