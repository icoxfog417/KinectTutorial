namespace KinectTutorial.KinectUtil.Data
{
    using System;
    using Microsoft.Kinect;

    /// <summary>
    /// Utility Class to manage Kinect Sensor
    /// </summary>
    public class Infrared : IDisposable
    {
        // Size of the RGBA pixel in the bitmap
        private const int BytesPerPixel = 4;
        
        /// <summary>
        /// The highest value that can be returned in the InfraredFrame.
        /// It is cast to a float for readability in the visualization code.
        /// </summary>
        private const float InfraredSourceValueMaximum = (float)ushort.MaxValue;

        /// </summary>
        /// Used to set the lower limit, post processing, of the infrared data that we will render.
        /// Increasing or decreasing this value sets a brightness "wall" either closer or further away.
        /// </summary>
        private const float InfraredOutputValueMinimum = 0.01f;

        /// <summary>
        /// The upper limit, post processing, of the infrared data that will render.
        /// </summary>
        private const float InfraredOutputValueMaximum = 1.0f;

        /// <summary>
        /// The InfraredSceneValueAverage value specifies the average infrared value of the scene. 
        /// This value was selected by analyzing the average pixel intensity for a given scene.
        /// This could be calculated at runtime to handle different IR conditions of a scene (outside vs inside).
        /// </summary>
        private const float InfraredSceneValueAverage = 0.08f;

        /// <summary>
        /// The InfraredSceneStandardDeviations value specifies the number of standard deviations to apply to InfraredSceneValueAverage.
        /// This value was selected by analyzing data from a given scene.
        /// This could be calculated at runtime to handle different IR conditions of a scene (outside vs inside).
        /// </summary>
        private const float InfraredSceneStandardDeviations = 3.0f;

        //Infrared Frame
        private InfraredFrameReader infraredFrameReader = null;
        public int Width { get; private set; }
        public int Height { get; private set; }
        private ushort[] infraredFrame = null;
        private byte[] infraredPixels = null;

        public delegate void InfraredPixelsHandler(int stride, byte[] infraredPixels);
        private InfraredPixelsHandler pixelsHandler = null;

        public Infrared(KinectSensor sensor, InfraredPixelsHandler pixelsHandler)
        {
            // get the infraredFrameDescription from the InfraredFrameSource
            FrameDescription infraredFrameDescription = sensor.InfraredFrameSource.FrameDescription;
            this.Width = infraredFrameDescription.Width;
            this.Height = infraredFrameDescription.Height;
            this.infraredFrame = new ushort[this.Width * this.Height];
            this.infraredPixels = new byte[this.Width * this.Height * BytesPerPixel];

            // set handler to handle each infrared frame
            this.pixelsHandler = pixelsHandler;
            this.Bind();

            // open the reader for the infrared frames
            this.infraredFrameReader = sensor.InfraredFrameSource.OpenReader();

        }
        
        public void Bind()
        {
            if (this.infraredFrameReader != null)
            {
                this.infraredFrameReader.FrameArrived += this.OnInfraredFrameArrived;
            }
        }

        public void Dispose()
        {
            if(this.infraredFrameReader != null)
            {
                this.infraredFrameReader.Dispose();
                this.infraredFrameReader = null;
            }
        }


        private void OnInfraredFrameArrived(object sender, InfraredFrameArrivedEventArgs e)
        {
            int stride = 0;

            using (InfraredFrame frame = e.FrameReference.AcquireFrame())
            {
                if(frame != null)
                {
                    FrameDescription fd = frame.FrameDescription;
                    // verify length
                    if (fd.Width * fd.Height == this.infraredFrame.Length)
                    {
                        frame.CopyFrameDataToArray(this.infraredFrame);
                        stride = fd.Width * BytesPerPixel;
                    }
                }
            }

            if (stride > 0)
            {
                SetPixelsByRGBA(this.infraredFrame);
                this.pixelsHandler(stride, this.infraredPixels);
            }

        }

        private void SetPixelsByRGBA(ushort[] frame)
        {
            int colorPixelIndex = 0;
            for (int i = 0; i < frame.Length; i++)
            {
                // value to ratio
                float intensityRatio = (float)frame[i] / InfraredSourceValueMaximum;
                // normalize
                intensityRatio /= InfraredSceneValueAverage * InfraredSceneStandardDeviations;
                // limit value (upper/lower)
                intensityRatio = Math.Min(InfraredOutputValueMaximum, intensityRatio);
                intensityRatio = Math.Max(InfraredOutputValueMinimum, intensityRatio);

                byte intensity = (byte)(intensityRatio * 255.0f);
                this.infraredPixels[colorPixelIndex++] = intensity; //Blue
                this.infraredPixels[colorPixelIndex++] = intensity; //Green
                this.infraredPixels[colorPixelIndex++] = intensity; //Red
                this.infraredPixels[colorPixelIndex++] = 255; //Alpha

            }
        }

    }
}
