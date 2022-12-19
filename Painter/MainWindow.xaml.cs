using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
using CImage = System.Windows.Controls.Image;

namespace Painter
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private MainViewModel viewModel;

        private Lazy<ImageSource> source = new(); //Image source
        private Lazy<System.Windows.Point> prevMousePos = new(); //Previos mouse position
        private Dictionary<CImage, double> aspectRatios = new(); //Images aspect rations
        private double weelDecr = 0.5; // Image scale speed
        private DrawingMode currentMode = DrawingMode.None;//Drawing mode
        private Lazy<Line> capturedLine = new();//Current captured line
        private Lazy<Border> capturedBorder = new();//Current captured border
        private Lazy<Ellipse> captured_circle = new();//Current captured circle
        private Lazy<Rectangle> captured_rectangle = new();//Current captured rectangle


        public MainWindow()
        {
            viewModel = new MainViewModel();
            this.DataContext = viewModel;

            InitializeComponent();
        }

        private void Img_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            double dtl = weelDecr * e.Delta;

            if ((sender as CImage).Width + dtl > 10 && (sender as CImage).Height + dtl > 10)
            {
                setImageWidth((CImage)sender, (sender as CImage).Width + dtl);
                checkResized(sender as CImage, Canvas.GetLeft(sender as CImage), Canvas.GetTop(sender as CImage), workField);
            }
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            ContextMenu menu = (sender as MenuItem).Parent as ContextMenu;
            CImage img = menu.PlacementTarget as CImage;
            workField.Children.Remove(img);
        }

        private void TransformHorizontaly_Click(object sender, RoutedEventArgs e)
        {
            //Get image
            ContextMenu menu = (sender as MenuItem).Parent as ContextMenu;
            CImage img = menu.PlacementTarget as CImage;
            ScaleTransform transform;

            transform = (ScaleTransform)img.RenderTransform;

            if (transform.ScaleX == -1)
                transform.ScaleX = 1;
            else
                transform.ScaleX = -1;
        }

        private void Rotate90angle_Click(object sender, RoutedEventArgs e)
        {
            //Get image
            ContextMenu menu = (sender as MenuItem).Parent as ContextMenu;
            CImage img = menu.PlacementTarget as CImage;
            BitmapImage pimg = (BitmapImage)img.Source;

            //Init new source
            BitmapImage nim = new BitmapImage();
            nim.BeginInit();
            nim.UriSource = pimg.UriSource;
            if (pimg.Rotation == Rotation.Rotate270)
                nim.Rotation = Rotation.Rotate0;
            else
                nim.Rotation = pimg.Rotation + 1;
            nim.EndInit();
            //Set rotated source
            img.Source = nim;

            //Updating imageBox size
            aspectRatios[img] = 1 / aspectRatios[img];
            setImageHeight(img, img.Width);
            checkResized(img, Canvas.GetTop(img), Canvas.GetLeft(img), workField);
        }

        private void Img_MouseLeave(object sender, MouseEventArgs e)
        {
            prevMousePos = new();
            if(sender is not null)
                (sender as CImage).ReleaseMouseCapture();
        }

        private void Img_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                if (sender is not null)
                    (sender as CImage).CaptureMouse();
            }
        }

        /// <summary>
        /// Update image position on mouse move
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Img_MouseMove(object sender, MouseEventArgs e)
        {
            if ((sender as CImage).IsMouseCaptured)
            {
                System.Windows.Point mousePos = e.MouseDevice.GetPosition(workField);
                IsFitIn isFitIn = new();//Preventing img oversize 

                if (prevMousePos.IsValueCreated)
                {
                    double left = Canvas.GetLeft((CImage)sender) + mousePos.X - prevMousePos.Value.X;
                    double top = Canvas.GetTop((CImage)sender) + mousePos.Y - prevMousePos.Value.Y;

                    isFitIn = checkResized((CImage)sender, top, left, workField);

                    if (isFitIn.NTop)
                    {
                        Canvas.SetTop((CImage)sender, top);
                    }

                    if (isFitIn.NLeft)
                    {
                        Canvas.SetLeft((CImage)sender, left);
                    }
                }

                if (isFitIn.FullyFit)
                    prevMousePos = new(mousePos);
                else
                    prevMousePos = new();
            }
        }

        private bool isFitInLeft(FrameworkElement element, double canvasLeft, Canvas canvas)
        {
            if (canvas.ActualWidth < element.Width)
            {
                setImageWidth((CImage)element, element.Width - (element.Width - canvas.ActualWidth));
            }

            if (canvasLeft < 0)
            {
                Canvas.SetLeft(element, 0);
                return false;
            }
            else if (element.Width + canvasLeft > canvas.ActualWidth)
            {
                Canvas.SetLeft(element, canvas.ActualWidth - element.Width);
                return false;
            }

            return true;
        }

        private bool isFitInTop(FrameworkElement element, double canvasTop, Canvas canvas)
        {
            if (canvas.ActualHeight < element.Height)
            {
                setImageHeight((CImage)element, element.Height - (element.Height - canvas.ActualHeight));
            }

            if (canvasTop < 0)
            {
                Canvas.SetTop(element, 0);
                return false;
            }
            else if (element.Height + canvasTop > canvas.ActualHeight)
            {
                Canvas.SetTop(element, canvas.ActualHeight - element.Height);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Looks up for any canvas size overflow
        /// </summary>
        /// <param name="element"></param>
        /// <param name="top"></param>
        /// <param name="left"></param>
        /// <param name="canvas"></param>
        /// <returns></returns>
        private IsFitIn checkResizedEl(FrameworkElement element, double top, double left, Canvas canvas)
        {
            IsFitIn fitIn = new();

            fitIn.NTop = isFitInTop(element, top, canvas);
            fitIn.NLeft = isFitInLeft(element, left, canvas);

            return fitIn;
        }

        /// <summary>
        /// Set new image width with persisting image ratio
        /// </summary>
        /// <param name="image"></param>
        /// <param name="width"></param>
        private void setImageWidth(CImage image, double width)
        {
            double ar;

            if (aspectRatios.TryGetValue(image, out ar))
            {
                image.Width = width;
                image.Height = width / ar;
                updateTransform(image);
            }
        }

        /// <summary>
        /// Set new image height with persisting image ratio
        /// </summary>
        /// <param name="image"></param>
        /// <param name="height"></param>
        private void setImageHeight(CImage image, double height)
        {
            double ar;

            if (aspectRatios.TryGetValue(image, out ar))
            {
                image.Height = height;
                image.Width = height * ar;
                updateTransform(image);
            }
        }

        /// <summary>
        /// Update transform cordinates to keep'em right
        /// </summary>
        /// <param name="image"></param>
        private void updateTransform(CImage image)
        {
            ScaleTransform transform = (ScaleTransform)image.RenderTransform;
            transform.CenterX = image.Width / 2;
            transform.CenterY = image.Height / 2;
        }

        private IsFitIn checkResized(FrameworkElement element, double top, double left, Canvas canvas)
        {
            IsFitIn fitIn = new();

            fitIn.NTop = isFitInTop(element, top, canvas);
            fitIn.NLeft = isFitInLeft(element, left, canvas);

            return fitIn;
        }

        private void workField_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            foreach (var child in workField.Children.OfType<FrameworkElement>())
            {
                double top = Canvas.GetTop(child);
                double left = Canvas.GetLeft(child);

                checkResized(child, top, left, workField);
            }
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new();

            //Fill up all existing image extensions
            openFileDialog.Filter = "All Pictures(*.emf; *.wmf; *.jpg; *.jpeg; *.jfif; *.jpe; *.png; *.bmp; *.dib; *.rle; *.gif; *.emz; *.wmz; *.tif; *.tiff; *.svg; *.ico)" +
            "|*.emf;*.wmf;*.jpg;*.jpeg;*.jfif;*.jpe;*.png;*.bmp;*.dib;*.rle;*.gif;*.emz;*.wmz;*.tif;*.tiff;*.svg;*.ico";
            openFileDialog.DefaultExt = "PNG (*.PNG)|*.PNG";

            if (openFileDialog.ShowDialog() ?? false)
            {
                //Generating image source
                BitmapImage b = new();
                b.BeginInit();
                b.UriSource = new(openFileDialog.FileName);
                b.EndInit();
                source = new(b);
                itemPreview.Source = source.Value;
            }

            if (source.IsValueCreated)
            {
                CImage img = new()
                {
                    Source = source.Value,
                    Height = source.Value.Height,
                    Width = source.Value.Width,
                    RenderTransform = new ScaleTransform(1, 1, Width / 2, Height / 2),
                    MinHeight = 10,
                    MinWidth = 10
                };
                img.MouseMove += Img_MouseMove;
                img.MouseUp += Img_MouseLeave;
                img.MouseDown += Img_MouseDown;
                img.MouseLeave += Img_MouseLeave;
                img.MouseWheel += Img_MouseWheel;

                //init menu
                ContextMenu cm = new();
                img.ContextMenu = cm;

                //initializing items
                MenuItem rotate90angle = new();
                rotate90angle.Header = "Rotate at 90";
                rotate90angle.Click += Rotate90angle_Click;
                MenuItem transformHorizontaly = new();
                transformHorizontaly.Header = "Flip Horizontally";
                transformHorizontaly.Click += TransformHorizontaly_Click;
                MenuItem delete = new();
                delete.Header = "Remove image";
                delete.Click += Delete_Click;

                //adding Items
                cm.Items.Add(rotate90angle);
                cm.Items.Add(transformHorizontaly);
                cm.Items.Add(delete);

                //adding img aspect ratio
                aspectRatios.Add(img, img.Width / img.Height);

                while (workField.ActualHeight < img.Height)
                {
                    setImageHeight(img, img.Height / 2);
                }
                while (workField.ActualWidth < img.Width)
                {
                    setImageWidth(img, img.Width / 2);
                }

                Canvas.SetTop(img, (workField.ActualHeight - img.Height) / 2);
                Canvas.SetLeft(img, (workField.ActualWidth - img.Width) / 2);

                workField.Children.Add(img);
            }
        }

        #region Mouse events
        private void onMouseCaprute(object sender, MouseButtonEventArgs e)
        {
            switch (currentMode)
            {
                case DrawingMode.Line:
                    lineMouseCaprute(sender, e);
                    break;
                case DrawingMode.Border:
                    borderMouseCaprute(sender, e);
                    break;
                case DrawingMode.Borderless_circle or DrawingMode.Circle:
                    circleMouseCaprute(sender, e);
                    break;
                case DrawingMode.Rectangle:
                    rectangleMouseCaprute(sender, e);
                    break;           
            }
        }

        private void onMouseMove(object sender, MouseEventArgs e)
        {
            switch (currentMode)
            {
                case DrawingMode.Pen:
                    abstractPenMouseMove(sender, e, GetPrimaryBrush());
                    break;
                case DrawingMode.Eraser:
                    abstractPenMouseMove(sender, e, GetSecondaryBrush());
                    break;
                case DrawingMode.Line:
                    lineMouseMove(sender, e);
                    break;
                case DrawingMode.Border:
                    borderMouseMove(sender, e);
                    break;
                case DrawingMode.Borderless_circle:
                    circleMouseMove(sender, e, false);
                    break;
                case DrawingMode.Circle:
                    circleMouseMove(sender, e, true);
                    break;
                case DrawingMode.Rectangle:
                    rectangleMouseMove(sender, e);
                    break;
            }
        }

        private void onReleaseMouseCapture(object sender, MouseEventArgs e)
        {
            switch (currentMode)
            {
                case DrawingMode.Line:
                    lineReleaseMouseCapture(sender, e);
                    break;
                case DrawingMode.Border:
                    borderReleaseMouseCapture(sender, e);
                    break;
                case DrawingMode.Borderless_circle or DrawingMode.Circle:
                    circleReleaseMouseCapture(sender, e);
                    break;
                case DrawingMode.Rectangle:
                    rectangleReleaseMouseCapture(sender, e);
                    break;
            }
        }
        #endregion

        private void workField_MouseMove(object sender, MouseEventArgs e) => onMouseMove(sender, e);

        private void abstractPenMouseMove(object sender, MouseEventArgs e, Brush color_brush)
        {
            if ((sender as Canvas).IsMouseCaptured)
            {
                System.Windows.Point mousePos = e.MouseDevice.GetPosition(workField);

                if (prevMousePos.IsValueCreated)
                {
                    Line line = new();
                    line.Stroke = color_brush;
                    line.StrokeThickness = GetThickness();
                    (line.X1, line.Y1) = (prevMousePos.Value.X, prevMousePos.Value.Y);
                    (line.X2, line.Y2) = (mousePos.X, mousePos.Y);

                    workField.Children.Add(line);
                }

                prevMousePos = new(mousePos);
            }
        }

        private void lineMouseCaprute(object sender, MouseButtonEventArgs e)
        {
            prevMousePos = new();
        }

        private void lineMouseMove(object sender, MouseEventArgs e)
        {
            if ((sender as Canvas).IsMouseCaptured)
            {
                System.Windows.Point mousePos = e.MouseDevice.GetPosition(workField);

                if (prevMousePos.IsValueCreated)
                {
                    if (capturedLine.IsValueCreated)
                    {
                        (capturedLine.Value.X2, capturedLine.Value.Y2) = (mousePos.X, mousePos.Y);
                    }
                    else
                    {
                        Line line = new();
                        line.Stroke = GetPrimaryBrush();
                        line.StrokeThickness = GetThickness();
                        (line.X1, line.Y1) = (prevMousePos.Value.X, prevMousePos.Value.Y);
                        (line.X2, line.Y2) = (mousePos.X, mousePos.Y);

                        capturedLine = new(line);
                        workField.Children.Add(capturedLine.Value);
                    }
                }
                else
                {
                    prevMousePos = new(mousePos);
                }
            }
        }

        private void lineReleaseMouseCapture(object sender, MouseEventArgs e)
        {
            prevMousePos = new();
            capturedLine = new();
        }

        private void borderMouseCaprute(object sender, MouseButtonEventArgs e)
        {
            prevMousePos = new();
        }

        private void borderMouseMove(object sender, MouseEventArgs e)
        {
            Canvas canvas = sender as Canvas;

            if (canvas.IsMouseCaptured)
            {
                System.Windows.Point mousePos = e.MouseDevice.GetPosition(workField);

                if (prevMousePos.IsValueCreated)
                {
                    if (capturedBorder.IsValueCreated)
                    {
                        adjustBorderPosition(capturedBorder.Value, prevMousePos.Value, mousePos);
                    }
                    else
                    {
                        Border border = new();
                        border.BorderBrush = GetPrimaryBrush();
                        border.BorderThickness = new Thickness(GetThickness());
                        adjustBorderPosition(border, prevMousePos.Value, mousePos);

                        capturedBorder = new(border);
                        workField.Children.Add(capturedBorder.Value);
                    }
                }
                else
                {
                    prevMousePos = new(mousePos);
                }
            }
        }

        private void adjustBorderPosition(Border border, Point startP, Point endP)
        {
            Canvas.SetLeft(border, startP.X < endP.X ? startP.X : endP.X);
            border.Width = Math.Abs(endP.X - startP.X);

            Canvas.SetTop(border, startP.Y < endP.Y ? startP.Y : endP.Y);
            border.Height = Math.Abs(endP.Y - startP.Y);
        }

        private void borderReleaseMouseCapture(object sender, MouseEventArgs e)
        {
            prevMousePos = new();
            capturedBorder = new();
        }

        private void circleMouseCaprute(object sender, MouseButtonEventArgs e)
        {
            prevMousePos = new();
        }

        private void circleMouseMove(object sender, MouseEventArgs e, bool isFill)
        {
            Canvas canvas = sender as Canvas;

            if (canvas.IsMouseCaptured)
            {
                System.Windows.Point mousePos = e.MouseDevice.GetPosition(workField);

                if (prevMousePos.IsValueCreated)
                {
                    if (captured_circle.IsValueCreated)
                    {
                        adjustToSquare(captured_circle.Value, prevMousePos.Value, mousePos);
                    }
                    else
                    {
                        Ellipse borderless_circle = new();
                        borderless_circle.Fill = isFill ? GetPrimaryBrush() : new SolidColorBrush() { Opacity = 0};
                        borderless_circle.Stroke = GetPrimaryBrush();
                        borderless_circle.StrokeThickness = GetThickness();

                        adjustToSquare(borderless_circle, prevMousePos.Value, mousePos);

                        captured_circle = new(borderless_circle);
                        workField.Children.Add(captured_circle.Value);
                    }
                }
                else
                {
                    prevMousePos = new(mousePos);
                }
            }
        }

        private void circleReleaseMouseCapture(object sender, MouseEventArgs e)
        {
            prevMousePos = new();
            captured_circle = new();
        }


        private void rectangleMouseCaprute(object sender, MouseButtonEventArgs e)
        {
            prevMousePos = new();
        }

        private void rectangleMouseMove(object sender, MouseEventArgs e)
        {
            Canvas canvas = sender as Canvas;

            if (canvas.IsMouseCaptured)
            {
                System.Windows.Point mousePos = e.MouseDevice.GetPosition(workField);

                if (prevMousePos.IsValueCreated)
                {
                    if (capturedBorder.IsValueCreated)
                    {
                        adjustShapePosition(captured_rectangle.Value, prevMousePos.Value, mousePos);
                    }
                    else
                    {
                        Rectangle rectangle = new();
                        rectangle.Fill = GetPrimaryBrush();
                        adjustShapePosition(rectangle, prevMousePos.Value, mousePos);

                        captured_rectangle = new(rectangle);
                        workField.Children.Add(captured_rectangle.Value);
                    }
                }
                else
                {
                    prevMousePos = new(mousePos);
                }
            }
        }

        private void rectangleReleaseMouseCapture(object sender, MouseEventArgs e)
        {
            prevMousePos = new();
            captured_rectangle = new();
        }

        private void adjustToSquare(Shape shape, Point startP, Point endP)
        {
            double xs = Math.Abs(startP.X - endP.X); //Side X size || width
            double ys = Math.Abs(startP.Y - endP.Y); //Side Y size || height
            double delta = Math.Abs(xs - ys);

            if (xs > ys)
            {
                if (startP.X < endP.X)
                    endP.X -= delta;
                else
                    startP.X -= delta;
            }
            else
            {
                if (startP.Y < endP.Y)
                    endP.Y -= delta;
                else
                    startP.Y -= delta;
            }

            adjustShapePosition(shape, startP, endP);
        }

        private void adjustShapePosition(Shape shape, Point startP, Point endP)
        {
            Canvas.SetLeft(shape, startP.X < endP.X ? startP.X : endP.X);
            shape.Width = Math.Abs(endP.X - startP.X);

            Canvas.SetTop(shape, startP.Y < endP.Y ? startP.Y : endP.Y);
            shape.Height = Math.Abs(endP.Y - startP.Y);
        }

        private void workField_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                (sender as Canvas).CaptureMouse();
                onMouseCaprute(sender, e);
            }
        }

        private Brush GetPrimaryBrush()
        {
            return new SolidColorBrush(primary_color.SelectedColor ?? Colors.Black);
        }

        private Brush GetSecondaryBrush()
        {
            return new SolidColorBrush(secondary_color.SelectedColor ?? Colors.White);
        }

        private double GetThickness()
        {
            return Convert.ToDouble(ThicknessComboBox.SelectedItem ?? 2);
        }


        private void Pen_Button_Click(object sender, RoutedEventArgs e) => currentMode = DrawingMode.Pen;
        private void Line_Button_Click(object sender, RoutedEventArgs e) => currentMode = DrawingMode.Line;
        private void Border_Button_Click(object sender, RoutedEventArgs e) => currentMode = DrawingMode.Border;
        private void Borderless_circle_Button_Click(object sender, RoutedEventArgs e) => currentMode = DrawingMode.Borderless_circle;
        private void Triangle_Button_Click(object sender, RoutedEventArgs e) => currentMode = DrawingMode.Triangle;
        private void Brocken_line_Button_Click(object sender, RoutedEventArgs e) => currentMode = DrawingMode.Brocken_line;
        private void Bezie_Button_Click(object sender, RoutedEventArgs e) => currentMode = DrawingMode.Bezie;
        private void Spline_Button_Click(object sender, RoutedEventArgs e) => currentMode = DrawingMode.Spline;
        private void Rectangle_Button_Click(object sender, RoutedEventArgs e) => currentMode = DrawingMode.Rectangle;
        private void Circle_Button_Click(object sender, RoutedEventArgs e) => currentMode = DrawingMode.Circle;
        private void Eraser_Button_Click(object sender, RoutedEventArgs e) => currentMode = DrawingMode.Eraser;
        private void Text_Button_Click(object sender, RoutedEventArgs e) => currentMode = DrawingMode.Text;
        private void Area_Button_Click(object sender, RoutedEventArgs e) => currentMode = DrawingMode.Area;
        private void Picture_Button_Click(object sender, RoutedEventArgs e) => currentMode = DrawingMode.Picture;
        

        private void workField_MouseLeave(object sender, MouseEventArgs e)
        {
            (sender as Canvas).ReleaseMouseCapture();
            onReleaseMouseCapture(sender, e);
        }

        private void workField_MouseUp(object sender, MouseButtonEventArgs e)
        {
            (sender as Canvas).ReleaseMouseCapture();
            onReleaseMouseCapture(sender, e);
        }
    }
}
