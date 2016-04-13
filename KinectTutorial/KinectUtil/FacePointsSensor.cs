using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KinectTutorial.KinectUtil.Face
{
    using Microsoft.Kinect;
    using Microsoft.Kinect.Face;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;

    class FacePointsSensor : IDisposable
    {

        /// <summary>
        /// Thickness of face bounding box and face points
        /// </summary>
        private const double DrawFaceShapeThickness = 8;

        /// <summary>
        /// Font size of face property text 
        /// </summary>
        private const double DrawTextFontSize = 30;

        /// <summary>
        /// Radius of face point circle
        /// </summary>
        private const double FacePointRadius = 1.0;

        /// <summary>
        /// Text layout offset in X axis
        /// </summary>
        private const float TextLayoutOffsetX = -0.1f;

        /// <summary>
        /// Text layout offset in Y axis
        /// </summary>
        private const float TextLayoutOffsetY = -0.15f;

        /// <summary>
        /// Face rotation display angle increment in degrees
        /// </summary>
        private const double FaceRotationIncrementInDegrees = 5.0;

        /// <summary>
        /// Formatted text to indicate that there are no bodies/faces tracked in the FOV
        /// </summary>

        private CoordinateMapper coordinateMapper;

        private ColorFrameReader colorReader = null;
        private int bodyCount = 0;
        private Body[] bodies = null;
        private BodyFrameReader bodyFrameReader = null;
        private FaceFrameSource[] faceFrameSources = null;
        private FaceFrameReader[] faceFrameReaders = null;
        private FaceFrameResult[] faceFrameResults = null;
        private FaceFrameFeatures faceFrameFeatures;
        private List<Brush> faceBrush;

        private DrawingGroup drawingGroup;
        private Rect displayRect;
        private DrawingImage facePointsSource;
        public ImageSource FacePointsSource { get { return this.facePointsSource; } }

        private WriteableBitmap bitmap;
        public WriteableBitmap Bitmap { get { return this.bitmap; } }
        public delegate void OnUpdate();
        private OnUpdate onUpdate;

        public FacePointsSensor(KinectSensor sensor, OnUpdate onUpdate)
        {

            this.colorReader = sensor.ColorFrameSource.OpenReader();
            this.colorReader.FrameArrived += this.OnColorFrame;
            this.onUpdate = onUpdate;

            this.bodyCount = sensor.BodyFrameSource.BodyCount;
            this.bodies = new Body[this.bodyCount];
            this.bodyFrameReader = sensor.BodyFrameSource.OpenReader();
            this.bodyFrameReader.FrameArrived += this.OnBodyFrame;

            this.faceFrameSources = new FaceFrameSource[this.bodyCount];
            this.faceFrameReaders = new FaceFrameReader[this.bodyCount];
            this.faceFrameResults = new FaceFrameResult[this.bodyCount];

            FrameDescription colorDescription = sensor.ColorFrameSource.FrameDescription;

            this.drawingGroup = new DrawingGroup();
            this.facePointsSource = new DrawingImage(this.drawingGroup);
            this.displayRect = new Rect(0.0, 0.0, colorDescription.Width, colorDescription.Height);

            this.bitmap = new WriteableBitmap(colorDescription.Width, colorDescription.Height, 96.0, 96.0, PixelFormats.Bgra32, null);

            this.coordinateMapper = sensor.CoordinateMapper;

            this.faceFrameFeatures =
                FaceFrameFeatures.BoundingBoxInColorSpace
                | FaceFrameFeatures.PointsInColorSpace
                | FaceFrameFeatures.BoundingBoxInInfraredSpace
                | FaceFrameFeatures.PointsInInfraredSpace
                | FaceFrameFeatures.RotationOrientation
                | FaceFrameFeatures.FaceEngagement
                | FaceFrameFeatures.Glasses
                | FaceFrameFeatures.Happy
                | FaceFrameFeatures.LeftEyeClosed
                | FaceFrameFeatures.RightEyeClosed
                | FaceFrameFeatures.LookingAway
                | FaceFrameFeatures.MouthMoved
                | FaceFrameFeatures.MouthOpen;

            this.faceBrush = new List<Brush>()
            {
                Brushes.White,
                Brushes.Orange,
                Brushes.Green,
                Brushes.Red,
                Brushes.LightBlue,
                Brushes.Yellow
            };

            for (int i = 0; i < this.bodyCount; i++)
            {
                // create the face frame source with the required face frame features and an initial tracking Id of 0
                this.faceFrameSources[i] = new FaceFrameSource(sensor, 0, faceFrameFeatures);

                // open the corresponding reader
                this.faceFrameReaders[i] = this.faceFrameSources[i].OpenReader();
            }

            for (int i = 0; i < this.bodyCount; i++)
            {
                if (this.faceFrameReaders[i] != null)
                {
                    // wire handler for face frame arrival
                    this.faceFrameReaders[i].FrameArrived += this.OnFaceFrame;
                }
            }

        }

        public void OnColorFrame(object sender, ColorFrameArrivedEventArgs e)
        {
            using (ColorFrame frame = e.FrameReference.AcquireFrame())
            {
                // validate
                if (frame == null)
                {
                    return;
                }
                else
                {
                    var fd = frame.FrameDescription;
                    if (fd.Width != this.bitmap.PixelWidth || fd.Height != this.bitmap.PixelHeight)
                    {
                        return;
                    }
                }

                // set color
                using (KinectBuffer colorBuffer = frame.LockRawImageBuffer())
                {
                    this.bitmap.Lock();

                    frame.CopyConvertedFrameDataToIntPtr(
                        this.bitmap.BackBuffer,
                        (uint)(this.bitmap.PixelWidth * this.bitmap.PixelHeight * 4),
                        ColorImageFormat.Bgra);
                    this.bitmap.AddDirtyRect(new System.Windows.Int32Rect(0, 0, this.bitmap.PixelWidth, this.bitmap.PixelHeight));

                    this.bitmap.Unlock();

                }
            }
        }

        private void OnFaceFrame(object sender, FaceFrameArrivedEventArgs e)
        {
            using (FaceFrame faceFrame = e.FrameReference.AcquireFrame())
            {
                if (faceFrame != null)
                {
                    int index = Array.FindIndex(this.faceFrameSources, fs => fs == faceFrame.FaceFrameSource);
;                   
                    // check if this face frame has valid face frame results
                    if (this.ValidateFaceBoxAndPoints(faceFrame.FaceFrameResult))
                    {
                        // store this face frame result to draw later
                        this.faceFrameResults[index] = faceFrame.FaceFrameResult;
                    }
                    else
                    {
                        // indicates that the latest face frame result from this reader is invalid
                        this.faceFrameResults[index] = null;
                    }
                }
            }
        }

        private bool ValidateFaceBoxAndPoints(FaceFrameResult result)
        {
            if(result == null)
            {
                return false;
            }

            bool isFaceValid = true;

            var faceBox = result.FaceBoundingBoxInColorSpace;
            if (faceBox != null)
            {
                // check if we have a valid rectangle within the bounds of the screen space
                isFaceValid = (faceBox.Right - faceBox.Left) > 0 &&
                              (faceBox.Bottom - faceBox.Top) > 0 &&
                              faceBox.Right <= this.displayRect.Width &&
                              faceBox.Bottom <= this.displayRect.Height;

                if (isFaceValid)
                {
                    var facePoints = result.FacePointsInColorSpace;
                    if (facePoints != null)
                    {
                        foreach (PointF pointF in facePoints.Values)
                        {
                            // check if we have a valid face point within the bounds of the screen space
                            bool isFacePointValid = pointF.X > 0.0f &&
                                                    pointF.Y > 0.0f &&
                                                    pointF.X < this.displayRect.Width &&
                                                    pointF.Y < this.displayRect.Height;

                            if (!isFacePointValid)
                            {
                                isFaceValid = false;
                                break;
                            }
                        }
                    }
                }
            }

            return isFaceValid;
        }


        private void OnBodyFrame(object sender, BodyFrameArrivedEventArgs e)
        {
            using (var bodyFrame = e.FrameReference.AcquireFrame())
            {
                if (bodyFrame != null)
                {
                    // update body data
                    bodyFrame.GetAndRefreshBodyData(this.bodies);

                    using (DrawingContext dc = this.drawingGroup.Open())
                    {
                        bool drawFaceResult = false;
                        dc.DrawRectangle(null, new Pen(Brushes.AliceBlue, 1.0), this.displayRect);

                        for (int i = 0; i < this.bodyCount; i++)
                        {
                            // check if a valid face is tracked in this face source
                            if (this.faceFrameSources[i].IsTrackingIdValid)
                            {
                                // check if we have valid face frame results
                                if (this.faceFrameResults[i] != null)
                                {
                                    // draw face frame results
                                    this.DrawFaceFrameResults(i, this.faceFrameResults[i], dc);

                                    if (!drawFaceResult)
                                    {
                                        drawFaceResult = true;
                                    }
                                }
                            }
                            else
                            {
                                // check if the corresponding body is tracked 
                                if (this.bodies[i].IsTracked)
                                {
                                    // update the face frame source to track this body
                                    this.faceFrameSources[i].TrackingId = this.bodies[i].TrackingId;
                                }
                            }
                        }

                        this.drawingGroup.ClipGeometry = new RectangleGeometry(this.displayRect);
                        this.onUpdate();
                    }
                }
            }
        }

        private void DrawFaceFrameResults(int faceIndex, FaceFrameResult faceResult, DrawingContext drawingContext)
        {
            // choose the brush based on the face index
            Brush drawingBrush = this.faceBrush[0];
            if (faceIndex < this.bodyCount)
            {
                drawingBrush = this.faceBrush[faceIndex];
            }

            Pen drawingPen = new Pen(drawingBrush, DrawFaceShapeThickness);

            // draw the face bounding box
            var faceBoxSource = faceResult.FaceBoundingBoxInColorSpace;
            Rect faceBox = new Rect(faceBoxSource.Left, faceBoxSource.Top, faceBoxSource.Right - faceBoxSource.Left, faceBoxSource.Bottom - faceBoxSource.Top);
            drawingContext.DrawRectangle(null, drawingPen, faceBox);

            if (faceResult.FacePointsInColorSpace != null)
            {
                // draw each face point
                foreach (PointF pointF in faceResult.FacePointsInColorSpace.Values)
                {
                    drawingContext.DrawEllipse(null, drawingPen, new Point(pointF.X, pointF.Y), FacePointRadius, FacePointRadius);
                }
            }

            string faceText = string.Empty;

            // extract each face property information and store it in faceText
            if (faceResult.FaceProperties != null)
            {
                foreach (var item in faceResult.FaceProperties)
                {
                    faceText += item.Key.ToString() + " : ";

                    // consider a "maybe" as a "no" to restrict 
                    // the detection result refresh rate
                    if (item.Value == DetectionResult.Maybe)
                    {
                        faceText += DetectionResult.No + "\n";
                    }
                    else
                    {
                        faceText += item.Value.ToString() + "\n";
                    }
                }
            }

            // extract face rotation in degrees as Euler angles
            if (faceResult.FaceRotationQuaternion != null)
            {
                int pitch, yaw, roll;
                ExtractFaceRotationInDegrees(faceResult.FaceRotationQuaternion, out pitch, out yaw, out roll);
                faceText += "FaceYaw : " + yaw + "\n" +
                            "FacePitch : " + pitch + "\n" +
                            "FacenRoll : " + roll + "\n";
            }

        }

        private static void ExtractFaceRotationInDegrees(Vector4 rotQuaternion, out int pitch, out int yaw, out int roll)
        {
            double x = rotQuaternion.X;
            double y = rotQuaternion.Y;
            double z = rotQuaternion.Z;
            double w = rotQuaternion.W;

            // convert face rotation quaternion to Euler angles in degrees
            double yawD, pitchD, rollD;
            pitchD = Math.Atan2(2 * ((y * z) + (w * x)), (w * w) - (x * x) - (y * y) + (z * z)) / Math.PI * 180.0;
            yawD = Math.Asin(2 * ((w * y) - (x * z))) / Math.PI * 180.0;
            rollD = Math.Atan2(2 * ((x * y) + (w * z)), (w * w) + (x * x) - (y * y) - (z * z)) / Math.PI * 180.0;

            // clamp the values to a multiple of the specified increment to control the refresh rate
            double increment = FaceRotationIncrementInDegrees;
            pitch = (int)(Math.Floor((pitchD + ((increment / 2.0) * (pitchD > 0 ? 1.0 : -1.0))) / increment) * increment);
            yaw = (int)(Math.Floor((yawD + ((increment / 2.0) * (yawD > 0 ? 1.0 : -1.0))) / increment) * increment);
            roll = (int)(Math.Floor((rollD + ((increment / 2.0) * (rollD > 0 ? 1.0 : -1.0))) / increment) * increment);
        }

        public void Dispose()
        {
            for (int i = 0; i < this.bodyCount; i++)
            {
                if (this.faceFrameReaders[i] != null)
                {
                    // FaceFrameReader is IDisposable
                    this.faceFrameReaders[i].Dispose();
                    this.faceFrameReaders[i] = null;
                }

                if (this.faceFrameSources[i] != null)
                {
                    // FaceFrameSource is IDisposable
                    this.faceFrameSources[i].Dispose();
                    this.faceFrameSources[i] = null;
                }
            }

            if (this.bodyFrameReader != null)
            {
                // BodyFrameReader is IDisposable
                this.bodyFrameReader.Dispose();
                this.bodyFrameReader = null;
            }

        }
    }
}
