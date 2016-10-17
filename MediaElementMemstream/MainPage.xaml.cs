using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.Core;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Debug = System.Diagnostics.Debug;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace MediaElementMemstream
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        VideoStreamDescriptor videoDesc;
        private Windows.Media.Core.MediaStreamSource mss = null;
        Windows.Storage.Streams.Buffer buff;

        public MainPage()
        {
            this.InitializeComponent();

            var videoProperties = VideoEncodingProperties.CreateH264();//.CreateUncompressed(MediaEncodingSubtypes.H264, 720, 480);
            var vd = VideoEncodingProperties.CreateUncompressed(MediaEncodingSubtypes.H264, 720, 480);
            videoDesc = new VideoStreamDescriptor(videoProperties);
            videoDesc.EncodingProperties.FrameRate.Numerator = 29970;
            videoDesc.EncodingProperties.FrameRate.Denominator = 1000;
            videoDesc.EncodingProperties.Width = 720;
            videoDesc.EncodingProperties.Height = 480;

            mss = new MediaStreamSource(videoDesc);
            mss.CanSeek = false;
            //mss.BufferTime = new TimeSpan(0, 0, 0, 0, 250);
            mss.Starting += mss_Starting;
            mss.SampleRequested += Mss_SampleRequested;
            mss.SampleRendered += Mss_SampleRendered;

            buff = new Windows.Storage.Streams.Buffer(1024*4);
        }

        private void Mss_SampleRendered(MediaStreamSource sender, MediaStreamSourceSampleRenderedEventArgs args)
        {
            Debug.WriteLine("sample rendered event");
        }

        private void Mss_SampleRequested(MediaStreamSource sender, MediaStreamSourceSampleRequestedEventArgs args)
        {
            MediaStreamSourceSampleRequest request = args.Request;

            // check if the sample requested byte offset is within the file size

            //if (byteOffset + sampleSize <= mssStream.Size)
            {
                MediaStreamSourceSampleRequestDeferral deferal = request.GetDeferral();

                //block here for signal from mpegTS parser that a sample is ready

                //dequeue the raw sample here

                //check max size of current buffer, increase if needed.
                if (buff.Length < 1)
                    buff = new Windows.Storage.Streams.Buffer( 1 );

                //create our sample here may need to keep initial time stamp for relative time?
                var sample = MediaStreamSample.CreateFromBuffer(buff, new TimeSpan(0));

                //write the raw sample to the reqest sample stream;

                //inputStream = mssStream.GetInputStreamAt(byteOffset);

                // create the MediaStreamSample and assign to the request object. 
                // You could also create the MediaStreamSample using createFromBuffer(...)

                //MediaStreamSample sample = await MediaStreamSample.CreateFromStreamAsync(inputStream, sampleSize, timeOffset);
                //sample.Duration = sampleDuration;
                //sample.KeyFrame = true;

                // increment the time and byte offset

                //byteOffset += sampleSize;
                //timeOffset = timeOffset.Add(sampleDuration);
                request.Sample = sample;
                deferal.Complete();
            }
        }

        private void mss_Starting(MediaStreamSource sender, MediaStreamSourceStartingEventArgs args)
        {
            //_sampleGenerator.Initialize(_mss, videoDesc);
            args.Request.SetActualStartPosition(new TimeSpan(0));
        }

        private async void button_Click(object sender, RoutedEventArgs e)
        {
            initStream();
        }

        private void initStream()
        {

            mediaElement.SetMediaStreamSource(mss);
        }

        private async void foo()
        {
            var dir = (await KnownFolders.RemovableDevices.GetFoldersAsync()).ToList();
            var f = await dir.FirstOrDefault(file => file.Name == @"D:\").GetFileAsync(@"extSdCard\Video_2014_5_7__15_33_44.mpg");//.GetFileAsync(@"extSdCard\Video_2014_5_6__16_51_56.mp4");//
            var props = await f.Properties.RetrievePropertiesAsync(new string[]
                                                                    {
                                                                        "System.Video.EncodingBitrate",
                                                                        "System.Video.SampleSize",
                                                                        "System.Video.FrameRate",
                                                                        "System.Video.FourCC",
                                                                        "System.Video.FrameWidth",
                                                                        "System.Video.FrameHeight",
                                                                    });
            string code = System.Text.UTF8Encoding.UTF8.GetString(BitConverter.GetBytes((uint)props["System.Video.FourCC"]));
            var fStream = await f.OpenReadAsync();
            
            mediaElement.Position = new TimeSpan(0);

            //mediaElement.Source = new Uri(@"file://d:/extSdCard/Video_2014_5_6__16_51_56.mp4");
            mediaElement.SetSource(fStream, code);

            //Windows.Media.Core.MediaStreamSource ms = new Windows.Media.Core.MediaStreamSource();
            //var uri = new Uri("file:///extSdCard/Video_2014_5_7__15_33_44.mpg");
            //var resp = await Windows.Media.Streaming.Adaptive.AdaptiveMediaSource.CreateFromUriAsync(uri);

            //if (resp.Status == Windows.Media.Streaming.Adaptive.AdaptiveMediaSourceCreationStatus.Success)
            //    mediaElement.SetMediaStreamSource(resp.MediaSource);


            mediaElement.Play();
        }
    }
}
