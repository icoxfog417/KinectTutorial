namespace KinectTutorial
{
    using System.Windows;
    using System.ComponentModel;
    using System.Drawing;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using Microsoft.Kinect;
    using KinectUtil.Image;
    using System;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {

        private KinectSensor sensor = null;
        private FrameType defaultType = FrameType.Infrared;
        private ImageSensor imageSensor = null;

        public event PropertyChangedEventHandler PropertyChanged;

        private WriteableBitmap imageSource = null;
        public ImageSource ImageSource
        {
            get {
                return this.imageSource;
            }
            set
            {
                if(this.imageSource != value)
                {
                    this.imageSource = (WriteableBitmap)value;
                    this.OnPropertyChanged("ImageSource");
                }
            }

        }

        public MainWindow()
        {
            this.sensor = KinectSensor.GetDefault();
            imageSensor = new ImageSensor(this.sensor, this.RenderImage, this.defaultType);
            this.Switch(this.defaultType);

            // bind with windows
            this.DataContext = this;

            this.sensor.Open();
            InitializeComponent();
        }

        public void RenderImage(int stride, byte[] infraredPixels)
        {
            this.imageSource.Lock();

            Int32Rect area = new Int32Rect(0, 0, this.imageSource.PixelWidth, this.imageSource.PixelHeight);
            this.imageSource.WritePixels(area, infraredPixels, stride, 0);

            this.imageSource.Unlock();

        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            this.imageSensor.BindHandler();
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            this.imageSensor.Dispose();
            if (this.sensor != null)
            {
                this.sensor.Close();
                this.sensor = null;
            }
        }

        private void OnPropertyChanged(string name)
        {
            if(this.PropertyChanged != null)
            {
                this.PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }

        private void InfraredButton_Click(object sender, RoutedEventArgs e)
        {
            this.Switch(FrameType.Infrared);
        }
        private void ColorButton_Click(object sender, RoutedEventArgs e)
        {
            this.Switch(FrameType.Color);
        }
        private void DepthButton_Click(object sender, RoutedEventArgs e)
        {
            this.Switch(FrameType.Depth);
        }

        private void Switch(FrameType frameType)
        {
            switch(frameType)
            {
                case FrameType.Infrared:
                    this.imageSensor.Switch(this.sensor, FrameType.Infrared);
                    break;
                case FrameType.Color:
                    this.imageSensor.Switch(this.sensor, FrameType.Color);
                    break;
                case FrameType.Depth:
                    this.imageSensor.Switch(this.sensor, FrameType.Depth);
                    break;
                default:
                    break;
            }

            ImageSource = new WriteableBitmap(imageSensor.Width, imageSensor.Height, 96.0, 96.0, PixelFormats.Bgra32, null);

        }


    }
}
