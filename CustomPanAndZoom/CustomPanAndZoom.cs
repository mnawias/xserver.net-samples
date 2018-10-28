﻿using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Ptv.XServer.Controls.Map.Tools;
using Ptv.XServer.Controls.Map.Gadgets;
using Ptv.XServer.Controls.Map;


namespace CustomPanAndZoom
{
    /// <summary><para> Mode describing what is to be done when clicking in the map. </para>
    /// <para> See the <conceptualLink target="eb8e522c-5ed2-4481-820f-bfd74ee2aeb8"/> topic for an example. </para></summary>
    public enum DragMode
    {
        /// <summary> No special action. </summary>
        None,
        /// <summary> Clicking in the map starts a panning. If the mouse button is clicked and the mouse is moved, the
        /// map is panned. This means that the map is moved by the amount of the mouse move. </summary>
        Pan,
        /// <summary> Clicking in the map starts a zoom selection. If the mouse button is clicked and the mouse is
        /// moved, a rectangle is shown. When releasing the mouse, the map is zoomed to this rectangle. </summary>
        Select,
        /// <summary> Use Select if Shift is hold, otherwise pan. </summary>
        SelectOnShift
    }

    /// <summary><para> User control for the <see cref="MapView"/>-object, translating user interactions into modification
    /// of the visible map section. </para>
    /// <para> See the <conceptualLink target="eb8e522c-5ed2-4481-820f-bfd74ee2aeb8"/> topic for an example. </para></summary>
    public class PanAndZoom : MapGadget
    {
        // NEW: setting the mouse mode externally
        public DragMode MouseDragMode = DragMode.SelectOnShift;

        // NEW: zoom on Double click
        public bool ZoomOnDoubleClick = true;

        #region private variables
        /// <summary> Start point of the interaction in world coordinates. </summary>
        private Point WorldStartPoint = new Point(0, 0);
        /// <summary> Start point of the interaction in screen coordinates (pixel). </summary>
        private Point ScreenStartPoint = new Point(0, 0);
        /// <summary> Map on which the interaction is to be executed. </summary>
        private MapView mapView;
        /// <summary> Interaction mode defining whether a panning or a zoom selection is to be executed. </summary>
        private DragMode dragMode;
        /// <summary> Rectangle used for zoom selection. The map will be zoomed to this section. </summary>
        private Rectangle dragRectangle;
        /// <summary> Flag showing if the map has lately been panned. </summary>
        private bool wasPanned;
        #endregion

        #region public variables
        /// <summary> Gets or sets a value indicating whether a zooming or panning action is in progress. </summary>
        /// <value> Flag indicating whether a zooming or panning action is active. </value>
        public bool IsActive { get; set; }
        #endregion

        #region private methods
        /// <summary> Initializes the handlers for key and mouse events. </summary>
        private void Setup()
        {
            mapView.Focusable = true;
            mapView.KeyDown += map_KeyDown;
            mapView.MouseMove += control_MouseMove;
            mapView.MouseDown += source_MouseDown;
            mapView.MouseUp += source_MouseUp;
            mapView.MouseWheel += source_MouseWheel;
        }
        #endregion

        #region event handling
        /// <summary> Event handler for pressing a key. Scrolls or zooms the map depending on the pressed key. </summary>
        /// <param name="sender"> Sender of the KeyDown event. </param>
        /// <param name="e"> Event parameters. </param>
        private void map_KeyDown(object sender, KeyEventArgs e)
        {
            if (!mapView.IsFocused)
                return;

            const double panOffset = .25;

            MapRectangle rect = mapView.FinalEnvelope;
            double dX = rect.Width * panOffset;
            double dY = rect.Height * panOffset;

            switch (e.Key)
            {
                case Key.Up:
                case Key.W:
                case Key.NumPad8:
                    mapView.SetXYZ(mapView.FinalX, mapView.FinalY + dY, mapView.FinalZoom, Map.UseAnimation);
                    e.Handled = true;
                    break;
                case Key.Right:
                case Key.D:
                case Key.NumPad6:
                    mapView.SetXYZ(mapView.FinalX + dX, mapView.FinalY, mapView.FinalZoom, Map.UseAnimation);
                    e.Handled = true;
                    break;
                case Key.Down:
                case Key.S:
                case Key.NumPad2:
                    mapView.SetXYZ(mapView.FinalX, mapView.FinalY - dY, mapView.FinalZoom, Map.UseAnimation);
                    e.Handled = true;
                    break;
                case Key.Left:
                case Key.A:
                case Key.NumPad4:
                    mapView.SetXYZ(mapView.FinalX - dX, mapView.FinalY, mapView.FinalZoom, Map.UseAnimation);
                    e.Handled = true;
                    break;
                case Key.Add:
                case Key.OemPlus:
                    if (mapView.FinalZoom < mapView.MaxZoom)
                        mapView.SetZoom(mapView.FinalZoom + 1, Map.UseAnimation);

                    e.Handled = true;
                    break;
                case Key.OemMinus:
                case Key.Subtract:
                    if (mapView.FinalZoom > mapView.MinZoom)
                        mapView.SetZoom(mapView.FinalZoom - 1, Map.UseAnimation);

                    e.Handled = true;
                    break;
            }
        }

        /// <summary> Event handler for scrolling the mouse wheel. Zooms in or out the map depending on the scroll
        /// direction of the mouse wheel. </summary>
        /// <param name="sender"> Sender of the MouseWheel event. </param>
        /// <param name="e"> Event parameters. </param>
        private void source_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            e.Handled = true;

            double oldZoom = mapView.FinalZoom;

            double delta = e.Delta * Map.MouseWheelSpeed / 120;
            if (Map.InvertMouseWheel)
                delta = -delta;

            double newZoom = oldZoom + delta;

            Point p = mapView.CanvasToPtvMercator(mapView.GeoCanvas, e.GetPosition(mapView.GeoCanvas));

            mapView.ZoomAround(p, newZoom, Map.UseAnimation);
        }

        /// <summary> Event handler for releasing the mouse button. The map is zoomed to the selected section. </summary>
        /// <param name="sender"> Sender of the MouseUp event. </param>
        /// <param name="e"> Event parameters. </param>
        private void source_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!IsActive)
                return;

            if (mapView.IsMouseCaptured)
            {
                // we're done.  reset the cursor and release the mouse pointer
                mapView.Cursor = Cursors.Arrow;
                mapView.ReleaseMouseCapture();
            }

            if (dragMode == DragMode.Select)
            {
                mapView.ForePaneCanvas.Children.Remove(dragRectangle);
                dragRectangle = null;

                dragMode = DragMode.None;

                var physicalPoint = e.GetPosition(mapView);

                double minx = physicalPoint.X < ScreenStartPoint.X ? physicalPoint.X : ScreenStartPoint.X;
                double miny = physicalPoint.Y < ScreenStartPoint.Y ? physicalPoint.Y : ScreenStartPoint.Y;
                double maxx = physicalPoint.X > ScreenStartPoint.X ? physicalPoint.X : ScreenStartPoint.X;
                double maxy = physicalPoint.Y > ScreenStartPoint.Y ? physicalPoint.Y : ScreenStartPoint.Y;

                if (Math.Abs(maxx - minx) < 32 && Math.Abs(maxy - miny) < 32)
                    return;

                Point p1 = mapView.TranslatePoint(new Point(minx, miny), mapView.GeoCanvas);
                Point p2 = mapView.TranslatePoint(new Point(maxx, maxy), mapView.GeoCanvas);


                mapView.SetEnvelope(new MapRectangle(
                    p1.X / MapView.ZoomAdjust * MapView.LogicalSize / MapView.ReferenceSize - 1.0 / MapView.ZoomAdjust * MapView.LogicalSize / 2,
                    p2.X / MapView.ZoomAdjust * MapView.LogicalSize / MapView.ReferenceSize - 1.0 / MapView.ZoomAdjust * MapView.LogicalSize / 2,
                    -(p2.Y / MapView.ZoomAdjust * MapView.LogicalSize / MapView.ReferenceSize) + 1.0 / MapView.ZoomAdjust * MapView.LogicalSize / 2,
                    -(p1.Y / MapView.ZoomAdjust * MapView.LogicalSize / MapView.ReferenceSize) + 1.0 / MapView.ZoomAdjust * MapView.LogicalSize / 2),
                    Map.UseAnimation);

                e.Handled = true;
            }

            if (!wasPanned) return;

            wasPanned = false;
            e.Handled = true;
        }        

        /// <summary> Event handler for pressing the mouse button. A double click with the left mouse button results in
        /// zooming in the map. A double click with the right mouse button results in zooming out the map. Pressing the
        /// left button and the shift key, the zooming rectangle is shown on the map. </summary>
        /// <param name="sender"> Sender of the MouseDown event. </param>
        /// <param name="e"> Event parameters. </param>
        private void source_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!IsActive)
                return;

            mapView.Focus();

            // NEW: Skip MouseDragMode == None
            if (MouseDragMode == DragMode.None)
            {
                e.Handled = true;
                return;
            }

            if (ZoomOnDoubleClick && e.ClickCount == 2)
            {
                Point p = MapView.CanvasToPtvMercator(MapView.GeoCanvas, e.GetPosition(MapView.GeoCanvas));

                if (e.RightButton == MouseButtonState.Pressed)
                {
                    MapView.ZoomAround(p, MapView.FinalZoom + -1, Map.UseAnimation);

                    e.Handled = true;
                    return;
                }

                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    MapView.ZoomAround(p, MapView.FinalZoom + 1, Map.UseAnimation);

                    e.Handled = true;
                    return;
                }
            }

            // Save starting point, used later when determining how much to scroll.
            ScreenStartPoint = e.GetPosition(mapView);
            WorldStartPoint = mapView.CanvasToPtvMercator(mapView, e.GetPosition(mapView));

            // NEW: check for MouseDragMode
            if (e.LeftButton == MouseButtonState.Pressed && (MouseDragMode == DragMode.Select ||
                 MouseDragMode == DragMode.SelectOnShift && (Keyboard.Modifiers & ModifierKeys.Shift) > 0)) // was && (Keyboard.Modifiers & ModifierKeys.Shift) > 0))
            {
                mapView.Cursor = Cursors.Arrow;

                if (dragRectangle != null)
                    mapView.ForePaneCanvas.Children.Remove(dragRectangle);

                dragRectangle = new Rectangle();
                dragRectangle.IsHitTestVisible = false;
                dragRectangle.Fill = new SolidColorBrush(Color.FromArgb(0x3e, 0x11, 0x57, 0xdc));
                dragRectangle.Stroke = new SolidColorBrush(Color.FromArgb(0x55, 0x07, 0x81, 0xf7));
                dragRectangle.StrokeDashArray = new DoubleCollection(new double[] { 20, 8 });
                dragRectangle.StrokeEndLineCap = PenLineCap.Round;
                dragRectangle.StrokeDashCap = PenLineCap.Round;
                dragRectangle.StrokeThickness = 1.5;
                dragRectangle.RadiusX = 8;
                dragRectangle.RadiusY = 8;

                Panel.SetZIndex(dragRectangle, 266);
                Canvas.SetLeft(dragRectangle, ScreenStartPoint.X);
                Canvas.SetTop(dragRectangle, ScreenStartPoint.Y);
                mapView.ForePaneCanvas.Children.Add(dragRectangle);
                dragMode = DragMode.Select;
                mapView.CaptureMouse();
            }
            else if (e.LeftButton == MouseButtonState.Pressed && mapView.CaptureMouse())
            {
                mapView.Cursor = Cursors.Hand;
                dragMode = DragMode.Pan;
                wasPanned = false;
            }
        }

        /// <summary> Event handler for moving the mouse. If you are currently panning, the map is moved together with
        /// the mouse. If you are currently in zoom mode, the Drag rectangle is shown and changed when moving the mouse. </summary>
        /// <param name="sender"> Sender of the MouseMove event. </param>
        /// <param name="e"> Event parameters. </param>
        private void control_MouseMove(object sender, MouseEventArgs e)
        {
            if (!IsActive || !mapView.IsMouseCaptured)
                return;

            switch (dragMode)
            {
                case DragMode.Pan:
                {
                    var physicalPoint = mapView.CanvasToPtvMercator(mapView, e.GetPosition(mapView));

                    if (WorldStartPoint.X == physicalPoint.X && WorldStartPoint.Y == physicalPoint.Y)
                        return;

                    double x = mapView.CurrentX + WorldStartPoint.X - physicalPoint.X;
                    double y = mapView.CurrentY + WorldStartPoint.Y - physicalPoint.Y;

                    wasPanned = true;

                    mapView.SetXYZ(x, y, mapView.CurrentZoom, Map.UseAnimation);
                    break;
                }
                case DragMode.Select:
                {
                    var physicalPoint = e.GetPosition(mapView);

                    Canvas.SetLeft(dragRectangle, physicalPoint.X < ScreenStartPoint.X ? physicalPoint.X : ScreenStartPoint.X);
                    Canvas.SetTop(dragRectangle, physicalPoint.Y < ScreenStartPoint.Y ? physicalPoint.Y : ScreenStartPoint.Y);
                    dragRectangle.Width = Math.Abs(physicalPoint.X - ScreenStartPoint.X);
                    dragRectangle.Height = Math.Abs(physicalPoint.Y - ScreenStartPoint.Y);
                    break;
                }
            }

            e.Handled = true;
        }
        #endregion

        #region protected methods
        /// <inheritdoc/>
        protected override void Initialize()
        {
            base.Initialize();

            IsActive = true;
            mapView = MapView;
            Setup();
        }
        #endregion
    }
}
