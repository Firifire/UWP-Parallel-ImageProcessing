namespace ImageProcessing
{
    using System;
    using ImageProcessing.Processors;
    using ImageProcessing.VideoDeviceFinders;
    using Windows.Media.MediaProperties;
    using OpenCvSharp;

    public static class CameraCapture
    {
        public delegate void ImageProcess(Mat src, out object[] result);
        public static async void StartImageProcessing(
      Action<object[]> resultCallback, Action preResultCallback,
      TimeSpan? timeout, ImageProcess processMethod, bool repeat)
        {
            object[] result;

            if (frameProcessor == null)
            {
                var mediaFrameSourceFinder = new MediaFrameSourceFinder();

                var populated = await mediaFrameSourceFinder.PopulateAsync(
                  MediaFrameSourceFinder.ColorVideoPreviewFilter,
                  MediaFrameSourceFinder.FirstOrDefault);

                if (populated)
                {
                    // We'll take the first video capture device.
                    var videoCaptureDevice =
                      await CaptureDeviceFinder.FindFirstOrDefaultAsync();

                    if (videoCaptureDevice != null)
                    {
                        // Make a processor which will pull frames from the camera and run
                        frameProcessor = new CaptureFrameProcessor(
                          mediaFrameSourceFinder,
                          videoCaptureDevice,
                          MediaEncodingSubtypes.Bgra8);

                        // Remember to ask for auto-focus on the video capture device.
                        frameProcessor.SetVideoDeviceControllerInitialiser(
                          vd => vd.Focus.TrySetAuto(true));
                    }
                }
            }
            if (frameProcessor != null)
            {
                isRunning = true;
                await frameProcessor.ProcessFramesAsync(timeout, processMethod, repeat, resultCallback, preResultCallback);
                
                result = frameProcessor.Result;
            }
        }
        static CaptureFrameProcessor frameProcessor;
        public static bool isRunning = false;
        public static bool Stop = false;
    }
}