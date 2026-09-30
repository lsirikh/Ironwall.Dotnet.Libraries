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
        // ── 브로셔 제품용 도구(2026-09-30) ─────────────────────────────────────────
        // 치수는 브로셔 실측 mm 를 200 mm = 1 단위로 옮긴다. 정면은 +Z, 위는 +Y.
        static double Mm(double mm) => mm / 200.0;
        void Rod(string t, double x, double y, double z, double x2, double y2, double z2, double r, double r2, int n = 12)
            => m.Tube(t, new(x, y, z), new(x2, y2, z2), r, r2, n, true, true);
        void Disc(string t, double x, double y, double z, double r, double depth, int n = 16)   // +Z 로 내민 원판
            => m.Tube(t, new(x, y, z), new(x, y, z + depth), r, r, n, true, true);
        void Bulb(string t, double x, double y, double z, double r, Vector3D axis) => m.Hemisphere(t, new(x, y, z), axis, r);
        // 철책 기둥 토막 + 브래킷 — 센서는 실제로 높이 1.6~2 m 철책 기둥에 붙는다. 기둥 전체를 그리면 30 px 에서 센서가 점이 되므로 토막만.
        // 기둥은 본체 뒷면에서 .1 뒤(z)에 서고, 브래킷이 그 사이를 잇는다.
        void FencePost(double top, double z, double bracketY)
        {
            Base(.34, .34);
            Rod("mat_metal", 0, .1, z, 0, top, z, .045, .045);
            m.Box("mat_trim", 0, bracketY, z + .05, .15, .09, .14);
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
            {
                // 감시시스템 함체(스마트 복합센서 II 브로셔 p.7) — 스테인리스 외함 + 앞으로 흘러내리는 경사 빗물 지붕.
                Base(.68, .48); m.BeveledBox("mat_steel", 0, .11, 0, .6, .91, .37, .02);
                // 경사 지붕: 뒤가 높고 앞으로 낮아지며 앞·옆으로 처마가 나온다. 닫힌 깊이는 .4~.6 범위를 지킨다(R-06).
                m.Prism("mat_steel", -.35, .35, new (double, double)[] { (1.02, -.215), (1.10, -.215), (1.035, .265), (1.005, .265) });
                Box("mat_trim", 0, .2, .192, .52, .73, .014);   // 도어 프레임(본체 고정)
                for (int i = 0; i < 2; i++) Rod("mat_metal", -.268, .3 + i * .42, .206, -.268, .38 + i * .42, .206, .016, .016, 8);   // 경첩
                // 도어(FR-11): 패널·손잡이·루버·명판·상태 LED 를 DoorLeft 관절로 — 경첩 (−.26, 0, .2), DoorOpen=1 에서 −75°(자유단이 +Z 전면으로).
                m.Joint = HousingJoint.DoorLeft; m.SetPivot(HousingJoint.DoorLeft, new Point3D(-.26, 0, .2));
                m.Box("mat_steel", 0, .215, .208, .49, .7, .018);
                Disc("mat_metal", .185, .58, .217, .026, .022, 12);                        // 잠금 실린더
                m.Box("mat_metal", .185, .44, .219, .028, .11, .02);                       // 손잡이
                for (int i = 0; i < 4; i++) m.Box("mat_trim", 0, .27 + i * .042, .219, .3, .016, .012);   // 하단 환기 루버
                Box("mat_body", .05, .79, .219, .2, .055, .01);                          // 명판 — 채우기 색을 받는 자리
                m.Box("mat_status", -.17, .8, .219, .045, .035, .015);                     // 상태 LED — 이벤트 색(mat_status)
                m.Joint = HousingJoint.None;
                m.DoorOpenAngle = Helpers.Fence.FenceDefaults.EnclosureOpenAngleDeg;
                m.IncludeDoorSweepInBounds = false;   // R-06: 닫힌 함체 크기 보존 — 열린 문은 박스 밖으로 그려진다(ClipToBounds=false)
                break;
            }
            case "controller":
            {
                // 스마트 제어기 S-IW-P104SC-1U (189 × 33.5 × 104 mm) — 함체 안 1U 도킹 모듈. 실물 두께로는 30 px 에서 선이 되므로 높이만 ×1.6.
                double w = Mm(189), h = Mm(33.5) * 1.6, d = Mm(104), y0 = .11, zf = d / 2 + .012;
                Base(w + .07, d + .08); m.BeveledBox("mat_black", 0, y0, 0, w, h, d, .015);
                m.Box("mat_trim", 0, y0 - .004, d / 2 + .006, w + .03, h + .008, .012);                  // 전면 패널
                for (int s = -1; s <= 1; s += 2) Rod("mat_metal", s * (w / 2 - .02), y0 + h / 2, zf, s * (w / 2 - .02), y0 + h / 2, zf + .035, .028, .028, 10);   // 나사 손잡이
                m.Box("mat_metal", -.29, y0 + h * .2, zf, .085, h * .6, .012); m.Box("mat_trim", -.29, y0 + h * .3, zf + .004, .055, h * .38, .012);   // Ethernet(RJ45)
                m.Box("mat_trim", -.205, y0 + h * .42, zf, .04, .02, .012);                                // USB-C 콘솔
                for (int i = 0; i < 3; i++) m.Box("mat_led", -.155, y0 + h * (.22 + i * .24), zf, .018, .018, .012);   // Power·Status·Error
                foreach (double cx in new[] { -.03, .15 })                                                 // M12 커넥터 Sensor A·B + 링크 LED
                {
                    Rod("mat_metal", cx, y0 + h / 2, zf, cx, y0 + h / 2, zf + .045, .05, .05, 14);
                    Disc("mat_trim", cx, y0 + h / 2, zf + .045, .03, .004, 10);
                    for (int i = 0; i < 2; i++) m.Box("mat_led", cx + .075, y0 + h * (.35 + i * .3), zf, .016, .016, .012);
                }
                m.Box("mat_trim", .31, y0 + h * .15, zf, .075, h * .7, .014); m.Box("mat_black", .31, y0 + h * .45, zf + .014, .05, h * .3, .02);   // 로커 스위치
                for (int i = 0; i < 5; i++) m.Box("mat_trim", w / 2 + .002, y0 + h * .25, -.14 + i * .065, .008, h * .5, .026);           // 옆면 환기 슬롯
                Box("mat_body", -.26, y0 + h, .12, .3, .006, .07);                                         // 상판 "SMART Controller" 명판 — 채우기 색
                lens = new(0, y0 + h / 2, zf + .05); break;
            }
            case "iocontroller":
            {
                // 제어기 S-IW-P104C (140 × 35.2 × 110 mm) — 접점 IN/OUT·RS-485 단자대·빨간 전원 스위치가 얼굴이다. 높이만 ×1.6.
                double w = Mm(140), h = Mm(35.2) * 1.6, d = Mm(110), y0 = .11, zf = d / 2 + .004;
                Base(w + .08, d + .08); m.BeveledBox("mat_black", 0, y0, 0, w, h, d, .015);
                m.Box("mat_metal", -.25, y0 + h * .25, zf, .06, .055, .014); m.Box("mat_trim", -.25, y0 + h * .3, zf + .004, .035, .03, .014);   // ⑤ 디버그 콘솔(USB-B)
                m.Box("#2E7D32", -.11, y0 + h * .18, zf, .15, h * .5, .03);                                   // ④ IN/OUT 접점 단자대(녹색)
                for (int i = 0; i < 6; i++) m.Box("mat_metal", -.17 + i * .024, y0 + h * .56, zf + .03, .012, .012, .006);   // 단자 나사
                m.Box("mat_metal", .04, y0 + h * .2, zf, .085, h * .6, .012); m.Box("mat_trim", .04, y0 + h * .3, zf + .004, .055, h * .38, .012);   // ③ Ethernet
                m.Box("#2E7D32", .15, y0 + h * .18, zf, .09, h * .5, .03);                                    // ② RS-485 UP/DOWN 단자대
                m.Box("#C62828", .265, y0 + h * .2, zf, .06, h * .6, .024);                                   // ① 빨간 전원 스위치
                for (int i = 0; i < 2; i++) m.Box("mat_led", -.315, y0 + h * (.3 + i * .3), zf, .018, .018, .012);   // ⑥ 상태·전원 LED
                Box("mat_body", -.14, y0 + h, .1, .26, .006, .07);                                          // 상판 명판 — 채우기 색
                lens = new(0, y0 + h / 2, zf + .03); break;
            }
            case "fence":
            {
                // 펜스센서(IRONWALL PIDS 브로셔 p.3) — MEMS 진동센서, 115 × 59 × 31 mm 검정 쐐기형 케이스를 철책 가로대에 물린다.
                // 센서만 그리면 30 px 에서 검은 점이라 철책 한 칸을 무대로 깔고 센서를 ×1.7 로 키웠다.
                const double px = .46, top = .95, rail = .78, bottom = .12;
                for (int s = -1; s <= 1; s += 2) { m.Box("mat_trim", s * px, 0, 0, .14, .05, .18); Rod("mat_metal", s * px, .05, 0, s * px, top, 0, .035, .035, 10); }
                Rod("mat_metal", -px, rail, 0, px, rail, 0, .02, .02, 8); Rod("mat_metal", -px, bottom, 0, px, bottom, 0, .02, .02, 8);
                m.Box("mat_mesh", 0, bottom, 0, px * 2 - .04, rail - bottom, .006);                         // 반투명 철망 면
                // 마름모 철망: 45° 두 방향 철사를 가로대 사이에서 잘라 그린다.
                double meshH = rail - bottom, lo = -px + .03, hi = px - .03;
                for (double x0 = -px - meshH; x0 <= px; x0 += .16)
                    foreach (int dir in new[] { 1, -1 })
                    {
                        // x(t) = start + dir·meshH·t, y(t) = bottom + meshH·t (t∈[0,1]) 를 기둥 사이 [lo, hi] 로 자른다.
                        double start = dir > 0 ? x0 : -x0, ta = (lo - start) / (dir * meshH), tb = (hi - start) / (dir * meshH);
                        double t0 = Math.Max(0, Math.Min(ta, tb)), t1 = Math.Min(1, Math.Max(ta, tb));
                        if (t1 - t0 < .03) continue;
                        m.Tube("mat_metal", new(start + dir * meshH * t0, bottom + meshH * t0, .006),
                                            new(start + dir * meshH * t1, bottom + meshH * t1, .006), .006, .006, 4, false);
                    }
                // 센서 본체 — 쐐기(윗면이 앞으로 기운) 검정 케이스, 가로대 앞면에 물림.
                // 크기는 철망 한 칸 대비로 잡는다(실물 비율이면 3 m 칸에 115 mm 라 보이지 않는다) — 폭 .34 에 브로셔 가로·세로·두께 비를 유지.
                double sw = .34, sh = sw * 59 / 115, sd = sw * 31 / 115 * 1.4, sy = rail - sh * .35, sz = .02;
                m.Prism("mat_black", -sw / 2, sw / 2, new (double, double)[] { (sy, sz), (sy, sz + sd), (sy + sh * .62, sz + sd), (sy + sh, sz + sd * .45), (sy + sh, sz) });
                for (int s = -1; s <= 1; s += 2) { m.Box("mat_black", s * (sw / 2 + .03), sy + .01, sz, .06, .05, .03); Disc("mat_metal", s * (sw / 2 + .03), sy + .035, sz + .03, .012, .006, 8); }   // 고정 귀·나사
                Rod("mat_trim", -sw / 2, sy + sh * .4, sz + sd / 2, -sw / 2 - .07, sy + sh * .4, sz + sd / 2, .026, .026, 10);   // 케이블 글랜드
                Rod("mat_trim", -sw / 2 - .07, sy + sh * .4, sz + sd / 2, -px + .03, sy - .15, .03, .011, .011, 6);        // 케이블 → 기둥
                Box("mat_body", 0, sy + sh * .12, sz + sd + .002, sw * .45, .03, .008);                      // 명판 — 채우기 색
                lens = new(0, sy + sh / 2, sz + sd + .01); break;
            }
            case "lamp":
                Pole(1.12); Box("mat_body", 0, 1.02, .015, .56, .18, .3);
                Box("mat_trim", 0, 1.01, .18, .5, .16, .03);
                for (int i = 0; i < 4; i++) Box("mat_led", -.18 + i * .12, 1.045, .2, .08, .085, .02); break;
            case "radar":
                Pole(.5); Tube("mat_metal", 0, .48, -.1, 0, .65, 0, .065, .065);
                Box("mat_body", 0, .53, 0, .69, .56, .17); Box("mat_glass", 0, .565, .094, .59, .49, .025);
                Box("mat_led", .24, .59, .113, .05, .03, .013); break;
            case "underground":
            {
                // 지진동센서(IRONWALL PIDS 브로셔 p.3·p.4) — Ø40→Ø60 × 77 mm 원뿔대를 땅에 묻고 말뚝으로 표시한다.
                // 묻힌 장비는 위에서 보이지 않으므로 땅을 한 귀퉁이 잘라낸 단면도로 그린다.
                // 잘라낸 칸은 −Z 쪽 — 방위 0°(기본값)에서 카메라가 −Z 에서 보므로 설치 직후 바로 단면이 보인다.
                const double gw = .9, gd = .7, soil = .46, grass = .035;
                m.Box("mat_soil", 0, 0, gd / 4, gw, soil, gd / 2);                   // 뒤쪽 절반(+Z)
                m.Box("mat_soil", -gw / 4, 0, -gd / 4, gw / 2, soil, gd / 2);        // 앞 한쪽
                m.Box("mat_soil", gw / 4, 0, -gd / 4, gw / 2, .05, gd / 2);          // 잘라낸 칸의 바닥
                m.Box("mat_grass", 0, soil, gd / 4, gw, grass, gd / 2);
                m.Box("mat_grass", -gw / 4, soil, -gd / 4, gw / 2, grass, gd / 2);
                foreach (double y in new[] { .15, .3 })                             // 단면에 드러난 지층 선
                {
                    m.Box("#57422C", gw / 4, y, -.002, gw / 2, .014, .006);
                    m.Box("#57422C", .002, y, -gd / 4, .006, .014, gd / 2);
                }
                // 원뿔대 Ø40→Ø60 × 77 mm 를 실물 비율(200 mm = 1)로 — 단면 모서리에 살짝 박혀 "땅속" 으로 읽힌다.
                double rb = Mm(40) / 2, rt = Mm(60) / 2, hc = Mm(77), cy0 = .05;
                const double cx = .13, cz = -.13;
                Rod("mat_black", cx, cy0, cz, cx, cy0 + hc, cz, rb, rt, 16);
                Rod("mat_trim", cx, cy0 + hc, cz, cx, cy0 + hc + .02, cz, rt, rt, 16);
                Rod("mat_trim", cx, cy0 + .06, cz, cx, cy0 + .06, .06, .016, .016, 6);        // 케이블 → 땅속
                Rod("mat_black", cx, cy0 + hc + .02, cz, cx, .84, cz, .04, .04, 10);          // 지표 표시 말뚝
                m.Tube("mat_black", new(cx, .82, cz), new(cx, .93, cz), .085, .085, 6);       // 육각 머리
                Rod("mat_body", cx, .855, cz, cx, .885, cz, .089, .089, 6);                   // 머리 띠 — 채우기 색
                m.Box("mat_led", cx, .93, cz, .035, .014, .035);
                lens = new(cx, .93, cz); break;
            }
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
            case "smartmulti":
            {
                // 스마트 복합센서 II S-IW-P104SM2 (109.5 × 183.5 × 81 mm, 올리브) — 이미지·레이다·적외선·진동 4종 융합.
                // 얼굴: 위쪽 사선 차양 안의 검은 창(카메라 + IR LED 4) · 가운데 흰 PIR 창 · 아래 케이블 글랜드 2.
                double w = Mm(109.5), h = Mm(183.5), d = Mm(81), y0 = .3, zf = d / 2, top = y0 + h, hood = d * .55;   // 차양은 본체 깊이의 약 절반 돌출(브로셔 측면)
                FencePost(y0 + h * .85, -d / 2 - .1, y0 + h * .55);
                m.BeveledBox("mat_olive", 0, y0, 0, w, h, d, .03);
                for (int s = -1; s <= 1; s += 2)
                {
                    foreach (double k in new[] { .25, .5, .75 }) m.Box("mat_olive", s * (w / 2 + .01), y0 + h * k, 0, .02, .025, d * .85);   // 옆면 보강 리브
                    foreach (double k in new[] { .16, .62 }) m.Box("mat_olive", s * (w / 2 + .035), y0 + h * k, -d * .25, .05, .07, .07);     // 고정 귀
                    // 사선 차양 옆벽: 위는 앞으로 .15 나오고 아래로 갈수록 본체 쪽으로 비스듬히 들어간다(브로셔 측면 사진).
                    double x0 = s * (w / 2 - .004), x1 = s * (w / 2 + .018);
                    m.Prism("mat_olive", Math.Min(x0, x1), Math.Max(x0, x1), new (double, double)[] { (top, zf - .02), (top, zf + hood), (y0 + h * .5, zf + hood), (y0 + h * .27, zf - .02) });
                }
                m.Box("mat_olive", 0, top - .004, zf + (hood - .02) / 2, w + .036, .028, hood + .02);   // 차양 지붕
                m.Box("mat_olive", 0, y0 + h * .26, zf + .01, w + .036, .026, .05);       // 차양 아래 턱
                m.Box("mat_glass", 0, y0 + h * .66, zf, w * .72, h * .21, .014);          // 검은 창
                Rod("mat_metal", 0, y0 + h * .765, zf + .01, 0, y0 + h * .765, zf + .03, .052, .052, 14);   // 카메라 렌즈 링
                Disc("mat_glass", 0, y0 + h * .765, zf + .03, .036, .012, 14);
                foreach (double lx in new[] { -.13, .13 }) foreach (double ly in new[] { -.045, .045 })
                    Bulb("mat_white", lx, y0 + h * .765 + ly, zf + .014, .026, new Vector3D(0, 0, 1));   // IR LED 4
                Rod("mat_trim", 0, y0 + h * .47, zf, 0, y0 + h * .47, zf + .012, .1, .1, 16);             // PIR 창 테
                Disc("mat_white", 0, y0 + h * .47, zf + .012, .083, .01, 16);                             // PIR 창
                Box("mat_body", 0, y0 + h * .07, zf + .002, w * .55, .045, .012);                        // "IRONWALL" 명판 — 채우기 색
                foreach (double gx in new[] { -.12, .12 })                                                // 케이블 글랜드
                { Rod("mat_trim", gx, y0, .02, gx, y0 - .075, .02, .036, .036, 10); Rod("mat_trim", gx, y0 - .02, .02, gx, y0 - .04, .02, .045, .045, 6); }
                lens = new(0, y0 + h * .765, zf + .045); break;
            }
            case "sensor":
            {
                // 스마트 센서 S-IW-P104MVP (97 × 66 × 104 mm, 검정 PC/ABS) — 둥근 케이스 · 위 가운데 흰 사각 창 · 아래 큰 렌즈 둘(검은 돔 + 동심원).
                double w = Mm(97), h = Mm(104), d = Mm(66), y0 = .36, zf = d / 2;
                FencePost(y0 + h * .7, -d / 2 - .09, y0 + h * .4);
                m.BeveledBox("mat_black", 0, y0, 0, w, h, d, .07);
                m.BeveledBox("mat_black", 0, y0 + h, 0, w - .06, .03, d - .06, .05);                      // 둥근 윗모서리
                m.BeveledBox("mat_black", 0, y0 + h * .58, zf - .02, w * .44, h * .3, .07, .03);          // 위쪽 돌출 보스
                m.Box("mat_white", 0, y0 + h * .64, zf + .05, w * .2, h * .16, .008);                      // 흰 사각 창
                Rod("mat_trim", -.115, y0 + h * .27, zf - .005, -.115, y0 + h * .27, zf + .015, .095, .095, 16);
                Bulb("mat_glass", -.115, y0 + h * .27, zf + .015, .072, new Vector3D(0, 0, 1));          // 검은 돔 렌즈
                Rod("mat_trim", .115, y0 + h * .27, zf - .005, .115, y0 + h * .27, zf + .015, .095, .095, 16);
                Rod("mat_metal", .115, y0 + h * .27, zf + .015, .115, y0 + h * .27, zf + .03, .07, .07, 16);   // 동심원 렌즈
                Rod("mat_trim", .115, y0 + h * .27, zf + .03, .115, y0 + h * .27, zf + .04, .038, .038, 12);
                for (int s = -1; s <= 1; s += 2) m.Box("mat_trim", s * (w / 2 + .012), y0 + h * .15, -d * .12, .024, h * .62, d * .5);   // 옆 고정 클립
                Box("mat_body", 0, y0 + .025, zf + .004, w * .5, .03, .012);                              // 명판 띠 — 채우기 색
                lens = new(0, y0 + h * .64, zf + .06); break;
            }
            case "multi":
            {
                // IRONWALL PIDS 복합센서(열감지, 91 × 108 × 70 mm, 캡 포함 90) — 검정 상자 · 위쪽 캡(차양) 안 흰 PIR 돔 · 아래 작은 렌즈 둘 · 볼 헤드 마운트.
                double w = Mm(91), h = Mm(108), d = Mm(70), y0 = .42, zf = d / 2, cap = Mm(20);
                Base(.42, .42); Rod("mat_metal", 0, .1, 0, 0, .22, 0, .05, .05);
                Rod("mat_black", 0, .22, 0, 0, y0 - .11, 0, .085, .075, 16);                               // 볼 헤드 몸통
                Rod("mat_metal", 0, .235, 0, 0, .26, 0, .09, .09, 16);                                     // 눈금 링
                Rod("mat_black", .07, .3, 0, .15, .3, 0, .028, .028, 10);                                  // 조임 손잡이
                Bulb("mat_metal", 0, y0 - .045, 0, .065, new Vector3D(0, -1, 0));                         // 볼
                m.Box("mat_trim", 0, y0 - .045, 0, .2, .045, .18);                                         // 마운트 판
                m.BeveledBox("mat_black", 0, y0, 0, w, h, d, .02);
                m.Box("mat_black", 0, y0 + h, cap / 2, w + .03, .025, d + cap);                            // 캡 지붕
                for (int s = -1; s <= 1; s += 2) m.Box("mat_black", s * (w / 2 + .004), y0 + h * .46, zf + cap / 2, .022, h * .54 + .025, cap);   // 캡 옆벽
                Rod("mat_trim", 0, y0 + h * .72, zf - .004, 0, y0 + h * .72, zf + .006, .12, .12, 16);
                Bulb("mat_white", 0, y0 + h * .72, zf + .006, .1, new Vector3D(0, 0, 1));                 // PIR 돔
                m.Box("mat_trim", 0, y0 + h * .1, zf, w * .8, h * .32, .012);                              // 아래 패널
                foreach (double lx in new[] { -.075, .075 })
                { Rod("mat_metal", lx, y0 + h * .3, zf + .012, lx, y0 + h * .3, zf + .026, .042, .042, 12); Disc("mat_glass", lx, y0 + h * .3, zf + .026, .03, .006, 12); }
                Box("mat_body", 0, y0 + h * .14, zf + .012, w * .38, .032, .008);                         // 라벨 띠 — 채우기 색
                lens = new(0, y0 + h * .72, zf + .1); break;
            }
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
