using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace TerrariaItemCounter
{
    /// <summary>
    /// Interaction logic for ZoomImageViewer.xaml
    /// </summary>
    public partial class ZoomImageViewer : UserControl
    {
        public static readonly DependencyProperty ImageSourceProperty = DependencyProperty.Register(
              "ImageSource", typeof(ImageSource), typeof(ZoomImageViewer), new PropertyMetadata(default(ImageSource)));

        private Point? lastDragPoint;
        private Point oldTarget;

        public ZoomImageViewer()
        {
            InitializeComponent();
        }

        public ImageSource ImageSource
        {
            get
            {
                return (ImageSource)GetValue(ImageSourceProperty);
            }

            set
            {
                InnerGridScaleTransform.CenterX = value.Width / 2d;
                InnerGridScaleTransform.CenterY = value.Height / 2d;
                SetValue(ImageSourceProperty, value);

                var scaleMinX = ImageScrollViewer.ViewportWidth / InnerGrid.Width;
                var scaleMinY = ImageScrollViewer.ViewportHeight / InnerGrid.Height;
                var scaleMin = Math.Min(scaleMinX, scaleMinY);
                ZoomSlider.Minimum = scaleMin;
            }
        }

        public Point TargetPoint
        {
            get
            {
                var center = new Point(ImageScrollViewer.ViewportWidth / 2,
                                       ImageScrollViewer.ViewportHeight / 2);
                return ImageScrollViewer.TranslatePoint(center, InnerGrid);
            }

            set
            {
                var center = new Point(ImageScrollViewer.ViewportWidth / 2,
                                       ImageScrollViewer.ViewportHeight / 2);
                var current = ImageScrollViewer.TranslatePoint(center, InnerGrid);
                var diff = value - current;
                diff.X *= ImageScrollViewer.ExtentWidth / InnerGrid.Width;
                diff.Y *= ImageScrollViewer.ExtentHeight / InnerGrid.Height;
                if (double.IsNaN(diff.X)
                  || double.IsNaN(diff.Y))
                {
                    return;
                }

                ImageScrollViewer.ScrollToHorizontalOffset(ImageScrollViewer.HorizontalOffset + diff.X);
                ImageScrollViewer.ScrollToVerticalOffset(ImageScrollViewer.VerticalOffset + diff.Y);
            }
        }

        void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (lastDragPoint.HasValue)
            {
                // 差分だけスクロール
                var mousePos = e.GetPosition(ImageScrollViewer);

                var diff = mousePos - lastDragPoint.Value;
                lastDragPoint = mousePos;

                ImageScrollViewer.ScrollToHorizontalOffset(ImageScrollViewer.HorizontalOffset - diff.X);
                ImageScrollViewer.ScrollToVerticalOffset(ImageScrollViewer.VerticalOffset - diff.Y);
            }
        }

        void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // マウスが範囲内なことを確認してフォーカス
            var mousePos = e.GetPosition(ImageScrollViewer);
            if (mousePos.X < ImageScrollViewer.ViewportWidth
                && mousePos.Y < ImageScrollViewer.ViewportHeight)
            {
                lastDragPoint = mousePos;
                Mouse.Capture(ImageScrollViewer);
            }
        }

        void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            //var pos = Mouse.GetPosition(InnerGrid);
            oldTarget = TargetPoint;

            ZoomSlider.Value += e.Delta * 0.0005;

            e.Handled = true;
        }

        void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            ImageScrollViewer.ReleaseMouseCapture();
            lastDragPoint = null;
        }

        void OnSliderValueChanged(object sender,
             RoutedPropertyChangedEventArgs<double> e)
        {
            InnerGridScaleTransform.ScaleX = e.NewValue;
            InnerGridScaleTransform.ScaleY = e.NewValue;

            oldTarget = TargetPoint;
        }

        void OnScrollViewerScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.ExtentHeightChange != 0 || e.ExtentWidthChange != 0)
            {
                TargetPoint = oldTarget;
            }
        }
    }
}
