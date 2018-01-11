using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using System.Runtime.InteropServices;
using Windows.Graphics.Imaging;
using Windows.Media;
using Windows.Media.Capture;
using Windows.Media.FaceAnalysis;
using Windows.Media.MediaProperties;
using Windows.System.Threading;
using UnityPlayer;

namespace HoloFaceSamples
{
    [ComImport]
    [Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    unsafe interface IMemoryBufferByteAccess
    {
        void GetBuffer(out byte* buffer, out uint capacity);
    }
    internal class FaceTrackerService : FaceDetectBase
    {
        public delegate Task SetMediaCaptureObjectAsync(MediaCapture capture);
        private readonly MediaCapture _capture;
        private FaceTracker faceTracker;
        private ThreadPoolTimer frameProcessingTimer;
        private SemaphoreSlim frameProcessingSemaphore = new SemaphoreSlim(1);
        private IList<DetectedFace> detectedFaces;
        private SoftwareBitmap bitmapRGBA;
        public FaceTrackerService(MediaCapture capture)
        {
            _capture = capture;
        }
        public static async Task InitializeServiceAsync(SetMediaCaptureObjectAsync action)
        {
            var devices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
            var device = devices[0];
            var capture = new MediaCapture();
            var settings = new MediaCaptureInitializationSettings
            {
                VideoDeviceId = device.Id
            };
            await capture.InitializeAsync(settings);
            await action(capture);
            var service = new FaceTrackerService(capture);
            UWPBridgeServiceManager.Instance.AddService<FaceDetectBase>(service);
        }
        public override void Start()
        {
            InitializeFaceTracker();
        }
        public async void InitializeFaceTracker()
        {
            faceTracker = await FaceTracker.CreateAsync();
            TimeSpan timerInterval = TimeSpan.FromMilliseconds(66); // 15 fps
            frameProcessingTimer = Windows.System.Threading.ThreadPoolTimer.CreatePeriodicTimer(new Windows.System.Threading.TimerElapsedHandler(ProcessCurrentVideoFrame), timerInterval);
        }
        public async void ProcessCurrentVideoFrame(ThreadPoolTimer timer)
        {
            if (!frameProcessingSemaphore.Wait(0))
            {
                return;
            }
            VideoFrame currentFrame = await GetLatestFrame();
            bitmapRGBA = SoftwareBitmap.Convert(currentFrame.SoftwareBitmap, BitmapPixelFormat.Rgba8, BitmapAlphaMode.Premultiplied);
            const BitmapPixelFormat faceDetectionPixelFormat = BitmapPixelFormat.Nv12;

            if (currentFrame.SoftwareBitmap.BitmapPixelFormat != faceDetectionPixelFormat)
            {
                System.Diagnostics.Debug.WriteLine("Not valid format");
                return;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine("Tracking");
                detectedFaces = await faceTracker.ProcessNextFrameAsync(currentFrame);

                var previewFrameSize = new Windows.Foundation.Size(currentFrame.SoftwareBitmap.PixelWidth, currentFrame.SoftwareBitmap.PixelHeight);
            }
            catch (Exception e)
            {
                // Face tracking failed
            }
            finally
            {
                frameProcessingSemaphore.Release();
            }

            currentFrame.Dispose();
        }
        public async Task<VideoFrame> GetLatestFrame()
        {
            var previewProperties = _capture.VideoDeviceController.GetMediaStreamProperties(MediaStreamType.VideoPreview) as VideoEncodingProperties;
            this.FrameSizeWidth = (int)previewProperties.Width;
            this.FrameSizeHeight = (int)previewProperties.Height;
            VideoFrame videoFrame = new VideoFrame(BitmapPixelFormat.Nv12, (int)previewProperties.Width, (int)previewProperties.Height);
            VideoFrame previewFrame = await _capture.GetPreviewFrameAsync(videoFrame);
            return previewFrame;
        }
        public override void DetectFace()
        {
            AppCallbacks.Instance.InvokeOnUIThread(async () =>
            {
                if (detectedFaces == null || bitmapRGBA == null)
                {
                    System.Diagnostics.Debug.WriteLine("NoFace");
                    AppCallbacks.Instance.InvokeOnAppThread(() => { }, false);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine(detectedFaces.ToString());
                    if (bitmapRGBA == null)
                    {
                        AppCallbacks.Instance.InvokeOnAppThread(() => { }, false);
                    }
                    else
                    {
                        /*
                        var faceInformations = detectedFaces.Select(x => new FaceInformation
                        {
                            Width = calc_length(x.FaceBox.X, 3 * x.FaceBox.Width, bitmapRGBA.PixelWidth),
                            Height = calc_length(x.FaceBox.Y, 3 * x.FaceBox.Height, bitmapRGBA.PixelHeight),
                            X = x.FaceBox.X + 0.5f * calc_length(x.FaceBox.X, 3 * x.FaceBox.Width, bitmapRGBA.PixelWidth),
                            //Y = x.FaceBox.Y,
                            Y = x.FaceBox.Y + 0.5f * calc_length(x.FaceBox.Y, 3 * x.FaceBox.Height, bitmapRGBA.PixelHeight),
                            RawData = CropImage(bitmapRGBA, x, 3)
                        }).ToList();
                        */
                        var faceInformations = detectedFaces.Select(x => CropFaceImage(bitmapRGBA, x, 3.0f)).ToList();
                        AppCallbacks.Instance.InvokeOnAppThread(() => { OnDetected(faceInformations); }, false);
                    }
                }
            }, true);
        }
        unsafe FaceInformation CropFaceImage(SoftwareBitmap softwareBitmap, DetectedFace face, float scale)
        {
            int centerx = (int)(face.FaceBox.X) + (int)(0.5f * face.FaceBox.Width);
            int centery = (int)(face.FaceBox.Y) + (int)(0.5f * face.FaceBox.Height);
            int startx = centerx - (int)(scale * face.FaceBox.Width * 0.5f);
            int endx = centerx + (int)(scale * face.FaceBox.Width * 0.5f);
            int starty = centery - (int)(scale * face.FaceBox.Height * 0.5f);
            int endy = centery + (int)(scale * face.FaceBox.Height * 0.5f);
            int width = endx - startx;
            int height = endy - starty;
            byte[] crop_data = new byte[4 * width * height];
            using (BitmapBuffer buffer = softwareBitmap.LockBuffer(BitmapBufferAccessMode.Write))
            {
                using (var reference = buffer.CreateReference())
                {
                    byte* dataInBytes;
                    uint capacity;
                    ((IMemoryBufferByteAccess)reference).GetBuffer(out dataInBytes, out capacity);

                    // Fill-in the BGRA plane
                    BitmapPlaneDescription bufferLayout = buffer.GetPlaneDescription(0);
                    for (int i = starty; i < endy; i++)
                    {
                        for (int j = startx; j < endx; j++)
                        {
                            int x1 = j - startx;
                            int y1 = i - starty;
                            int x2 = j;
                            int y2 = endy - 1 - i + starty;
                            if (x2 < 0 || x2 >= bufferLayout.Width) continue;
                            if (y2 < 0 || y2 >= bufferLayout.Height) continue;
                            crop_data[4 * ((i - starty) * width + (j - startx)) + 0] = dataInBytes[bufferLayout.StartIndex + bufferLayout.Stride * (endy - 1 - i + starty) + 4 * j + 0];
                            crop_data[4 * ((i - starty) * width + (j - startx)) + 1] = dataInBytes[bufferLayout.StartIndex + bufferLayout.Stride * (endy - 1 - i + starty) + 4 * j + 1];
                            crop_data[4 * ((i - starty) * width + (j - startx)) + 2] = dataInBytes[bufferLayout.StartIndex + bufferLayout.Stride * (endy - 1 - i + starty) + 4 * j + 2];
                            crop_data[4 * ((i - starty) * width + (j - startx)) + 3] = dataInBytes[bufferLayout.StartIndex + bufferLayout.Stride * (endy - 1 - i + starty) + 4 * j + 3];
                        }
                    }
                }
            }
            return new FaceInformation
            {
                Width = width,
                Height = height,
                X = centerx,
                Y = centery,
                RawData = crop_data
            };
        }

        int calc_length(uint start, uint len, int max)
        {
            if (start + len >= max)
            {
                return max - 1 - (int)start;
            } else
            {
                return (int)len;
            }
        }

        unsafe public byte[] CropImage(SoftwareBitmap softwareBitmap, DetectedFace face, int scale)
        {
            int centerx = (int)(face.FaceBox.X) + (int)(0.5f * face.FaceBox.Width);
            int centery = (int)(face.FaceBox.Y) + (int)(0.5f * face.FaceBox.Height);
            int startx = (int)(face.FaceBox.X);
            int endx = (int)(face.FaceBox.X + scale * face.FaceBox.Width);
            if (endx >= softwareBitmap.PixelWidth)
            {
                endx = softwareBitmap.PixelWidth - 1;
            }
            int starty = (int)(face.FaceBox.Y);
            int endy = (int)(face.FaceBox.Y + scale * face.FaceBox.Height);
            if (endy >= softwareBitmap.PixelHeight)
            {
                endy = softwareBitmap.PixelHeight - 1;
            }
            int width = endx - startx;
            int height = endy - starty;
            byte[] crop_data = new byte[4 * width * height];
            using (BitmapBuffer buffer = softwareBitmap.LockBuffer(BitmapBufferAccessMode.Write))
            {
                using (var reference = buffer.CreateReference())
                {
                    byte* dataInBytes;
                    uint capacity;
                    ((IMemoryBufferByteAccess)reference).GetBuffer(out dataInBytes, out capacity);

                    // Fill-in the BGRA plane
                    BitmapPlaneDescription bufferLayout = buffer.GetPlaneDescription(0);
                    for (int i = starty; i < endy; i++)
                    {
                        for (int j = startx; j < endx; j++)
                        {
                            if (i > bufferLayout.Height || j > bufferLayout.Width) continue;
                            crop_data[4 * ((i - starty) * width + (j - startx)) + 0] = dataInBytes[bufferLayout.StartIndex + bufferLayout.Stride * (endy - 1 - i + starty) + 4 * j + 0];
                            crop_data[4 * ((i - starty) * width + (j - startx)) + 1] = dataInBytes[bufferLayout.StartIndex + bufferLayout.Stride * (endy - 1 - i + starty) + 4 * j + 1];
                            crop_data[4 * ((i - starty) * width + (j - startx)) + 2] = dataInBytes[bufferLayout.StartIndex + bufferLayout.Stride * (endy - 1 - i + starty) + 4 * j + 2];
                            crop_data[4 * ((i - starty) * width + (j - startx)) + 3] = dataInBytes[bufferLayout.StartIndex + bufferLayout.Stride * (endy - 1 - i + starty) + 4 * j + 3];
                        }
                    }
                }
            }
            return crop_data;
        }
    }
}
