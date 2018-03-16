namespace ImageProcessing.Processors
{
    using System;
    using System.Collections.Generic;
    using ImageProcessing.VideoDeviceFinders;
    using System.Runtime.InteropServices.WindowsRuntime;
    using Windows.Devices.Enumeration;
    using Windows.Media.Capture;
    using Windows.Media.Capture.Frames;
    using Windows.Graphics.Imaging;
    using Windows.Storage;
    using Windows.Storage.Streams;
    using Windows.Storage.Pickers;
    using OpenCvSharp;
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
                        sourceImageData = new Vec3b[bitmap.PixelHeight * bitmap.PixelWidth];
                    }
                    if (processMethod == null)
                    {
                        Result = new object[1] { 1 };
                        var task =  SaveSoftwareBitmapToFile();
                        task.Wait();
                    }
                    else
                    {
                        bitmap.CopyToBuffer(buffer.AsBuffer());
                        sourceImage = new Mat(bitmap.PixelHeight, bitmap.PixelWidth, MatType.CV_8UC4, buffer);
                        //Cv2.CvtColor(sourceImage, sourceImage, ColorConversionCodes.BGRA2BGR); //<Remove>
                        processMethod(sourceImage, out _result);
                        if (_result != null)
                            Result = _result;
                    }
                }
                catch
                {
                }
            }
            return (this.Result != null);
        }
        Mat sourceImage;
        Vec3b[] sourceImageData;
        SoftwareBitmap bitmap;
        byte[] buffer = null;
        static int i = 0;
        private async Task SaveSoftwareBitmapToFile()
        {

            //string filename = string.Format(@"CapturedImage{0}_n.jpg", i++);
            //string filePath = System.IO.Path.Combine("C:/Data/Users/firif/AppData/Local/Packages/DataAugmentor_pzq3xp76mxafg/LocalState", filename);

            var myPictures = Windows.Storage.ApplicationData.Current.LocalFolder;
            StorageFile file = await myPictures.CreateFileAsync("photo" + i + ".jpg", CreationCollisionOption.GenerateUniqueName);
            //var myPictures = await Windows.Storage.StorageLibrary.GetLibraryAsync(Windows.Storage.KnownLibraryId.Pictures);
            //StorageFile file = await myPictures.SaveFolder.CreateFileAsync("photo.jpg", CreationCollisionOption.GenerateUniqueName);
            using (IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.ReadWrite))
            {
                // Create an encoder with the desired format
                BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, stream);
                //softwareBitmap.CopyTo(abitmap);
                // Set the software bitmap
                encoder.SetSoftwareBitmap(bitmap);

                try
                {
                    await encoder.FlushAsync();
                }
                catch (Exception err)
                {
                    switch (err.HResult)
                    {
                        case unchecked((int)0x88982F81): //WINCODEC_ERR_UNSUPPORTEDOPERATION
                                                         // If the encoder does not support writing a thumbnail, then try again
                                                         // but disable thumbnail generation.
                            encoder.IsThumbnailGenerated = false;
                            break;
                        default:
                            throw err;
                    }
                }
                if (encoder.IsThumbnailGenerated == false)
                {
                    try
                    {
                        await encoder.FlushAsync();
                    }
                    catch { }
                }

            }
        }

    }
}