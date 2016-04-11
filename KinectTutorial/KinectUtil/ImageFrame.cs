namespace KinectTutorial.KinectUtil.Image

{
    using System;
    using Microsoft.Kinect;

    public enum FrameType
    {
        Infrared,
        Color
    }


    /// <summary>
    /// Utility Class to manage Kinect Sensor
    /// </summary>
    public class ImageSensor : IDisposable
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
        private FrameType frameType = FrameType.Color;
        private FrameDescription frameDescription = null;
        private MultiSourceFrameReader frameReader = null;
        public int Width { get; private set; }
        public int Height { get; private set; }
        private byte[] pixels = null;

        public delegate void PixelsHandler(int stride, byte[] pixels);
        private PixelsHandler pixelsHandler = null;

        public ImageSensor(KinectSensor sensor, PixelsHandler pixelsHandler, FrameType frameType = FrameType.Infrared)
        {

            this.Switch(sensor, frameType);
            this.pixelsHandler = pixelsHandler;
            this.frameReader = sensor.OpenMultiSourceFrameReader(FrameSourceTypes.Color|FrameSourceTypes.Infrared);
            this.BindHandler();
        }

        public void Switch(KinectSensor sensor, FrameType frameType)
        {
            this.frameType = frameType;
            switch(this.frameType)
            {
                case FrameType.Infrared:
                    this.frameDescription = sensor.InfraredFrameSource.FrameDescription;
                    break;
                case FrameType.Color:
                    this.frameDescription = sensor.ColorFrameSource.FrameDescription;
                    break;
                default:
                    break;
            }

            this.Width = this.frameDescription.Width;
            this.Height = this.frameDescription.Height;
            this.pixels = new byte[this.Width * this.Height * BytesPerPixel];
        }

        public void BindHandler()
        {
            if (this.frameReader != null)
            {
                this.frameReader.MultiSourceFrameArrived += this.OnFrameArrived;
            }
        }

        public void Dispose()
        {
            if(this.frameReader != null)
            {
                this.frameReader.Dispose();
                this.frameReader = null;
            }
        }


        private void OnFrameArrived(object sender, MultiSourceFrameArrivedEventArgs e)
        {
            MultiSourceFrame multiFrame = e.FrameReference.AcquireFrame();
            
            if(multiFrame != null)
            {
                switch (this.frameType)
                {
                    case FrameType.Infrared:
                        using (InfraredFrame frame = multiFrame.InfraredFrameReference.AcquireFrame())
                        {
                            this.SetInfraredFrame(frame);
                        }
                        break;
                    case FrameType.Color:
                        using (ColorFrame frame = multiFrame.ColorFrameReference.AcquireFrame())
                        {
                            this.SetColorFrame(frame);
                        }
                        break;
                    default:
                        break;
                }
            }

        }


        private void SetInfraredFrame(InfraredFrame frame)
        {
            FrameDescription fd = null;

            // validation
            if (frame == null)
            {
                return;
            }else
            {
                fd = frame.FrameDescription;
                if(fd.Width != this.Width || fd.Height != this.Height)
                {
                    return;
                }
            }

            // frame to array
            ushort[] frameArray = new ushort[this.Width * this.Height]; ;
            frame.CopyFrameDataToArray(frameArray);

            // array to pixels (RGBA conversion)
            int colorPixelIndex = 0;
            for (int i = 0; i < frameArray.Length; i++)
            {
                // value to ratio
                float intensityRatio = (float)frameArray[i] / InfraredSourceValueMaximum;
                // normalize
                intensityRatio /= InfraredSceneValueAverage * InfraredSceneStandardDeviations;
                // limit value (upper/lower)
                intensityRatio = Math.Min(InfraredOutputValueMaximum, intensityRatio);
                intensityRatio = Math.Max(InfraredOutputValueMinimum, intensityRatio);

                byte intensity = (byte)(intensityRatio * 255.0f);
                this.pixels[colorPixelIndex++] = intensity; //Blue
                this.pixels[colorPixelIndex++] = intensity; //Green
                this.pixels[colorPixelIndex++] = intensity; //Red
                this.pixels[colorPixelIndex++] = 255; //Alpha

            }

            int stride = fd.Width * BytesPerPixel;
            this.pixelsHandler(stride, this.pixels);

        }

        private void SetColorFrame(ColorFrame frame)
        {
            FrameDescription fd = null;

            // validation
            if (frame == null)
            {
                return;
            }
            else
            {
                fd = frame.FrameDescription;
                if (fd.Width != this.Width || fd.Height != this.Height)
                {
                    return;
                }
            }

            // frame to pixel
            if (frame.RawColorImageFormat == ColorImageFormat.Bgra)
            {
                frame.CopyRawFrameDataToArray(this.pixels);
            }else
            {
                frame.CopyConvertedFrameDataToArray(this.pixels, ColorImageFormat.Bgra);
            }

            int stride = fd.Width * BytesPerPixel;
            this.pixelsHandler(stride, this.pixels);

        }

    }
}
