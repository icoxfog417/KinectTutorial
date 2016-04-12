namespace KinectTutorial
{
    using System.Windows;
    using System.ComponentModel;
    using System.Drawing;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using Microsoft.Kinect;
    using KinectUtil;
    using KinectUtil.Image;
    using KinectUtil.Body;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {

        private KinectSensor sensor = null;
        private FrameType defaultType = FrameType.Infrared;
        private ImageSensor imageSensor = null;
        private BodySensor bodySensor = null;

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
            this.Switch(this.defaultType);

            // bind with windows
            this.DataContext = this;

            this.sensor.Open();
            InitializeComponent();
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

        public void RenderImage(int stride, byte[] pixels)
        {
            this.imageSource.Lock();

            Int32Rect area = new Int32Rect(0, 0, this.imageSource.PixelWidth, this.imageSource.PixelHeight);
            this.imageSource.WritePixels(area, pixels, stride, 0);

            this.imageSource.Unlock();

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
        private void BodyMaskButton_Click(object sender, RoutedEventArgs e)
        {
            this.Switch(FrameType.BodyMask);
        }

        private void Switch(FrameType frameType)
        {
            // initialize view and sensors
            if(ImageCanvas != null)
            {
                ImageCanvas.Visibility = Visibility.Collapsed;
            }
            if(BodyJointCanvas != null)
            {
                BodyJointCanvas.Visibility = Visibility.Collapsed;
            }
            if (this.imageSensor != null)
            {
                this.imageSensor.Dispose();
                this.imageSensor = null;
            }
            if (this.bodySensor != null)
            {
                this.imageSensor.Dispose();
                this.imageSensor = null;
            }

            if (frameType == FrameType.Infrared || 
                frameType == FrameType.Color || 
                frameType == FrameType.Depth || 
                frameType == FrameType.BodyMask)
            {
                this.imageSensor = new ImageSensor(this.sensor, this.RenderImage, frameType);
                if (ImageCanvas != null)
                {
                    ImageCanvas.Visibility = Visibility.Visible;
                }
                ImageSource = new WriteableBitmap(imageSensor.Width, imageSensor.Height, 96.0, 96.0, PixelFormats.Bgra32, null);
            }
            else if(frameType == FrameType.BodyJoints)
            {

                if (BodyJointCanvas != null)
                {
                    BodyJointCanvas.Visibility = Visibility.Visible;
                }

            }
        }


    }
}
