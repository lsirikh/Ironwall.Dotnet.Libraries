using Newtonsoft.Json;

namespace Ironwall.Dotnet.Libraries.GMaps.Models;
/****************************************************************************
   Purpose      : 지도 회전 상태 영속(사용자 요구 2026-07-28 — "재시작해도 설정 유지").
                  나침반(회전 기능) ON/OFF + 회전각을 AppSettings.MapRotation에 저장,
                  부팅 시 앵커 적용 '이후' 복원(A모드 앵커면 SSOT가 회전 차단 — 정책 유지).
   Note         : V-07 재해석 — 사용자 원결정은 'Undo 비대상'이며 영속 배제가 아니었음.
   Created On   : 2026-07-28 · Sensorway Co., Ltd.
 ****************************************************************************/
public class MapRotationModel
{
    /// <summary>회전 기능(나침반) 활성 여부 — 부팅 시 이 값으로 kill-switch 복원.</summary>
    public bool IsEnabled { get; set; }

    /// <summary>마지막 회전각(canonical [-180,180)). IsEnabled=true일 때만 복원 적용.</summary>
    public double Angle { get; set; }

    public override string ToString() => $"MapRotation[enabled={IsEnabled}, angle={Angle:F1}]";
}
