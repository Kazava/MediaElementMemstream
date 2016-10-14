using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace MediaElementMemstream
{
    //public class src: Windows.Media.Core.IMediaSource
    //{

    //}

    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();

        }

        private async void button_Click(object sender, RoutedEventArgs e)
        {
            
            var dir = (await KnownFolders.RemovableDevices.GetFoldersAsync()).ToList();
            var f = await dir.FirstOrDefault(file => file.Name == @"D:\").GetFileAsync(@"extSdCard\Video_2014_5_6__16_51_56.mp4");//.GetFileAsync(@"extSdCard\Video_2014_5_7__15_33_44.mpg");//
            var fStream = await f.OpenReadAsync();
            
            mediaElement.Position = new TimeSpan(0);

            //mediaElement.Source = new Uri(@"file://d:/extSdCard/Video_2014_5_6__16_51_56.mp4");
            mediaElement.SetSource(fStream, f.ContentType);

            //Windows.Media.Core.MediaStreamSource ms = new Windows.Media.Core.MediaStreamSource();
            //var uri = new Uri("file:///extSdCard/Video_2014_5_7__15_33_44.mpg");
            //var resp = await Windows.Media.Streaming.Adaptive.AdaptiveMediaSource.CreateFromUriAsync(uri);

            //if (resp.Status == Windows.Media.Streaming.Adaptive.AdaptiveMediaSourceCreationStatus.Success)
            //    mediaElement.SetMediaStreamSource(resp.MediaSource);


            mediaElement.Play();

        }
    }
}
