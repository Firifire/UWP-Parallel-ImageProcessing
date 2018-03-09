namespace ImageProcessing.Processors
{
    using System;
    using System.Threading.Tasks;
    using VideoDeviceFinders;
    using Windows.Devices.Enumeration;
    using Windows.Media.Capture;
    using Windows.Media.Capture.Frames;
    using Windows.Media.Devices;

    public abstract class MediaCaptureFrameProcessor<T> : IDisposable
    {
        public MediaCaptureFrameProcessor(
          MediaFrameSourceFinder mediaFrameSourceFinder,
          DeviceInformation videoDeviceInformation,
          string mediaEncodingSubtype,
          MediaCaptureMemoryPreference memoryPreference = MediaCaptureMemoryPreference.Cpu)
        {
            this.mediaFrameSourceFinder = mediaFrameSourceFinder;
            this.videoDeviceInformation = videoDeviceInformation;
            this.mediaEncodingSubtype = mediaEncodingSubtype;
            this.memoryPreference = memoryPreference;
        }
        public void SetVideoDeviceControllerInitialiser(
          Action<VideoDeviceController> initialiser)
        {
            this.videoDeviceControllerInitialiser = initialiser;
        }
        protected abstract bool ProcessFrame(MediaFrameReference frameReference, CameraCapture.ImageProcess processMethod);
        public T Result { get; protected set; }

        public async Task ProcessFramesAsync(
          TimeSpan? timeout, CameraCapture.ImageProcess processMethod, bool repeat,
          Action<T> resultCallback = null)
        {
            await Task.Run(
              async () =>
              {
                  var startTime = DateTime.Now;

                  this.Result = default(T);

                  if (this.mediaCapture == null)
                  {
                      this.mediaCapture = await this.CreateMediaCaptureAsync();
                  }
                  var mediaFrameSource = this.mediaCapture.FrameSources[
              this.mediaFrameSourceFinder.FrameSourceInfo.Id];

                  using (var frameReader =
              await this.mediaCapture.CreateFrameReaderAsync(
                mediaFrameSource, this.mediaEncodingSubtype))
                  {
                      bool done = false;

                      await frameReader.StartAsync();

                      while (!done)
                      {
                          using (var frame = frameReader.TryAcquireLatestFrame())
                          {
                              if (frame != null)
                              {
                                  if (this.ProcessFrame(frame, processMethod) && (resultCallback != null))
                                  {
                                      resultCallback(this.Result);
                                  }
                              }
                          }
                          if (timeout.HasValue)
                          {
                              var timedOut = (DateTime.Now - startTime) > timeout;

                              if (timedOut && (resultCallback != null))
                              {
                                  resultCallback(default(T));
                              }
                              done = (this.Result != null && !repeat) || (timedOut) || CameraCapture.Stop;
                          }
                          else
                              done = (this.Result != null && !repeat) || CameraCapture.Stop;
                      }
                      await frameReader.StopAsync();
                      CameraCapture.isRunning = false;
                  }
              }
            );
        }
        async Task<MediaCapture> CreateMediaCaptureAsync()
        {
            var settings = new MediaCaptureInitializationSettings()
            {
                VideoDeviceId = this.videoDeviceInformation.Id,
                SourceGroup = this.mediaFrameSourceFinder.FrameSourceGroup,
                MemoryPreference = this.memoryPreference
            };

            var mediaCapture = new MediaCapture();

            await mediaCapture.InitializeAsync(settings);

            this.videoDeviceControllerInitialiser?.Invoke(mediaCapture.VideoDeviceController);

            return (mediaCapture);
        }
        public void Dispose()
        {
            if (this.mediaCapture != null)
            {
                this.mediaCapture.Dispose();
                this.mediaCapture = null;
            }
        }
        Action<VideoDeviceController> videoDeviceControllerInitialiser;
        string mediaEncodingSubtype;
        MediaFrameSourceFinder mediaFrameSourceFinder;
        DeviceInformation videoDeviceInformation;
        MediaCaptureMemoryPreference memoryPreference;
        MediaCapture mediaCapture;
    }
}