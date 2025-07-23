using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore;
using SkiaSharp;
using System;
using LiveChartsCore.Drawing;
using LiveChartsCore.Kernel;
using LiveChartsCore.SkiaSharpView.Drawing.Geometries;
using System.IO.Pipelines;
using LiveChartsCore.SkiaSharpView.Drawing;
using System.Windows.Media.Animation;

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
    }
}