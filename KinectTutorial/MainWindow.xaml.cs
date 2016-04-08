namespace KinectTutorial
{
    using System.Windows;
    using System.ComponentModel;
    using System.Drawing;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using Microsoft.Kinect;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        private KinectSensor sensor = null;
        private KinectUtil.Data.Infrared infraredStream = null;

        private WriteableBitmap infraredSource = null;
        public ImageSource ImageSource
        {
            get
            {
                return this.infraredSource;
            }

        }

        public MainWindow()
        {
            this.sensor = KinectSensor.GetDefault();
            infraredStream = new KinectUtil.Data.Infrared(this.sensor, this.RenderInfraredPixels);

            this.infraredSource = new WriteableBitmap(infraredStream.Width, infraredStream.Height, 96.0, 96.0, PixelFormats.Bgra32, null);

            // bind with windows
            this.DataContext = this;

            this.sensor.Open();
            InitializeComponent();
        }

        public void RenderInfraredPixels(int stride, byte[] infraredPixels)
        {
            this.infraredSource.Lock();

            Int32Rect area = new Int32Rect(0, 0, this.infraredSource.PixelWidth, this.infraredSource.PixelHeight);
            this.infraredSource.WritePixels(area, infraredPixels, stride, 0);

            this.infraredSource.Unlock();

        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            this.infraredStream.Bind();
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            this.infraredStream.Dispose();
            if (this.sensor != null)
            {
                this.sensor.Close();
                this.sensor = null;
            }
        }

    }
}
