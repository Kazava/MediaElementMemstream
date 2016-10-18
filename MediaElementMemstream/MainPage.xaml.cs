using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
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
using System.Runtime.InteropServices.WindowsRuntime;

//this link has some useful info
//https://social.msdn.microsoft.com/Forums/en-US/b169961a-0fa6-40aa-9c2d-55cc72e5db59/how-to-trigger-mediastreamsourcesamplerequested-event-when-the-h264-data-is-received-from-our?forum=wpdevelop


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
        MpegTS.BufferExtractor extractor;
        private volatile bool running;

        public MainPage()
        {
            this.InitializeComponent();

            var videoProperties = VideoEncodingProperties.CreateH264();//.CreateUncompressed(MediaEncodingSubtypes.H264, 720, 480);
            var vd = VideoEncodingProperties.CreateUncompressed(MediaEncodingSubtypes.H264, 720, 480);
            videoDesc = new VideoStreamDescriptor(vd);
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

        bool gotT0 = false;
        private TimeSpan T0;
        uint frameCount = 0;
        MediaStreamSample emptySample = MediaStreamSample.CreateFromBuffer(new Windows.Storage.Streams.Buffer(0), new TimeSpan(0));

        
        private void Mss_SampleRequested(MediaStreamSource sender, MediaStreamSourceSampleRequestedEventArgs args)
        {
            if (!(args.Request.StreamDescriptor is VideoStreamDescriptor))
                return;

            Debug.WriteLine("requesting sample");
            MediaStreamSourceSampleRequest request = args.Request;

            // check if the sample requested byte offset is within the file size

            //if (byteOffset + sampleSize <= mssStream.Size)
            {
                MediaStreamSourceSampleRequestDeferral deferal = request.GetDeferral();

                //block here for signal from mpegTS parser that a sample is ready
                //if (extractor.SampleCount == 0)
                //    threadSync.WaitOne();

                //dequeue the raw sample here
                MpegTS.VideoSample rawSample = null;

                //do
                //{
                //    threadSync.WaitOne();
                    rawSample = extractor.DequeueNextSample(false);
                //}
                //while (rawSample == null || extractor.SampleCount == 0);
                if (rawSample == null)

                {
                    request.Sample = emptySample;
                    deferal.Complete();

                    return;
                }

                if(!gotT0)
                {
                    gotT0 = true;
                    T0 = new TimeSpan(333667);
                    //T0.TotalMilliseconds = 33.3667;
                }

                //check max size of current buffer, increase if needed.
                if (buff.Capacity < rawSample.Length)
                    buff = new Windows.Storage.Streams.Buffer( (uint)rawSample.Length );

                //create our sample here may need to keep initial time stamp for relative time?
                var sample = MediaStreamSample.CreateFromBuffer(buff, new TimeSpan(T0.Ticks * frameCount));

                var bStream = sample.Buffer.AsStream();
                bStream.Position = 0;

                //write the raw sample to the reqest sample stream;
                rawSample.WriteToStream(bStream);

                sample.Buffer.Length = (uint)rawSample.Length;
                Debug.WriteLine("sample length: {0}", rawSample.Length);
                //sample.DecodeTimestamp = new TimeSpan(T0.Ticks * frameCount);
                sample.Duration = T0;
                sample.KeyFrame = rawSample.Length > 3000;

                //
                ++frameCount;

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

            Debug.WriteLine("exit request sample");
        }

        private void mss_Starting(MediaStreamSource sender, MediaStreamSourceStartingEventArgs args)
        {
            extractor = new MpegTS.BufferExtractor();

            running = true;
            frameCount = 0;
            //_sampleGenerator.Initialize(_mss, videoDesc);
            args.Request.SetActualStartPosition(new TimeSpan(0));

            Task.Run(() => RunreadFromFile());
        }

        private async void RunreadFromFile()
        {
            var dir = (await KnownFolders.RemovableDevices.GetFoldersAsync()).ToList();
            var f = await dir.FirstOrDefault(file => file.Name == @"D:\").GetFileAsync(@"extSdCard\Video_2014_5_7__15_33_44.mpg");

            var fStream = (await f.OpenReadAsync()).AsStream();//let's use a standard stream
            long eof = fStream.Length - MpegTS.TsPacket.PacketLength;
            byte[] b;

            do
            {
                if (fStream.Position < eof)
                {
                    b = extractor.GetBuffer();

                    await fStream.ReadAsync(b, 0, b.Length).ConfigureAwait(false);

                    if(!extractor.AddRaw(b))
                    {//the chunk we read was not mpegTS data, or we need to find the sync byte
                        int syncbytePos = MpegTS.TsPacket.PacketLength - b.ToList().IndexOf(MpegTS.TsPacket.SyncByte);

                        if(syncbytePos < MpegTS.TsPacket.PacketLength)
                            fStream.Position -= syncbytePos;//try to re-sync the stream cursor
                    }

                    if (extractor.SampleCount > 0)
                    {
                        if (extractor.SampleCount > 1)
                            await Task.Delay(20).ConfigureAwait(false);//slow down the extractor, no need to pre-load too much
                    }
                }
                else
                    fStream.Position = 0;//go to start of file


            } while (running);
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
