namespace ImageProcessing.Processors
{
    using ImageProcessing.VideoDeviceFinders;
    using System.Runtime.InteropServices.WindowsRuntime;
    using Windows.Devices.Enumeration;
    using Windows.Media.Capture;
    using Windows.Media.Capture.Frames;
    using OpenCvHololens;
    using System.Threading.Tasks;

    public class CaptureFrameProcessor : MediaCaptureFrameProcessor<object[]>
    {
        object[] _result;
        public CaptureFrameProcessor(
          MediaFrameSourceFinder mediaFrameSourceFinder,
          DeviceInformation videoDeviceInformation,
          string mediaEncodingSubtype,
          MediaCaptureMemoryPreference memoryPreference = MediaCaptureMemoryPreference.Cpu)

          : base(
              mediaFrameSourceFinder,
              videoDeviceInformation,
              mediaEncodingSubtype,
              memoryPreference)
        {
        }
        protected override bool ProcessFrame(MediaFrameReference frameReference, CameraCapture.ImageProcess processMethod)
        {
            this.Result = null;
            _result = null;
            // doc here https://msdn.microsoft.com/en-us/library/windows/apps/xaml/windows.media.capture.frames.videomediaframe.aspx
            // says to dispose this softwarebitmap if you access it.
            using (bitmap = frameReference.VideoMediaFrame.SoftwareBitmap)
            {
                try
                {
                   
                    if (this.buffer == null)
                    {
                        this.buffer = new byte[4 * bitmap.PixelHeight * bitmap.PixelWidth];
                    }
                    bitmap.CopyToBuffer(buffer.AsBuffer());
                    Vec3b[] sourceImageData = new Vec3b[bitmap.PixelHeight * bitmap.PixelWidth];
                    Mat sourceImage = new Mat(bitmap.PixelHeight, bitmap.PixelWidth, MatType.CV_8UC3);
#if !WINDOWS_UWP
                    ParallelU.For(0, imHeight, i => {
#else
                    Parallel.For(0, bitmap.PixelHeight, i => {
#endif
                        for (var j = 0; j < bitmap.PixelWidth; j++)
                        {
                            var vec3 = new Vec3b
                            {
                                Item0 = buffer[(j + i * bitmap.PixelWidth) * 4 + 0],
                                Item1 = buffer[(j + i * bitmap.PixelWidth) * 4 + 1],
                                Item2 = buffer[(j + i * bitmap.PixelWidth) * 4 + 2]
                            };
                            // set pixel to an array
                            sourceImageData[j + i * bitmap.PixelWidth] = vec3;
                        }
                    });
                    sourceImage.SetArray(0, 0, sourceImageData);
                    processMethod(sourceImage, out _result);
                    if (_result != null)
                        Result = _result;
                }
                catch
                {
                }
            }
            return (this.Result != null);
        }
        Windows.Graphics.Imaging.SoftwareBitmap bitmap;
        byte[] buffer = null;
    }
}