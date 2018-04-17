namespace ImageProcessing.Processors
{
    using System;
    using System.Threading.Tasks;
    using VideoDeviceFinders;
    using Windows.Devices.Enumeration;
    using Windows.Media.Capture;
    using Windows.Media.Capture.Frames;
    using Windows.Media.Devices;
    using Windows.Media.MediaProperties;
    using System.Collections.Generic;
    using System.Linq;

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
          Action<T> resultCallback = null, Action preResultCallback = null)
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

                  var allStreamProperties =
        mediaCapture.VideoDeviceController.GetAvailableMediaStreamProperties(MediaStreamType.VideoPreview).Select(x => new StreamPropertiesHelper(x));
                  allStreamProperties = allStreamProperties.OrderByDescending(x => x.Height * x.Width);
                  await mediaCapture.VideoDeviceController.SetMediaStreamPropertiesAsync(MediaStreamType.VideoPreview, allStreamProperties.ElementAt(0).EncodingProperties);

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
                              preResultCallback?.Invoke();
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

        class StreamPropertiesHelper
        {
            private IMediaEncodingProperties _properties;

            public StreamPropertiesHelper(IMediaEncodingProperties properties)
            {
                if (properties == null)
                {
                    throw new ArgumentNullException(nameof(properties));
                }

                // This helper class only uses VideoEncodingProperties or VideoEncodingProperties
                if (!(properties is ImageEncodingProperties) && !(properties is VideoEncodingProperties))
                {
                    throw new ArgumentException("Argument is of the wrong type. Required: " + typeof(ImageEncodingProperties).Name
                        + " or " + typeof(VideoEncodingProperties).Name + ".", nameof(properties));
                }

                // Store the actual instance of the IMediaEncodingProperties for setting them later
                _properties = properties;
            }

            public uint Width
            {
                get
                {
                    if (_properties is ImageEncodingProperties)
                    {
                        return (_properties as ImageEncodingProperties).Width;
                    }
                    else if (_properties is VideoEncodingProperties)
                    {
                        return (_properties as VideoEncodingProperties).Width;
                    }

                    return 0;
                }
            }

            public uint Height
            {
                get
                {
                    if (_properties is ImageEncodingProperties)
                    {
                        return (_properties as ImageEncodingProperties).Height;
                    }
                    else if (_properties is VideoEncodingProperties)
                    {
                        return (_properties as VideoEncodingProperties).Height;
                    }

                    return 0;
                }
            }

            public uint FrameRate
            {
                get
                {
                    if (_properties is VideoEncodingProperties)
                    {
                        if ((_properties as VideoEncodingProperties).FrameRate.Denominator != 0)
                        {
                            return (_properties as VideoEncodingProperties).FrameRate.Numerator /
                                (_properties as VideoEncodingProperties).FrameRate.Denominator;
                        }
                    }

                    return 0;
                }
            }

            public double AspectRatio
            {
                get { return Math.Round((Height != 0) ? (Width / (double)Height) : double.NaN, 2); }
            }

            public IMediaEncodingProperties EncodingProperties
            {
                get { return _properties; }
            }

            public string GetFriendlyName(bool showFrameRate = true)
            {
                if (_properties is ImageEncodingProperties ||
                    !showFrameRate)
                {
                    return Width + "x" + Height + " [" + AspectRatio + "] " + _properties.Subtype;
                }
                else if (_properties is VideoEncodingProperties)
                {
                    return Width + "x" + Height + " [" + AspectRatio + "] " + FrameRate + "FPS " + _properties.Subtype;
                }

                return String.Empty;
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