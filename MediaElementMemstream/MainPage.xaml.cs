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

//more reference links
//http://www.itu.int/rec/T-REC-H.264-201304-S

//http://stackoverflow.com/questions/1685494/what-does-this-h264-nal-header-mean 

//http://stackoverflow.com/questions/1957427/detect-mpeg4-h264-i-frame-idr-in-rtp-stream

//another source reference:
//https://code.msdn.microsoft.com/MediaStreamSource-media-dfd55dff#content

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
        System.Threading.AutoResetEvent threadSync;

        private volatile bool running;
        private List<MediaStreamSample> SampleRecycler;
        //System.Collections.Concurrent.<> bag;
        private System.Collections.Concurrent.ConcurrentQueue<MediaStreamSample> SampleQ;


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

            //initialize some buffers
            buff = new Windows.Storage.Streams.Buffer(1024*4);
            bStream = buff.AsStream();

            //this seems needed for start-up
            threadSync = new System.Threading.AutoResetEvent(false);

            //get the frame time in ms
            double ms = 1000.0 * videoDesc.EncodingProperties.FrameRate.Denominator / videoDesc.EncodingProperties.FrameRate.Numerator;
            //get the frame time in ticks
            Tf = System.TimeSpan.FromTicks((long)(ms * System.TimeSpan.TicksPerMillisecond));

            SampleRecycler = new List<MediaStreamSample>(10);
            SampleQ = new System.Collections.Concurrent.ConcurrentQueue<MediaStreamSample>();

            //our demuxer
            extractor = new MpegTS.BufferExtractor();
            running = true;

            //give the file IO a head start
            Task.Run(() => RunreadFromFile());
        }

        private void Mss_SampleRendered(MediaStreamSource sender, MediaStreamSourceSampleRenderedEventArgs args)
        {
            foundKeyFrame = true;

            Debug.WriteLine("sample rendered event");
        }

        bool lastFrame, foundKeyFrame, gotT0 = false;
        private TimeSpan Tf, T0, Tn;
        uint frameCount = 0;
        MediaStreamSample emptySample = MediaStreamSample.CreateFromBuffer(new Windows.Storage.Streams.Buffer(0), new TimeSpan(0));
        Stream bStream;
        MediaStreamSample sample;

        private void Mss_SampleRequested(MediaStreamSource sender, MediaStreamSourceSampleRequestedEventArgs args)
        {
            if (!(args.Request.StreamDescriptor is VideoStreamDescriptor))
                return;

            Debug.WriteLine("requesting sample");
            MediaStreamSourceSampleRequest request = args.Request;

            MpegTS.VideoSample rawSample = null;
            MediaStreamSourceSampleRequestDeferral deferal = request.GetDeferral();

            try
            {
                //block here for signal from mpegTS parser that a sample is ready
                //if (extractor.SampleCount == 0)
                //    threadSync.WaitOne();

                //dequeue the raw sample here
                //if (!foundKeyFrame)
                //{
                    //do
                    //{
                    //    threadSync.WaitOne();
                    //    rawSample = extractor.DequeueNextSample(false);
                    //}
                    //while (rawSample == null || extractor.SampleCount == 0);
                //}
                //else 
                //{
                //    if(extractor.SampleCount > 0)
                //        rawSample = extractor.DequeueNextSample(false);

                //    if (rawSample == null)
                //    {
                //        Debug.WriteLine("return empty sample.");
                //        request.Sample = emptySample;
                //        deferal.Complete();

                //        return;
                //    }
                //}

                //if (!gotT0)
                //{
                //    gotT0 = true;
                //    //T0.TotalMilliseconds = 33.3667;
                //    Tn = T0 = TimeSpan.FromTicks(rawSample.PresentationTimeStamp*100);
                //}

                //sample = GetSample(rawSample.Length);

                //check max size of current buffer, increase if needed.
                //if (!foundKeyFrame)
                //{
                //if (buff.Capacity < rawSample.Length)
                //{
                //    buff = new Windows.Storage.Streams.Buffer((uint)rawSample.Length);
                //    bStream.Dispose();
                //    //bStream = sample.Buffer.AsStream();
                //    bStream = buff.AsStream();
                //}

                //////create our sample here may need to keep initial time stamp for relative time?
                //sample = MediaStreamSample.CreateFromBuffer(buff, new TimeSpan(Tf.Ticks * frameCount));
                //}
                //else
                bool gotSam = false;
                MediaStreamSample s;
                do
                {
                    gotSam = SampleQ.TryDequeue(out s);
                } while (!gotSam);


                ////bStream.Dispose();//clean up old stream 
                //bStream = sample.Buffer.AsStream();

                //bStream.Position = 0;

                ////write the raw sample to the reqest sample stream;
                //rawSample.WriteToStream(bStream);

                //sample.Buffer.Length = (uint)rawSample.Length;
                //sample.DecodeTimestamp = TimeSpan.FromTicks(rawSample.PresentationTimeStamp*100);
                //Debug.WriteLine("sample length: {0}", rawSample.Length);
                //Debug.WriteLine("PTS: {0}", rawSample.PresentationTimeStamp);
                ////sample.DecodeTimestamp = new TimeSpan(T0.Ticks * frameCount);
                //sample.Duration = sample.DecodeTimestamp - Tn;
                //sample.KeyFrame = ScanForKeyframe(bStream, sample.Buffer.Length);//rawSample.Length > 3000;//

                ////not sure if this is correct...
                //sample.Discontinuous = !lastFrame;

                ////this just tells us if the MpegTS Continuity Counter 
                ////for all Mpeg packets in the sample were in order. (0-15)
                //lastFrame = rawSample.IsComplete;

                ////if (!foundKeyFrame && !sample.KeyFrame)
                ////    sample = emptySample;
                ////else

                //Tn = sample.DecodeTimestamp;

                //++frameCount;

                // create the MediaStreamSample and assign to the request object. 
                // You could also create the MediaStreamSample using createFromBuffer(...)

                //MediaStreamSample sample = await MediaStreamSample.CreateFromStreamAsync(inputStream, sampleSize, timeOffset);
                //sample.Duration = sampleDuration;
                //sample.KeyFrame = true;

                // increment the time and byte offset

                //byteOffset += sampleSize;
                //timeOffset = timeOffset.Add(sampleDuration);
                request.Sample = s;
            }
            catch(Exception ex)
            {
                var exStr = ex.ToString();
                Debug.WriteLine(exStr);
                throw ex;
            }
            finally
            {
                deferal.Complete();
            }

            Debug.WriteLine("exit request sample");
        }

        private MediaStreamSample GetSample(int length)
        {
            MediaStreamSample samp = null;

            lock (SampleRecycler)
            {
                if(SampleRecycler.Count > 0)
                    samp = SampleRecycler.FirstOrDefault(smpl => smpl.Buffer.Capacity >= length);
            }
            if(samp == null)
            {
                //create a new sample obj
                samp = MediaStreamSample.CreateFromBuffer(new Windows.Storage.Streams.Buffer((uint)length),
                                                        new TimeSpan(0));//default timespan

                samp.Processed += SampleProcessed;//make sure to recycle our samples/buffers
            }
            else
            {
                lock (SampleRecycler) SampleRecycler.Remove(samp);
            }

            return samp;
        }

        private void SampleProcessed(MediaStreamSample sender, object args)
        {
            Debug.WriteLine("sample proc evnt");

            lock (SampleRecycler)
                SampleRecycler.Add(sender);
        }

        enum NalTypes: byte
        {
            KeyFrame = 0x05,
            SPS = 0x07,
            PPS = 0x08,
        }

        /// <summary>
        /// must provide len, since we are recycling the buffer
        /// </summary>
        /// <param name="bStream"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        private bool ScanForKeyframe(Stream bStream, uint length)
        {
            List<int> nali = new List<int>(8);

            bStream.Position = 0;

            int b;

            for(int i = 0; i<length; ++i)
            {
                b = bStream.ReadByte();

                if (b == 0x00)
                {
                    bStream.Position += 2;

                    if (bStream.ReadByte() == 0x01)
                    {
                        i = (int)bStream.Position;

                        var type = (NalTypes)(bStream.ReadByte() & 0x0F);

                        Debug.WriteLine("found NAL type: {0}", type);

                        if (type == NalTypes.KeyFrame)
                        {
                            bStream.Position = 0 ;
                            var buf = new byte[bStream.Length-bStream.Position];
                            bStream.Read(buf, 0, buf.Length);
                            return true;
                        }

                        nali.Add(i);
                    }
                }
                //else
                //    i += 2;
            }

            Debug.WriteLine("nal count: {0}", nali.Count);

            //string iStr = "nal i's:";
            System.Text.StringBuilder iStr = new System.Text.StringBuilder("nal i's:");
            foreach (int i in nali)
                iStr.Append(" ").Append(i).Append(",");

            Debug.WriteLine(iStr.ToString());

            return false;
        }

        System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();

        private void mss_Starting(MediaStreamSource sender, MediaStreamSourceStartingEventArgs args)
        {
            //threadSync.WaitOne();//make the codec wait on us to start?

            frameCount = 0;
            //_sampleGenerator.Initialize(_mss, videoDesc);
            args.Request.SetActualStartPosition(new TimeSpan(0));
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
                    //var ts = sw.Elapsed;
                    //Debug.WriteLine("reading sample from file {0}", ts);

                    await fStream.ReadAsync(b, 0, b.Length).ConfigureAwait(false);

                    if(!extractor.AddRaw(b))
                    {//the chunk we read was not mpegTS data, or we need to find the sync byte
                        Debug.WriteLine("re-syncing file stream...");

                        int syncbytePos = MpegTS.TsPacket.PacketLength - b.ToList().IndexOf(MpegTS.TsPacket.SyncByte);

                        if(syncbytePos < MpegTS.TsPacket.PacketLength)
                            fStream.Position -= syncbytePos;//try to re-sync the stream cursor
                    }

                    if (extractor.SampleCount > 0)
                    {
                        var rawSam = extractor.DequeueNextSample(false);

                        var vidSam = GetSample(rawSam.Length);
                        var bStr = vidSam.Buffer.AsStream();

                        rawSam.WriteToStream(bStr);
                        vidSam.Buffer.Length = (uint)bStr.Length;
                        bStr.Position = 0;

                        vidSam.DecodeTimestamp = TimeSpan.FromTicks(rawSam.PresentationTimeStamp * 100);
                        Debug.WriteLine("sample length: {0}", rawSam.Length);
                        Debug.WriteLine("PTS: {0}", rawSam.PresentationTimeStamp);
                        //sample.DecodeTimestamp = new TimeSpan(T0.Ticks * frameCount);
                        vidSam.Duration = vidSam.DecodeTimestamp - Tn;
                        vidSam.KeyFrame = ScanForKeyframe(bStr, vidSam.Buffer.Length);//rawSample.Length > 3000;//

                        //not sure if this is correct...
                        vidSam.Discontinuous = !lastFrame;

                        //this just tells us if the MpegTS Continuity Counter 
                        //for all Mpeg packets in the sample were in order. (0-15)
                        lastFrame = rawSam.IsComplete;

                        //if (!foundKeyFrame && !sample.KeyFrame)
                        //    sample = emptySample;
                        //else

                        Tn = vidSam.DecodeTimestamp;

                        ++frameCount;

                        //need to look for 1st key frame here before starting the decoder!
                        //once we find the 1st kf, then we start queueing MediaSamples (not just raw samples) here
                        if (!gotT0 && !foundKeyFrame)
                        {
                            if (vidSam.KeyFrame)
                            {
                                Tn = T0 = TimeSpan.FromTicks(rawSam.PresentationTimeStamp * 100);

                                foundKeyFrame = gotT0 = true;

                                vidSam.Duration = Tf;

                                SampleQ.Enqueue(vidSam);

                            }
                            else
                                lock(SampleRecycler) SampleRecycler.Add(vidSam);
                        }
                        else
                        {
                            SampleQ.Enqueue(vidSam);

                            threadSync.Set();

                            while (SampleQ.Count > 5)
                                await Task.Delay(20).ConfigureAwait(false);//slow down the extractor, no need to pre-load too much
                        }

                        bStr.Dispose();
                    }
                }
                else
                    fStream.Position = 0;//go to start of file


            } while (running);
        }

        private async void button_Click(object sender, RoutedEventArgs e)
        {
            await Task.Run(() => threadSync.WaitOne());

            initStream();
        }

        private void initStream()
        {
            mediaElement.RealTimePlayback = true;

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