using Caliburn.Micro;
using GMap.NET.WindowsPresentation;
using Ironwall.Dotnet.Libraries.GMaps.Ui.GMapSymbols;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Ironwall.Dotnet.Libraries.GMaps.Ui.GMapCustoms;

public class GMapCustomControl : GMapControl
{
    #region - Ctors -
    public GMapCustomControl()
    {
        _eventAggregator = IoC.Get<IEventAggregator>();
        Markers.CollectionChanged += Markers_CollectionChanged;
        CustomMarkers = new ObservableCollection<GMapCustomMarker>();
        OnAreaChange += GMapCustomControl_OnAreaChange;
    }

    public GMapCustomControl(IEventAggregator eventAggregator)
    {
        _eventAggregator = eventAggregator;
        Markers.CollectionChanged += Markers_CollectionChanged;
        CustomMarkers = new ObservableCollection<GMapCustomMarker>();
    }

    private void GMapCustomControl_OnAreaChange(GMap.NET.RectLatLng selection, double zoom, bool zoomToFit)
    {
        Debug.WriteLine($"Selection Changed: {selection}, Zoom: {zoom}, ZoomToFit: {zoomToFit}");
        Markers.OfType<GMapCustomMarker>().ToList().ForEach(entity =>
        {
            if (Zoom <= VISIBILITY_ZOOM)
                entity.Visibility = false;
            else
                entity.Visibility = true;

        });
    }
    #endregion
    #region - Implementation of Interface -
    #endregion
    #region - Overrides -
    protected override void OnInitialized(EventArgs e)
    {
        _eventAggregator.SubscribeOnUIThread(this);
        base.OnInitialized(e);
    }

    public override void RegenerateShape(IShapable s)
    {
        base.RegenerateShape(s);

        //var marker = s as GMapCustomPath;

        //if (s.Points != null && s.Points.Count > 1)
        //{
        //    marker.Position = s.Points[0];
        //    var localPath = new List<Point>(s.Points.Count);
        //    var offset = FromLatLngToLocal(s.Points[0]);

        //    foreach (var i in s.Points)
        //    {
        //        var p = FromLatLngToLocal(i);
        //        localPath.Add(new Point(p.X - offset.X, p.Y - offset.Y));
        //    }


        //    // Create the GMapPathCustomControl using CreatePath
        //    var shape = (GMapPathCustomControl)marker.CreatePath(localPath, true);

        //    // Set marker.Shape to the new GMapPathCustomControl
        //    marker.Shape = shape;
        //    if (Zoom <= VISIBILITY_ZOOM)
        //        marker.Visibility = false;
        //    else
        //        marker.Visibility = true;

        //}
        //else
        //{
        //    marker.Shape = null;
        //}
    }
    #endregion
    #region - Binding Methods -
    #endregion
    #region - Processes -
    private void Markers_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                // New items added
                foreach (var newItem in (e.NewItems ?? throw new NullReferenceException()).OfType<GMapCustomMarker>().ToList())
                {
                    CustomMarkers.Add(newItem);
                }

                break;

            case NotifyCollectionChangedAction.Remove:
                // Items removed
                foreach (var oldItem in (e.OldItems ?? throw new NullReferenceException()).OfType<GMapCustomMarker>().ToList())
                {
                    var entity = CustomMarkers.Where(entity => entity.Id == oldItem.Id).FirstOrDefault();
                    CustomMarkers.Remove(entity);
                }

                break;

            case NotifyCollectionChangedAction.Replace:
                // Some items replaced
                int index = 0;

                foreach (var oldItem in (e.OldItems ?? throw new NullReferenceException()).OfType<GMapCustomMarker>().ToList())
                {
                    var entity = CustomMarkers.Where(entity => entity.Id == oldItem.Id).FirstOrDefault();
                    index = CustomMarkers.IndexOf(entity);
                    CustomMarkers.Remove(entity);
                }

                foreach (var newItem in (e.NewItems ?? throw new NullReferenceException()).OfType<GMapCustomMarker>().ToList())
                {
                    CustomMarkers.Insert(index, newItem);
                }

                break;

            case NotifyCollectionChangedAction.Reset:
                // The whole list is refreshed
                CustomMarkers.Clear();
                foreach (var newItem in Markers.OfType<GMapCustomMarker>().ToList())
                {
                    CustomMarkers.Add(newItem);
                }

                break;
        }
    }
    #endregion
    #region - IHanldes -
    #endregion
    #region - Properties -

    public ObservableCollection<GMapCustomMarker> CustomMarkers { get; private set; }
    // TempPolyLineViewModel을 위한 속성 추가
    #endregion
    #region - Attributes -
    private IEventAggregator _eventAggregator;
    public int VISIBILITY_ZOOM = 14;
    #endregion
}
