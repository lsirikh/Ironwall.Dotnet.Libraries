using System;
using System.Collections.Generic;

namespace GMap.NET.Projections
{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 8/11/2025 4:13:07 PM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    /// <summary>
    /// UTM Zone 52N Projection for Korea Region
    /// PROJCS["WGS 84 / UTM zone 52N",GEOGCS["WGS 84",DATUM["WGS_1984",SPHEROID["WGS 84",6378137,298.257223563,AUTHORITY["EPSG","7030"]],AUTHORITY["EPSG","6326"]],PRIMEM["Greenwich",0,AUTHORITY["EPSG","8901"]],UNIT["degree",0.01745329251994328,AUTHORITY["EPSG","9122"]],AUTHORITY["EPSG","4326"]],PROJECTION["Transverse_Mercator"],PARAMETER["latitude_of_origin",0],PARAMETER["central_meridian",129],PARAMETER["scale_factor",0.9996],PARAMETER["false_easting",500000],PARAMETER["false_northing",0],AUTHORITY["EPSG","32652"]]
    /// </summary>
    public class UTMZone52NProjection : PureProjection
    {
        public static readonly UTMZone52NProjection Instance = new UTMZone52NProjection();

        // 한국 지역 경계
        static readonly double MinLatitude = 33.0;   // 제주도 남단
        static readonly double MaxLatitude = 43.0;   // 함경도 북단  
        static readonly double MinLongitude = 124.0; // 서해 최서단
        static readonly double MaxLongitude = 132.0; // 동해 최동단

        // UTM Zone 52N 원점 (타일 좌표계용)
        static readonly double OrignX = -200000;     // 서쪽 여유분
        static readonly double OrignY = 10000000;    // 북쪽 여유분

        // UTM Zone 52N 파라미터
        static readonly double ScaleFactor = 0.9996;                        // UTM 축척 인수
        static readonly double CentralMeridian = DegreesToRadians(129.0);   // 중앙경선 129°E
        static readonly double LatOrigin = 0.0;                             // 원점 위도
        static readonly double FalseNorthing = 0.0;                         // Y축 가산값
        static readonly double FalseEasting = 500000.0;                     // X축 가산값

        // WGS84 타원체 파라미터
        static readonly double SemiMajor = 6378137.0;                       // 장반축
        static readonly double SemiMinor = 6356752.3141403561;              // 단반축
        static readonly double SemiMinor2 = 6356752.3142451793;             // 단반축2
        static readonly double MetersPerUnit = 1.0;                         // 미터/단위
        static readonly double COS_67P5 = 0.38268343236508977;              // cos(67.5°)
        static readonly double AD_C = 1.0026000;                            // Toms 지역 상수

        public override RectLatLng Bounds =>
            RectLatLng.FromLTRB(MinLongitude, MaxLatitude, MaxLongitude, MinLatitude);

        public override GSize TileSize => new GSize(256, 256);
        public override double Axis => 6378137;
        public override double Flattening => 1.0 / 298.257223563;

        public override GPoint FromLatLngToPixel(double lat, double lng, int zoom)
        {
            lat = Clip(lat, MinLatitude, MaxLatitude);
            lng = Clip(lng, MinLongitude, MaxLongitude);

            // WGS84 → UTM 변환 과정
            double[] wgs84 = new[] { lng, lat };
            double[] cartesian = WGS84ToCartesian(wgs84);           // 1. WGS84 → 직교좌표
            double[] wgs84_2 = CartesianToWGS84(cartesian);         // 2. 직교좌표 → WGS84 (정규화)
            double[] utm = WGS84ToUTM(wgs84_2);                     // 3. WGS84 → UTM

            double res = GetTileMatrixResolution(zoom);
            return UTMToPixel(utm, res);
        }

        public override PointLatLng FromPixelToLatLng(long x, long y, int zoom)
        {
            var ret = PointLatLng.Empty;

            double res = GetTileMatrixResolution(zoom);

            // 픽셀 → UTM 변환
            double[] utm = PixelToUTM(x, y, res);

            // UTM → WGS84 변환 과정 (역순)
            double[] wgs84_normalized = UTMToWGS84(utm);            // 1. UTM → WGS84
            double[] cartesian = WGS84ToCartesian(wgs84_normalized); // 2. WGS84 → 직교좌표
            double[] wgs84_final = CartesianToWGS84(cartesian);     // 3. 직교좌표 → WGS84 (최종)

            ret.Lat = Clip(wgs84_final[1], MinLatitude, MaxLatitude);
            ret.Lng = Clip(wgs84_final[0], MinLongitude, MaxLongitude);

            return ret;
        }

        /// <summary>
        /// WGS84 지리좌표 → 직교좌표 변환
        /// </summary>
        private double[] WGS84ToCartesian(double[] lonlat)
        {
            // 이심률 제곱: (a^2 - b^2)/a^2
            double es = 1.0 - SemiMinor2 * SemiMinor2 / (SemiMajor * SemiMajor);

            double lon = DegreesToRadians(lonlat[0]);
            double lat = DegreesToRadians(lonlat[1]);
            double h = lonlat.Length < 3 ? 0 : lonlat[2];

            double v = SemiMajor / Math.Sqrt(1 - es * Math.Pow(Math.Sin(lat), 2));
            double x = (v + h) * Math.Cos(lat) * Math.Cos(lon);
            double y = (v + h) * Math.Cos(lat) * Math.Sin(lon);
            double z = ((1 - es) * v + h) * Math.Sin(lat);

            return new[] { x, y, z };
        }

        /// <summary>
        /// 직교좌표 → WGS84 지리좌표 변환
        /// </summary>
        private double[] CartesianToWGS84(double[] pnt)
        {
            // 이심률 제곱: (a^2 - b^2)/a^2
            double es = 1.0 - SemiMinor * SemiMinor / (SemiMajor * SemiMajor);

            // 제2 이심률 제곱: (a^2 - b^2)/b^2
            double ses = (Math.Pow(SemiMajor, 2) - Math.Pow(SemiMinor, 2)) / Math.Pow(SemiMinor, 2);

            bool atPole = false;
            double Z = pnt.Length < 3 ? 0 : pnt[2];

            double lon;
            double lat = 0;
            double height;

            if (pnt[0] != 0.0)
            {
                lon = Math.Atan2(pnt[1], pnt[0]);
            }
            else
            {
                if (pnt[1] > 0)
                    lon = Math.PI / 2;
                else if (pnt[1] < 0)
                    lon = -Math.PI * 0.5;
                else
                {
                    atPole = true;
                    lon = 0.0;
                    if (Z > 0.0) // 북극
                        lat = Math.PI * 0.5;
                    else if (Z < 0.0) // 남극
                        lat = -Math.PI * 0.5;
                    else // 지구 중심
                        return new[] { RadiansToDegrees(lon), RadiansToDegrees(Math.PI * 0.5), -SemiMinor };
                }
            }

            double W2 = pnt[0] * pnt[0] + pnt[1] * pnt[1]; // Z축으로부터 거리의 제곱
            double W = Math.Sqrt(W2); // Z축으로부터 거리
            double T0 = Z * AD_C; // 수직 성분 초기 추정
            double S0 = Math.Sqrt(T0 * T0 + W2); // 수평 성분 초기 추정
            double Sin_B0 = T0 / S0; // sin(B0), B0는 Bowring 보조 변수 추정
            double Cos_B0 = W / S0; // cos(B0)
            double Sin3_B0 = Math.Pow(Sin_B0, 3);
            double T1 = Z + SemiMinor * ses * Sin3_B0; // 수직 성분 보정 추정
            double Sum = W - SemiMajor * es * Cos_B0 * Cos_B0 * Cos_B0; // cos(phi1)의 분자
            double S1 = Math.Sqrt(T1 * T1 + Sum * Sum); // 수평 성분 보정 추정
            double Sin_p1 = T1 / S1; // sin(phi1), phi1은 추정 위도
            double Cos_p1 = Sum / S1; // cos(phi1)
            double Rn = SemiMajor / Math.Sqrt(1.0 - es * Sin_p1 * Sin_p1); // 해당 위치에서의 지구 반지름

            if (Cos_p1 >= COS_67P5)
                height = W / Cos_p1 - Rn;
            else if (Cos_p1 <= -COS_67P5)
                height = W / -Cos_p1 - Rn;
            else
                height = Z / Sin_p1 + Rn * (es - 1.0);

            if (!atPole)
                lat = Math.Atan(Sin_p1 / Cos_p1);

            return new[] { RadiansToDegrees(lon), RadiansToDegrees(lat), height };
        }

        /// <summary>
        /// WGS84 → UTM Zone 52N 변환
        /// </summary>
        private double[] WGS84ToUTM(double[] lonlat)
        {
            double e0, e1, e2, e3; // 이심률 상수
            double e, es, esp;     // 이심률 상수
            double ml0;            // 작은 값 m

            es = 1.0 - Math.Pow(SemiMinor / SemiMajor, 2);
            e = Math.Sqrt(es);
            e0 = E0Fn(es);
            e1 = E1Fn(es);
            e2 = E2Fn(es);
            e3 = E3Fn(es);
            ml0 = SemiMajor * Mlfn(e0, e1, e2, e3, LatOrigin);
            esp = es / (1.0 - es);

            double lon = DegreesToRadians(lonlat[0]);
            double lat = DegreesToRadians(lonlat[1]);

            double delta_lon; // 경도 차이 (주어진 경도 - 중심경도)
            double sin_phi, cos_phi; // sin 및 cos 값
            double al, als; // 임시 값
            double c, t, tq; // 임시 값
            double con, n, ml; // 원뿔 상수, 작은 m

            delta_lon = AdjustLongitude(lon - CentralMeridian);
            SinCos(lat, out sin_phi, out cos_phi);

            al = cos_phi * delta_lon;
            als = Math.Pow(al, 2);
            c = esp * Math.Pow(cos_phi, 2);
            tq = Math.Tan(lat);
            t = Math.Pow(tq, 2);
            con = 1.0 - es * Math.Pow(sin_phi, 2);
            n = SemiMajor / Math.Sqrt(con);
            ml = SemiMajor * Mlfn(e0, e1, e2, e3, lat);

            double x = ScaleFactor * n * al * (1.0 + als / 6.0 * (1.0 - t + c + als / 20.0 *
                                                                  (5.0 - 18.0 * t + Math.Pow(t, 2) + 72.0 * c -
                                                                   58.0 * esp))) + FalseEasting;

            double y = ScaleFactor * (ml - ml0 + n * tq * (als * (0.5 + als / 24.0 *
                                                                  (5.0 - t + 9.0 * c + 4.0 * Math.Pow(c, 2) + als /
                                                                   30.0 * (61.0 - 58.0 * t
                                                                           + Math.Pow(t, 2) + 600.0 * c -
                                                                           330.0 * esp))))) + FalseNorthing;

            return new[] { x / MetersPerUnit, y / MetersPerUnit };
        }

        /// <summary>
        /// UTM → WGS84 변환
        /// </summary>
        private double[] UTMToWGS84(double[] p)
        {
            double e0, e1, e2, e3; // 이심률 상수
            double e, es, esp;     // 이심률 상수
            double ml0;            // 작은 값 m

            es = 1.0 - Math.Pow(SemiMinor / SemiMajor, 2);
            e = Math.Sqrt(es);
            e0 = E0Fn(es);
            e1 = E1Fn(es);
            e2 = E2Fn(es);
            e3 = E3Fn(es);
            ml0 = SemiMajor * Mlfn(e0, e1, e2, e3, LatOrigin);
            esp = es / (1.0 - es);

            double con, phi;
            double delta_phi;
            long i;
            double sin_phi, cos_phi, tan_phi;
            double c, cs, t, ts, n, r, d, ds;
            long max_iter = 6;

            double x = p[0] * MetersPerUnit - FalseEasting;
            double y = p[1] * MetersPerUnit - FalseNorthing;

            con = (ml0 + y / ScaleFactor) / SemiMajor;
            phi = con;
            for (i = 0; ; i++)
            {
                delta_phi = (con + e1 * Math.Sin(2.0 * phi) - e2 * Math.Sin(4.0 * phi) + e3 * Math.Sin(6.0 * phi)) / e0 - phi;
                phi += delta_phi;

                if (Math.Abs(delta_phi) <= Epsilon)
                    break;

                if (i >= max_iter)
                    throw new ArgumentException("위도 수렴 실패");
            }

            if (Math.Abs(phi) < HalfPi)
            {
                SinCos(phi, out sin_phi, out cos_phi);
                tan_phi = Math.Tan(phi);
                c = esp * Math.Pow(cos_phi, 2);
                cs = Math.Pow(c, 2);
                t = Math.Pow(tan_phi, 2);
                ts = Math.Pow(t, 2);
                con = 1.0 - es * Math.Pow(sin_phi, 2);
                n = SemiMajor / Math.Sqrt(con);
                r = n * (1.0 - es) / con;
                d = x / (n * ScaleFactor);
                ds = Math.Pow(d, 2);

                double lat = phi - n * tan_phi * ds / r * (0.5 - ds / 24.0 * (5.0 + 3.0 * t +
                                                                              10.0 * c - 4.0 * cs - 9.0 * esp - ds /
                                                                              30.0 * (61.0 + 90.0 * t +
                                                                                      298.0 * c + 45.0 * ts -
                                                                                      252.0 * esp - 3.0 * cs)));

                double lon = AdjustLongitude(CentralMeridian + d * (1.0 - ds / 6.0 * (1.0 + 2.0 * t +
                                                                                      c - ds / 20.0 *
                                                                                      (5.0 - 2.0 * c + 28.0 * t -
                                                                                       3.0 * cs + 8.0 * esp +
                                                                                       24.0 * ts))) / cos_phi);

                return new[] { RadiansToDegrees(lon), RadiansToDegrees(lat) };
            }
            else
            {
                return new[] { RadiansToDegrees(HalfPi * Sign(y)), RadiansToDegrees(CentralMeridian) };
            }
        }

        /// <summary>
        /// UTM 좌표 → 픽셀 좌표 변환
        /// </summary>
        private GPoint UTMToPixel(double[] utm, double res)
        {
            return new GPoint(
                (long)Math.Floor((utm[0] - OrignX) / res),
                (long)Math.Floor((OrignY - utm[1]) / res)
            );
        }

        /// <summary>
        /// 픽셀 좌표 → UTM 좌표 변환
        /// </summary>
        private double[] PixelToUTM(long x, long y, double res)
        {
            return new[] { x * res + OrignX, OrignY - y * res };
        }

        /// <summary>
        /// 줌 레벨별 해상도 (미터/픽셀)
        /// </summary>
        private double GetTileMatrixResolution(int zoom)
        {
            // UTM 좌표계에서 줌 레벨별 해상도 정의
            // 기본적으로 2^n 방식으로 계산
            double baseResolution = 4096.0; // 줌 레벨 0에서의 기본 해상도 (미터/픽셀)
            return baseResolution / Math.Pow(2, zoom);
        }

        /// <summary>
        /// UTM Grid North 기준 방위각 계산
        /// </summary>
        public double GetGridBearing(PointLatLng p1, PointLatLng p2)
        {
            var utm1 = WGS84ToUTM(new[] { p1.Lng, p1.Lat });
            var utm2 = WGS84ToUTM(new[] { p2.Lng, p2.Lat });

            double deltaE = utm2[0] - utm1[0]; // Easting 차이
            double deltaN = utm2[1] - utm1[1]; // Northing 차이

            // Grid North 기준 방위각 (0도 = 북쪽, 시계방향)
            double gridBearing = Math.Atan2(deltaE, deltaN) * 180.0 / Math.PI;
            return (gridBearing + 360) % 360;
        }

        /// <summary>
        /// 수렴각 계산 (True North → Grid North)
        /// </summary>
        public double GetConvergenceAngle(PointLatLng position)
        {
            double deltaLng = position.Lng - 129.0; // 중앙경선(129°E)과의 차이
            double convergence = Math.Sin(position.Lat * Math.PI / 180.0) *
                               (deltaLng * Math.PI / 180.0);
            return convergence * 180.0 / Math.PI;
        }

        #region -- 타일 매트릭스 정보 --

        public override double GetGroundResolution(int zoom, double latitude)
        {
            return GetTileMatrixResolution(zoom);
        }

        Dictionary<int, GSize> _extentMatrixMin;
        Dictionary<int, GSize> _extentMatrixMax;

        public override GSize GetTileMatrixMinXY(int zoom)
        {
            if (_extentMatrixMin == null)
                GenerateExtents();
            return _extentMatrixMin[zoom];
        }

        public override GSize GetTileMatrixMaxXY(int zoom)
        {
            if (_extentMatrixMax == null)
                GenerateExtents();
            return _extentMatrixMax[zoom];
        }

        void GenerateExtents()
        {
            _extentMatrixMin = new Dictionary<int, GSize>();
            _extentMatrixMax = new Dictionary<int, GSize>();

            for (int i = 0; i <= 18; i++) // 줌 레벨 0-18
            {
                _extentMatrixMin.Add(i, new GSize(FromPixelToTileXY(FromLatLngToPixel(Bounds.LocationTopLeft, i))));
                _extentMatrixMax.Add(i, new GSize(FromPixelToTileXY(FromLatLngToPixel(Bounds.LocationRightBottom, i))));
            }
        }

        #endregion
    }
}
