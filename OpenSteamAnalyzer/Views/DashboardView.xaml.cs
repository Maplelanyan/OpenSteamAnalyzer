using System.ComponentModel;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using OpenSteamAnalyzer.Models;
using OpenSteamAnalyzer.ViewModels;

namespace OpenSteamAnalyzer.Views;

public partial class DashboardView : UserControl
{
    private static readonly HttpClient BackgroundHttpClient = new();
    private MediaPlayer? _backgroundMediaPlayer;
    private MediaClock? _backgroundMediaClock;

    public DashboardView()
    {
        InitializeComponent();
        DataContextChanged += DashboardView_OnDataContextChanged;
        Unloaded += DashboardView_OnUnloaded;
    }

    private void SaveApiKeyButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DashboardViewModel viewModel)
        {
            return;
        }

        if (viewModel.SaveApiKey(ApiKeyPasswordBox.Password))
        {
            ApiKeyPasswordBox.Clear();
        }
    }

    private void GamesDataGrid_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        PageScrollViewer.ScrollToVerticalOffset(PageScrollViewer.VerticalOffset - e.Delta);
        e.Handled = true;
    }

    private void DashboardView_OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is DashboardViewModel oldViewModel)
        {
            oldViewModel.PropertyChanged -= DashboardViewModel_OnPropertyChanged;
        }

        if (e.NewValue is DashboardViewModel newViewModel)
        {
            newViewModel.PropertyChanged += DashboardViewModel_OnPropertyChanged;
            UpdateAnimatedAvatar(newViewModel.Profile);
            UpdateProfileBackground(newViewModel.Profile);
        }
    }

    private void DashboardView_OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is DashboardViewModel viewModel)
        {
            viewModel.PropertyChanged -= DashboardViewModel_OnPropertyChanged;
        }
    }

    private void DashboardViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DashboardViewModel.Profile)
            && sender is DashboardViewModel viewModel)
        {
            UpdateAnimatedAvatar(viewModel.Profile);
            UpdateProfileBackground(viewModel.Profile);
        }
    }

    private async void UpdateAnimatedAvatar(SteamProfile? profile)
    {
        if (profile is null || !profile.HasAnimatedAvatarLayer)
        {
            AnimatedAvatarWebView.Visibility = Visibility.Collapsed;
            return;
        }

        try
        {
            AnimatedAvatarWebView.Visibility = Visibility.Visible;
            AnimatedAvatarWebView.DefaultBackgroundColor = System.Drawing.Color.Transparent;
            await AnimatedAvatarWebView.EnsureCoreWebView2Async();
            AnimatedAvatarWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            AnimatedAvatarWebView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            AnimatedAvatarWebView.NavigateToString(BuildAnimatedAvatarHtml(profile));
        }
        catch
        {
            AnimatedAvatarWebView.Visibility = Visibility.Collapsed;
        }
    }

    private static string BuildAnimatedAvatarHtml(SteamProfile profile)
    {
        var avatarLayer = string.IsNullOrWhiteSpace(profile.AnimatedAvatarVideoUrl)
            ? ImageTag("avatar-media", profile.DisplayAvatarUrl)
            : VideoTag("avatar-media", profile.AnimatedAvatarVideoUrl);
        var frameLayer = string.IsNullOrWhiteSpace(profile.AvatarFrameVideoUrl)
            ? ImageTag("frame-media", profile.AvatarFrameUrl)
            : VideoTag("frame-media", profile.AvatarFrameVideoUrl);

        return $$"""
            <!doctype html>
            <html>
            <head>
                <meta charset="utf-8">
                <style>
                    html, body {
                        width: 72px;
                        height: 72px;
                        margin: 0;
                        overflow: hidden;
                        background: transparent;
                    }
                    .root {
                        position: relative;
                        width: 72px;
                        height: 72px;
                    }
                    .avatar {
                        position: absolute;
                        left: 7px;
                        top: 7px;
                        width: 58px;
                        height: 58px;
                        border-radius: 8px;
                        overflow: hidden;
                        background: #E8EDF5;
                    }
                    .avatar-media {
                        width: 100%;
                        height: 100%;
                        object-fit: cover;
                        display: block;
                    }
                    .frame-media {
                        position: absolute;
                        inset: 0;
                        width: 72px;
                        height: 72px;
                        object-fit: contain;
                        display: block;
                    }
                </style>
            </head>
            <body>
                <div class="root">
                    <div class="avatar">{{avatarLayer}}</div>
                    {{frameLayer}}
                </div>
            </body>
            </html>
            """;
    }

    private async void UpdateProfileBackground(SteamProfile? profile)
    {
        if (profile is null || !profile.HasProfileBackground)
        {
            ProfileBackgroundImage.Visibility = Visibility.Collapsed;
            ProfileBackgroundImage.Source = null;
            StopProfileBackgroundVideo();
            return;
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(profile.ProfileBackgroundUrl))
            {
                ProfileBackgroundImage.Source = new BitmapImage(new Uri(profile.ProfileBackgroundUrl, UriKind.Absolute));
                ProfileBackgroundImage.Visibility = Visibility.Visible;
            }
            else
            {
                ProfileBackgroundImage.Visibility = Visibility.Collapsed;
                ProfileBackgroundImage.Source = null;
            }

            if (!string.IsNullOrWhiteSpace(profile.ProfileBackgroundVideoUrl))
            {
                var cachedVideoPath = await CacheBackgroundVideoAsync(profile.ProfileBackgroundVideoUrl);
                StartProfileBackgroundVideo(cachedVideoPath);
                return;
            }

            if (string.IsNullOrWhiteSpace(profile.ProfileBackgroundUrl))
            {
                StopProfileBackgroundVideo();
                return;
            }

            StopProfileBackgroundVideo();
        }
        catch
        {
            ProfileBackgroundImage.Visibility = Visibility.Collapsed;
            ProfileBackgroundImage.Source = null;
            StopProfileBackgroundVideo();
        }
    }

    private void StartProfileBackgroundVideo(string videoPath)
    {
        StopProfileBackgroundVideo();

        var timeline = new MediaTimeline(new Uri(videoPath, UriKind.Absolute))
        {
            RepeatBehavior = RepeatBehavior.Forever
        };
        _backgroundMediaClock = timeline.CreateClock();
        _backgroundMediaPlayer = new MediaPlayer
        {
            Clock = _backgroundMediaClock,
            Volume = 0
        };

        var drawing = new VideoDrawing
        {
            Player = _backgroundMediaPlayer,
            Rect = new Rect(0, 0, 16, 9)
        };
        ProfileBackgroundVideo.Fill = new DrawingBrush(drawing)
        {
            Stretch = Stretch.UniformToFill
        };
        ProfileBackgroundVideo.Visibility = Visibility.Visible;
        _backgroundMediaClock.Controller?.Begin();
    }

    private void StopProfileBackgroundVideo()
    {
        _backgroundMediaClock?.Controller?.Stop();
        _backgroundMediaPlayer?.Close();
        _backgroundMediaClock = null;
        _backgroundMediaPlayer = null;
        ProfileBackgroundVideo.Fill = null;
        ProfileBackgroundVideo.Visibility = Visibility.Collapsed;
    }

    private static async Task<string> CacheBackgroundVideoAsync(string videoUrl)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var cacheDirectory = Path.Combine(appData, "OpenSteamAnalyzer", "media-cache");
        Directory.CreateDirectory(cacheDirectory);

        var extension = Path.GetExtension(new Uri(videoUrl).AbsolutePath);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".mp4";
        }

        var fileName = $"{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(videoUrl)))}{extension}";
        var filePath = Path.Combine(cacheDirectory, fileName);
        if (File.Exists(filePath) && new FileInfo(filePath).Length > 0)
        {
            return filePath;
        }

        await using var remoteStream = await BackgroundHttpClient.GetStreamAsync(videoUrl);
        await using var fileStream = File.Create(filePath);
        await remoteStream.CopyToAsync(fileStream);
        return filePath;
    }

    private static string ImageTag(string cssClass, string source)
    {
        return string.IsNullOrWhiteSpace(source)
            ? string.Empty
            : $"""<img class="{cssClass}" src="{WebUtility.HtmlEncode(source)}" alt="">""";
    }

    private static string VideoTag(string cssClass, string source)
    {
        return string.IsNullOrWhiteSpace(source)
            ? string.Empty
            : $"""<video class="{cssClass}" src="{WebUtility.HtmlEncode(source)}" autoplay loop muted playsinline></video>""";
    }
}
