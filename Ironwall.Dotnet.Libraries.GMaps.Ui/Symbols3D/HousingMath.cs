namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Symbols3D;

/// <summary>Shared camera projection for meshes, ground marks and the FOV lens anchor.</summary>
public static class HousingMath
{
    public const double Pitch = 35;
    public static double SinPitch => Math.Sin(Pitch * Math.PI / 180);
    public static double CosPitch => Math.Cos(Pitch * Math.PI / 180);

    public static double Normalize(double degrees) => double.IsFinite(degrees) ? (degrees % 360 + 360) % 360 : 0;

    /// <summary>
    /// 문 관절 각도(FR-11): 좌측 문짝은 <paramref name="doorOpenAngle"/> 방향, 우측 문짝은 거울(−) —
    /// Y축 우수(右手) 회전에서 음수 각이 +X 오프셋을 +Z(전면)로 보내므로 양개 통문이 바깥으로 함께 열린다.
    /// </summary>
    public static double DoorAngle(HousingJoint joint, double open, double doorOpenAngle)
    {
        double t = double.IsFinite(open) ? Math.Clamp(open, 0, 1) : 0;
        return joint switch
        {
            HousingJoint.DoorLeft => t * doorOpenAngle,
            HousingJoint.DoorRight => -t * doorOpenAngle,
            _ => 0,
        };
    }

    public static double Fit(double width, double height, double modelWidth, double modelHeight, double depth)
    {
        if (!double.IsFinite(width + height + modelWidth + modelHeight + depth) ||
            width <= 0 || height <= 0 || modelWidth <= 0 || modelHeight <= 0 || depth <= 0) return 0;
        double diameter = Math.Sqrt(modelWidth * modelWidth + depth * depth);
        return .9 * Math.Min(width / diameter, height / (modelHeight * CosPitch + diameter * SinPitch));
    }

    // +Z is north, +Y is up. Camera looks north from the south: positive Y yaw is clockwise on the map.
    public static (double x, double y) Project(double x, double y, double z, double yaw, double scale,
        double width, double height, double modelHeight)
    {
        double a = Normalize(yaw) * Math.PI / 180;
        double rx = x * Math.Cos(a) + z * Math.Sin(a);
        double rz = -x * Math.Sin(a) + z * Math.Cos(a);
        return (width / 2 + rx * scale, height / 2 - ((y - modelHeight / 2) * CosPitch + rz * SinPitch) * scale);
    }

    public static (double x, double y) InverseRotate(double x, double y, double cx, double cy, double angle)
    {
        double r = -Normalize(angle) * Math.PI / 180;
        return (cx + (x - cx) * Math.Cos(r) - (y - cy) * Math.Sin(r),
            cy + (x - cx) * Math.Sin(r) + (y - cy) * Math.Cos(r));
    }
}
