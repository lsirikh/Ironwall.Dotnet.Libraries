using System.Collections.Concurrent;
using System.Windows.Media.Media3D;
using Ironwall.Dotnet.Libraries.Enums;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.Symbols3D;

public static class HousingModels
{
    private static readonly ConcurrentDictionary<string, HousingModel> Cache = new();
    public static string? DeviceKey(EnumDeviceType type, string? variant = null) => type switch
    {
        EnumDeviceType.IpCamera => variant?.ToLowerInvariant() switch { "dome" => "camera.dome", "ptz" => "camera.ptz", _ => "camera" },
        EnumDeviceType.Controller => "controller",
        EnumDeviceType.Multi => "multi",
        EnumDeviceType.Fence => "fence",
        EnumDeviceType.Underground => "underground",
        EnumDeviceType.Contact => "contact",
        EnumDeviceType.PIR => "pir",
        EnumDeviceType.IoController => "iocontroller",
        EnumDeviceType.Laser => "laser",
        EnumDeviceType.SmartSensor or EnumDeviceType.SmartSensor2 or EnumDeviceType.SmartCompound => "sensor",
        EnumDeviceType.IpSpeaker => "speaker",
        EnumDeviceType.Radar => "radar",
        EnumDeviceType.Lamp => "lamp",
        EnumDeviceType.Enclosure => "enclosure",
        EnumDeviceType.SmartMultisensor2 => "smartmulti",
        EnumDeviceType.Gate => "fencegate",   // 통문(D2). "gate" 는 시설물 정문(BuildInfrastructure) 키라 충돌 금지(분석 C-2)
        _ => null
    };

    public static HousingModel Get(string key, int floors = 1) => Cache.GetOrAdd(
        key + ":" + Math.Clamp(floors, 1, 12), _ => HousingObjLoader.TryLoad(key) ?? Create(key, Math.Clamp(floors, 1, 12)));

    private static HousingModel Create(string key, int floors)
    {
        var m = new HousingMeshBuilder();
        var lens = new Point3D(0, .6, .4);
        void Box(string t, double x, double y, double z, double w, double h, double d)
        { if (t == "mat_body") m.BeveledBox(t, x, y, z, w, h, d); else m.Box(t, x, y, z, w, h, d); }
        void Tube(string t, double x, double y, double z, double x2, double y2, double z2, double r, double r2, int n = 12)
            => m.Tube(t, new(x, y, z), new(x2, y2, z2), r, r2, n);
        void Base(double w = .6, double d = .48) { Box("mat_trim", 0, 0, 0, w, .07, d); Box("mat_metal", 0, .07, 0, w * .9, .04, d * .9); }
        void Pole(double height = 1.1) { Base(.4, .4); Tube("mat_metal", 0, .1, -.15, 0, height, -.15, .045, .045, 8); }
        void Lens(double x, double y, double z, double r)
        {
            Tube("mat_trim", x, y, z - .04, x, y, z + .012, r * 1.3, r * 1.3, 16);
            Tube("mat_glass", x, y, z + .013, x, y, z + .025, r, r, 16);
            Tube("mat_led", x - r * .28, y + r * .25, z + .026, x - r * .28, y + r * .25, z + .028, r * .22, r * .22, 8);
        }
        switch (key)
        {
            case "camera":
                Base(.44, .5); Box("mat_metal", 0, .11, -.24, .14, .3, .15);
                Tube("mat_trim", 0, .39, -.27, 0, .6, -.05, .065, .065, 8);
                // 고정형은 독립 헤드가 없다 — 배럴·실드·렌즈가 본체와 함께 Bearing 으로 돈다(D-11). 독립 회전은 PTZ 만.
                Tube("mat_body", 0, .64, -.28, 0, .64, .35, .155, .17, 16);
                Box("mat_body", 0, .79, .055, .4, .045, .78); // long sun shield, clearly a bullet
                Box("mat_metal", 0, .755, -.19, .36, .03, .3);
                Lens(0, .64, .36, .13); lens = new(0, .64, .385);
                Box("mat_led", .115, .59, .373, .035, .018, .02);
                break;
            case "camera.dome":
            case "camera.ptz":
                bool ptz = key.EndsWith("ptz");
                m.HeadPivot = new Point3D(0, 0, .18);
                Base(.46, .5); Box("mat_metal", 0, .1, -.3, .12, 1.03, .12);
                Box("mat_body", 0, 1.05, -.05, .15, .11, .6); // suspended bracket above the dome
                Tube("mat_metal", 0, .93, .18, 0, 1.05, .18, .075, .075);
                Tube("mat_body", 0, ptz ? .63 : .77, .18, 0, .94, .18, ptz ? .23 : .25, .17, 16);
                Tube("mat_trim", 0, ptz ? .61 : .75, .18, 0, ptz ? .67 : .79, .18, .245, .245, 16);
                m.Dome("mat_glass", 0, ptz ? .63 : .77, .18, ptz ? .235 : .23);
                m.Head = ptz; Lens(0, ptz ? .54 : .69, .35, ptz ? .085 : .07); m.Head = false;   // 돔형은 고정 렌즈, PTZ 만 헤드(D-11)
                lens = new(0, ptz ? .54 : .69, .375);
                Box("mat_led", .12, .85, .366, .055, .025, .018); break;
            case "speaker":
                Base(.43, .43); Box("mat_metal", 0, .1, -.15, .12, .33, .14);
                Box("mat_metal", 0, .4, -.05, .34, .08, .48);
                Tube("mat_body", 0, .67, -.44, 0, .67, -.08, .19, .19, 16); // full depth driver
                Tube("mat_metal", 0, .67, -.29, 0, .67, -.25, .204, .204, 16);
                m.Tube("mat_body", new(0, .67, -.08), new(0, .67, .38), .12, .34, 16, false);
                // inner cone and dark throat leave a visibly open mouth
                m.Tube("mat_metal", new(0, .67, .385), new(0, .67, .04), .312, .095, 16, false);
                Tube("mat_trim", 0, .67, .035, 0, .67, .043, .095, .095, 12);
                lens = new(0, .67, .39); break;
            case "enclosure":
                Base(.68, .48); Box("mat_body", 0, .11, 0, .6, .91, .37);
                Box("mat_metal", 0, 1.02, 0, .68, .055, .45);
                Box("mat_trim", 0, .2, .192, .52, .73, .014);   // 도어 프레임(본체 고정)
                // 도어(FR-11): 패널·손잡이·루버 4·상태 LED 를 DoorLeft 관절로 — 경첩 (−.26, 0, .2), DoorOpen=1 에서 −75°(자유단이 +Z 전면으로).
                m.Joint = HousingJoint.DoorLeft; m.SetPivot(HousingJoint.DoorLeft, new Point3D(-.26, 0, .2));
                Box("mat_body", 0, .215, .208, .49, .7, .018);
                Box("mat_metal", .17, .53, .229, .035, .15, .02);
                for (int i = 0; i < 4; i++) Box("mat_trim", 0, .31 + i * .048, .227, .31, .017, .02);
                Box("mat_status", -.17, .82, .233, .045, .035, .015);   // 상태 LED — 이벤트 색(mat_status)
                m.Joint = HousingJoint.None;
                m.DoorOpenAngle = Helpers.Fence.FenceDefaults.EnclosureOpenAngleDeg;
                m.IncludeDoorSweepInBounds = false;   // R-06: 닫힌 함체 크기 보존 — 열린 문은 박스 밖으로 그려진다(ClipToBounds=false)
                break;
            case "controller":
            case "iocontroller":
                Base(.95, .55); Box("mat_body", 0, .11, 0, .87, .27, .46);
                Box("mat_metal", 0, .38, 0, .9, .035, .5);
                for (int i = 0; i < 5; i++) Box("mat_trim", -.29 + i * .145, .17, .242, .095, .1, .014);
                Box("mat_led", -.32, .31, .25, .04, .025, .015);
                for (int i = 0; i < 4; i++) Box("mat_trim", 0, .417, -.15 + i * .075, .65, .009, .025); break;
            case "fence":
                for (int s = -1; s <= 1; s += 2) { Box("mat_trim", s * .43, 0, 0, .18, .06, .24); Tube("mat_metal", s * .43, .06, 0, s * .43, .84, 0, .045, .045, 8); }
                Box("mat_metal", 0, .72, 0, .86, .028, .04); Box("mat_metal", 0, .18, 0, .86, .028, .04);
                for (int i = 0; i < 7; i++) Box("mat_body", -.36 + i * .12, .18, 0, .012, .54, .018);
                for (int i = 0; i < 4; i++) Box("mat_body", 0, .25 + i * .12, 0, .86, .012, .018); break;
            case "lamp":
                Pole(1.12); Box("mat_body", 0, 1.02, .015, .56, .18, .3);
                Box("mat_trim", 0, 1.01, .18, .5, .16, .03);
                for (int i = 0; i < 4; i++) Box("mat_led", -.18 + i * .12, 1.045, .2, .08, .085, .02); break;
            case "radar":
                Pole(.5); Tube("mat_metal", 0, .48, -.1, 0, .65, 0, .065, .065);
                Box("mat_body", 0, .53, 0, .69, .56, .17); Box("mat_glass", 0, .565, .094, .59, .49, .025);
                Box("mat_led", .24, .59, .113, .05, .03, .013); break;
            case "underground":
                Base(.7, .6); Box("mat_body", 0, .11, 0, .58, .065, .48);
                Box("mat_led", 0, .18, 0, .065, .018, .3); Box("mat_led", 0, .18, 0, .3, .018, .065);
                Box("mat_metal", -.21, .17, -.14, .06, .24, .06); break;
            case "contact":
                Base(.63, .35); Box("mat_body", -.13, .11, 0, .24, .49, .22);
                Box("mat_body", .16, .11, 0, .15, .49, .22); Box("mat_led", -.13, .48, .12, .085, .035, .012); break;
            case "laser":
                Pole(.35); Tube("mat_body", 0, .55, -.22, 0, .55, .28, .17, .17, 12);
                Lens(0, .55, .29, .1); Box("mat_led", 0, .71, 0, .07, .035, .1); lens = new(0, .55, .32); break;
            case "pir":
                Base(.38, .35); Box("mat_body", 0, .11, 0, .34, .6, .24);
                Tube("mat_glass", 0, .36, .13, 0, .36, .16, .125, .125, 12);
                Box("mat_led", 0, .6, .13, .055, .035, .02); break;
            case "multi":
            case "smartmulti":
            case "sensor":
                Base(.57, .42); Box("mat_body", 0, .11, 0, .48, .78, .28);
                Box("mat_trim", 0, .24, .15, .39, .54, .025);
                Box("mat_metal", 0, .9, .015, .59, .045, .4); // rain canopy
                int count = key == "sensor" ? 2 : 3;
                for (int i = 0; i < count; i++) Lens(0, .34 + i * .17, .18, .067);
                Box("mat_led", .14, .8, .166, .04, .025, .016); break;
            case "fencegate":
            {
                // 통문(D2, FR-11): 기둥 2 · 상단 가로대 · 양개 문짝(DoorLeft/DoorRight, 경첩 x=∓.43) · 상태 LED(mat_status).
                // 문짝은 DoorOpen=1 에서 −80°(좌)/+80°(우)로 +Z(전면)를 향해 함께 열린다. "gate" 는 시설물 정문 키라 별도(분석 C-2).
                const double postX = .5, postH = 1.0, leafW = .41, leafH = .82, hingeX = .43;
                Base(1.3, .5);
                Tube("mat_metal", -postX, .07, 0, -postX, postH, 0, .045, .045, 10);
                Tube("mat_metal", postX, .07, 0, postX, postH, 0, .045, .045, 10);
                m.Box("mat_metal", 0, postH - .03, 0, postX * 2 + .1, .05, .06);   // 상단 가로대
                m.Box("mat_trim", -postX, postH + .02, 0, .1, .04, .1); m.Box("mat_trim", postX, postH + .02, 0, .1, .04, .1);   // 기둥 캡
                m.Box("mat_status", -postX, postH + .07, 0, .07, .05, .07);   // 상태 LED(이벤트 색)
                void Leaf(HousingJoint joint, double sign)
                {
                    m.Joint = joint; m.SetPivot(joint, new Point3D(sign * hingeX, 0, 0));
                    double cx = sign * (hingeX - leafW / 2);
                    m.Box("mat_metal", cx, .12, 0, leafW, .03, .025);                      // 하단 프레임
                    m.Box("mat_metal", cx, .12 + leafH - .03, 0, leafW, .03, .025);        // 상단 프레임
                    m.Box("mat_metal", sign * (hingeX - .0125), .12, 0, .025, leafH, .025); // 경첩쪽 세로 프레임
                    m.Box("mat_metal", sign * (hingeX - leafW + .0125), .12, 0, .025, leafH, .025); // 자유단 세로 프레임
                    for (int i = 1; i <= 5; i++) m.Box("mat_trim", sign * (hingeX - i * leafW / 6), .15, 0, .012, leafH - .06, .012);   // 세로 살
                    m.Box("mat_mesh", cx, .15, 0, leafW - .05, leafH - .06, .006);         // 반투명 철망 패널(양면)
                    m.Joint = HousingJoint.None;
                }
                Leaf(HousingJoint.DoorLeft, -1); Leaf(HousingJoint.DoorRight, 1);
                m.DoorOpenAngle = Helpers.Fence.FenceDefaults.GateOpenAngleDeg;
                lens = new(0, .5, .05);
                break;
            }
            default:
                BuildInfrastructure(m, key, floors); break;
        }
        return m.Build(lens);
    }

    private static void BuildInfrastructure(HousingMeshBuilder m, string key, int floors)
    {
        key = key.ToLowerInvariant().Replace("infra.", "");
        void B(string t, double x, double y, double z, double w, double h, double d) => m.Box(t, x, y, z, w, h, d);
        void T(string t, double x, double y, double z, double y2, double r) => m.Tube(t, new(x, y, z), new(x, y2, z), r, r, 12);
        B("mat_trim", 0, 0, 0, 1.12, .055, .85);
        if (key == "gate")
        {
            B("mat_body", -.44, .055, 0, .18, .68, .24); B("mat_body", .44, .055, 0, .18, .68, .24);
            B("mat_roof", 0, .735, 0, 1.1, .12, .31); B("mat_metal", 0, .38, 0, .8, .045, .05);
            for (int i = 0; i < 6; i++) B("mat_led", -.33 + i * .13, .385, -.03, .06, .038, .01);
            return;
        }
        if (key is "watchtower" or "antenna" or "watertower")
        {
            // 층수 → 높이(D-6). 기본 3층에서 종전 형상과 동일하도록 계수를 맞췄다.
            double lh = key == "antenna" ? .91 : Math.Clamp(.55 + floors * .12, .67, 1.5);          // 다리 높이
            for (int x = -1; x <= 1; x += 2) for (int z = -1; z <= 1; z += 2) B("mat_metal", x * .25, .05, z * .2, .055, lh, .055);
            if (key == "antenna")
            {
                double mh = Math.Clamp(1.11 + floors * .13, 1.2, 2.4);                                    // 마스트 높이
                T("mat_body", 0, .05, 0, mh, .04); B("mat_glass", 0, mh - .5, 0, .5, .25, .08); B("mat_roof", 0, mh, 0, .11, .04, .11); return; // 마스트=mat_body·캡=mat_roof (D-5)
            }
            B("mat_metal", 0, lh - .01, 0, .74, .06, .64);
            if (key == "watertower") T("mat_body", 0, lh + .05, 0, lh + .49, .36);
            else { B("mat_body", 0, lh + .05, 0, .57, .27, .47); B("mat_glass", 0, lh + .13, -.243, .48, .14, .015); }
            B("mat_roof", 0, lh + .34, 0, .74, .065, .64); return;
        }
        if (key == "watertank") { T("mat_body", 0, .055, 0, .67, .38); T("mat_roof", 0, .67, 0, .71, .39); T("mat_metal", 0, .71, 0, .77, .1); return; }
        if (key == "helipad")
        {
            B("mat_body", 0, .055, 0, 1.02, .025, .76);
            for (int s = -1; s <= 1; s += 2) { B("mat_roof", 0, .08, s * .365, 1.02, .012, .03); B("mat_roof", s * .495, .08, 0, .03, .012, .76); } // 테두리=mat_roof (D-5)
            B("mat_led", -.13, .085, 0, .045, .01, .3); B("mat_led", .13, .085, 0, .045, .01, .3); B("mat_led", 0, .085, 0, .26, .01, .04); return;
        }
        if (key == "bridge")
        {
            B("mat_body", 0, .35, 0, 1.1, .08, .52);
            for (int i = -1; i <= 1; i += 2) { B("mat_metal", i * .35, .05, 0, .1, .3, .46); B("mat_roof", 0, .49, i * .26, 1.1, .035, .025); }
            return;
        }
        if (key == "powerpole")
        {
            double ph = Math.Clamp(.91 + floors * .13, 1.0, 2.2);                                          // 층수 → 기둥 높이(D-6)
            T("mat_metal", 0, .05, 0, ph, .045); B("mat_body", 0, ph - .2, 0, .76, .07, .06); B("mat_roof", 0, ph, 0, .09, .035, .09); // 캡=mat_roof (D-5)
            for (int i = -1; i <= 1; i++) T("mat_glass", i * .29, ph - .13, 0, ph - .03, .045); return;
        }
        if (key == "generator")
        {
            B("mat_body", 0, .09, 0, .85, .42, .49); B("mat_roof", 0, .51, 0, .91, .055, .54);
            for (int i = 0; i < 6; i++) B("mat_trim", -.22 + i * .085, .18, .253, .04, .23, .018);
            T("mat_metal", .29, .56, -.1, .76, .045); B("mat_led", -.29, .42, .26, .08, .03, .015); return;
        }
        double h = key == "guardpost" ? .38 : Math.Min(.9, .3 + floors * .055);
        double w = key == "guardpost" ? .57 : .94, d = key == "barracks" ? .44 : .65;
        m.BeveledBox("mat_body", 0, .055, 0, w, h, d); B("mat_roof", 0, h + .06, 0, w + .09, .055, d + .09);
        int bands = Math.Min(floors, 8);
        for (int j = 0; j < bands; j++)
        {
            double y = .12 + j * (h - .08) / bands;
            double band = Math.Min(.08, (h - .1) / bands * .65);
            B("mat_glass", -.025, y, d / 2 + .008, w * .78, band, .016);
            B("mat_glass", -.025, y, -d / 2 - .008, w * .78, band, .016);
            B("mat_glass", w / 2 + .008, y, 0, .015, band, d * .75);
        }
        B("mat_trim", w * .33, .055, d / 2 + .012, .12, .25, .027);
        if (key == "barracks")
        {
            var a = new Point3D(-.52, h + .11, -.29); var b = new Point3D(.52, h + .11, -.29);
            var c = new Point3D(.52, h + .27, 0); var e = new Point3D(-.52, h + .27, 0);
            m.Quad("mat_roof", a, b, c, e); m.Quad("mat_roof", e, c, new(.52, h + .11, .29), new(-.52, h + .11, .29));
        }
        if (key == "factory") { T("mat_metal", -.29, h + .11, .13, h + .42, .055); T("mat_metal", -.08, h + .11, .13, h + .34, .055); }
        if (key == "hospital") { B("mat_led", 0, h + .12, 0, .07, .012, .29); B("mat_led", 0, h + .12, 0, .29, .012, .07); }
        if (key == "warehouse") { B("mat_metal", -.12, .055, d / 2 + .03, .45, .29, .02); for (int i = 0; i < 5; i++) B("mat_trim", -.12, .085 + i * .047, d / 2 + .043, .4, .009, .008); }
        if (key == "datacenter") for (int i = 0; i < 3; i++) B("mat_metal", -.3 + i * .3, h + .12, .03, .21, .07, .27);
    }
}
