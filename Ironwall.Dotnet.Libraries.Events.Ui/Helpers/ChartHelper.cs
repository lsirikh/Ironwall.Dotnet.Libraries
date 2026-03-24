using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore;
using SkiaSharp;
using System;
using LiveChartsCore.Drawing;
using LiveChartsCore.Kernel;
using LiveChartsCore.SkiaSharpView.Drawing.Geometries;
using LiveChartsCore.SkiaSharpView.Drawing;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media.Animation;
using Ironwall.Dotnet.Libraries.Messages.Dto.Events;

namespace Ironwall.Dotnet.Libraries.Events.Ui.Helpers{
    /****************************************************************************
       Purpose      :                                                          
       Created By   : GHLee                                                
       Created On   : 6/28/2025 5:52:40 PM                                                    
       Department   : SW Team                                                   
       Company      : Sensorway Co., Ltd.                                       
       Email        : lsirikh@naver.com                                         
    ****************************************************************************/
    public static class ChartHelper
    {
        /// <summary>
        /// StartDate~EndDate 구간의 시간 라벨 배열을 생성한다.
        /// 48시간 이하: 1시간 단위 ("yyyy-MM-dd HH"), 48시간 초과: 1일 단위 ("yyyy-MM-dd")
        /// </summary>
        public static string[] GenerateHourlyLabels(DateTime start, DateTime end)
        {
            var totalHours = (end - start).TotalHours;
            var labels = new List<string>();

            if (totalHours > 48)
            {
                var current = start.Date;
                var endDate = end.Date;
                while (current <= endDate)
                {
                    labels.Add(current.ToString("yyyy-MM-dd"));
                    current = current.AddDays(1);
                }
            }
            else
            {
                var current = new DateTime(start.Year, start.Month, start.Day, start.Hour, 0, 0);
                while (current <= end)
                {
                    labels.Add(current.ToString("yyyy-MM-dd HH"));
                    current = current.AddHours(1);
                }
            }

            return labels.ToArray();
        }

        /// <summary>
        /// 1개의 값 → Pie(ISeries) 로 변환
        /// </summary>
        public static ISeries MakePie(string name, double value, SKColor fill, SKColor stroke)
        {

            var pie = new PieSeries<double>
            {
                Name = name,
                Values = new[] { value },
                Fill = new SolidColorPaint(fill),
                Stroke = new SolidColorPaint(stroke),
                DataLabelsPosition = PolarLabelsPosition.Middle,
                DataLabelsSize = 13,
                DataLabelsPaint = new SolidColorPaint(new SKColor(255, 255, 255), 2),
            };
            pie.PointMeasured += Pie_PointMeasured;
            return pie;
        }

        private static void Pie_PointMeasured(ChartPoint<double, DoughnutGeometry, LabelGeometry> point)
        {
            var perPointDelay = 100; // in milliseconds
            var delay = point.Context.Entity.MetaData!.EntityIndex * perPointDelay;
            var speed = (float)point.Context.Chart.AnimationsSpeed.TotalMilliseconds + delay;

            // the animation takes a function, that represents the progress of the animation
            // the parameter is the progress of the animation, it goes from 0 to 1
            // the function must return a value from 0 to 1, where 0 is the initial state
            // and 1 is the end state

            point.Visual?.SetTransition(
                new Animation(progress =>
                {
                    var d = delay / speed;

                    return progress <= d
                        ? 0
                        : EasingFunctions.BuildCustomElasticOut(1.5f, 0.60f)((progress - d) / (1 - d));
                },
                TimeSpan.FromMilliseconds(speed)));
        }

        /// <summary>
        /// 1개의 값 집합(List) → Column(ISeries) 로 변환
        /// </summary>
        public static ISeries MakeBar(string name, List<double> values, SKColor color, SKColor stroke) 
        {
        
            var column = new ColumnSeries<double>
            {
                Name = name,
                Values = values,
                Fill = new SolidColorPaint(color),
                Stroke= new SolidColorPaint(stroke),
                DataLabelsSize = 12,
                DataLabelsPaint = new SolidColorPaint(new SKColor(255, 255, 255), 2),
                DataLabelsRotation = 90,
                DataLabelsPosition = DataLabelsPosition.Middle,
            };
            column.PointMeasured += Column_PointMeasured;
            return column;
        }

        private static void Column_PointMeasured(ChartPoint<double, RoundedRectangleGeometry, LabelGeometry> point)
        {
            var perPointDelay = 100; // in milliseconds
            var delay = point.Context.Entity.MetaData!.EntityIndex * perPointDelay;
            var speed = (float)point.Context.Chart.AnimationsSpeed.TotalMilliseconds + delay;

            point.Visual?.SetTransition(
                new Animation(progress =>
                {
                    var d = delay / speed;

                    return progress <= d
                        ? 0
                        : EasingFunctions.BuildCustomElasticOut(1.5f, 0.60f)((progress - d) / (1 - d));
                },
                TimeSpan.FromMilliseconds(speed)));
        }

        // ────────────────────────── Statistics API Chart Builders ──────────────────────────

        private static readonly (string Name, SKColor Color, Func<EventTrendItemDto, int> Selector)[] TrendCategories =
        {
            ("Sensor Detection", new SKColor(255, 205, 0),   item => item.SensorDetection),
            ("Camera Detection", new SKColor(255, 100, 100), item => item.CameraDetection),
            ("Malfunction",      new SKColor(30, 144, 255),  item => item.Malfunction),
            ("Connection",       new SKColor(155, 89, 182),  item => item.Connection),
            ("Action",           new SKColor(50, 205, 50),   item => item.Action)
        };

        /// <summary>
        /// EventTrendDto → LineSeries 5개 + xLabels 배열
        /// </summary>
        public static (List<ISeries> Series, string[] XLabels) BuildLineSeriesFromTrend(
            EventTrendDto trend, DateTime startDate, DateTime endDate)
        {
            var series = new List<ISeries>();
            string[] xLabels;

            if (trend.Series == null || trend.Series.Count == 0)
            {
                xLabels = GenerateHourlyLabels(startDate, endDate);
                foreach (var cat in TrendCategories)
                {
                    series.Add(new LineSeries<int>
                    {
                        Name = cat.Name,
                        LineSmoothness = 0.5,
                        GeometrySize = 8,
                        Stroke = new SolidColorPaint(cat.Color, 3),
                        GeometryStroke = new SolidColorPaint(cat.Color, 3),
                        GeometryFill = new SolidColorPaint(Darken(cat.Color, 0.5f)),
                        Fill = null,
                        Values = new int[xLabels.Length]
                    });
                }
                return (series, xLabels);
            }

            xLabels = trend.Series.Select(s => s.TimeBucket).ToArray();

            foreach (var cat in TrendCategories)
            {
                var values = trend.Series.Select(cat.Selector).ToArray();
                series.Add(new LineSeries<int>
                {
                    Name = cat.Name,
                    LineSmoothness = 0.5,
                    GeometrySize = 8,
                    Stroke = new SolidColorPaint(cat.Color, 3),
                    GeometryStroke = new SolidColorPaint(cat.Color, 3),
                    GeometryFill = new SolidColorPaint(Darken(cat.Color, 0.5f)),
                    Fill = null,
                    Values = values
                });
            }

            return (series, xLabels);
        }

        /// <summary>
        /// EventByDeviceDto → ColumnSeries (막대 그래프) + xLabels (장치명)
        /// eventTypes 필터: "DET" → sensor_detection, "MAL" → malfunction, "CON" → connection, "ACT" → action
        /// </summary>
        public static (List<ISeries> Series, string[] XLabels) BuildBarSeriesFromByDevice(
            EventByDeviceDto byDevice, string[] eventTypes)
        {
            var series = new List<ISeries>();

            // Controller 기반 (센서/장애/연결)
            var controllers = byDevice.Controllers ?? new List<ControllerStatsDto>();
            var controllerLabels = controllers.Select(c => c.ControllerName).ToArray();

            if (controllerLabels.Length == 0)
                controllerLabels = new[] { "No Data" };

            if (eventTypes.Contains("DET"))
            {
                var values = controllers.Select(c => (double)c.SensorDetection).ToList();
                if (values.Count == 0) values.Add(0);
                series.Add(MakeBar("Detection", values, new SKColor(255, 205, 0), SKColors.White));
            }

            if (eventTypes.Contains("MAL"))
            {
                var values = controllers.Select(c => (double)c.Malfunction).ToList();
                if (values.Count == 0) values.Add(0);
                series.Add(MakeBar("Malfunction", values, new SKColor(30, 144, 255), SKColors.White));
            }

            if (eventTypes.Contains("CON"))
            {
                var values = controllers.Select(c => (double)c.Connection).ToList();
                if (values.Count == 0) values.Add(0);
                series.Add(MakeBar("Connection", values, new SKColor(155, 89, 182), SKColors.White));
            }

            if (eventTypes.Contains("ACT"))
            {
                var values = controllers.Select(c => (double)c.Action).ToList();
                if (values.Count == 0) values.Add(0);
                series.Add(MakeBar("Action", values, new SKColor(50, 205, 50), SKColors.White));
            }

            return (series, controllerLabels);
        }

        /// <summary>
        /// EventSummaryDto → PieSeries (원형 그래프)
        /// eventTypes 필터: "DET" → sensor+camera, "MAL", "CON", "ACT"
        /// </summary>
        public static List<ISeries> BuildPieSeriesFromSummary(EventSummaryDto summary, string[] eventTypes)
        {
            var series = new List<ISeries>();

            if (eventTypes.Contains("DET"))
            {
                series.Add(MakePie("Detection",
                    summary.SensorDetection + summary.CameraDetection,
                    new SKColor(255, 205, 0), SKColors.White));
            }

            if (eventTypes.Contains("MAL"))
            {
                series.Add(MakePie("Malfunction", summary.Malfunction,
                    new SKColor(30, 144, 255), SKColors.White));
            }

            if (eventTypes.Contains("CON"))
            {
                series.Add(MakePie("Connection", summary.Connection,
                    new SKColor(155, 89, 182), SKColors.White));
            }

            if (eventTypes.Contains("ACT"))
            {
                series.Add(MakePie("Action", summary.Action,
                    new SKColor(50, 205, 50), SKColors.White));
            }

            return series;
        }

        private static SKColor Darken(SKColor color, float factor)
        {
            return new SKColor(
                (byte)(color.Red * factor),
                (byte)(color.Green * factor),
                (byte)(color.Blue * factor),
                color.Alpha);
        }
    }
}