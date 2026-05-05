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
using System.Windows.Media.Imaging;
using OpenSteamAnalyzer.Models;
using OpenSteamAnalyzer.ViewModels;

namespace OpenSteamAnalyzer.Views;

public partial class DashboardView : UserControl
{
    private static readonly HttpClient BackgroundHttpClient = new();
    private MediaPlayer? _backgroundMediaPlayer;
    private int _backgroundRequestVersion;
    private int _miniProfileRequestVersion;
    private bool _hasAnimatedAvatarContent;

    public DashboardView()
    {
        InitializeComponent();
        DataContextChanged += DashboardView_OnDataContextChanged;
        Unloaded += DashboardView_OnUnloaded;
    }

    private void GamesDataGrid_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        PageScrollViewer.ScrollToVerticalOffset(PageScrollViewer.VerticalOffset - e.Delta);
        e.Handled = true;
    }

    private void PageScrollViewer_OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (!_hasAnimatedAvatarContent)
        {
            return;
        }

        AnimatedAvatarWebView.Visibility = PageScrollViewer.VerticalOffset <= 12
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private async void AnalyzeFriendButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DashboardViewModel viewModel
            || sender is not FrameworkElement { DataContext: SteamFriend friend })
        {
            return;
        }

        await viewModel.AnalyzeFriendAsync(friend);
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
            UpdateMiniProfilePreview(newViewModel.Profile);
        }
    }

    private void DashboardView_OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is DashboardViewModel viewModel)
        {
            viewModel.PropertyChanged -= DashboardViewModel_OnPropertyChanged;
        }

        StopMiniProfilePreview();
        StopProfileBackgroundVideo();
    }

    private void DashboardViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DashboardViewModel.Profile)
            && sender is DashboardViewModel viewModel)
        {
            UpdateAnimatedAvatar(viewModel.Profile);
            UpdateProfileBackground(viewModel.Profile);
            UpdateMiniProfilePreview(viewModel.Profile);
        }
    }

    private void MiniProfilePreviewMedia_OnMediaEnded(object sender, RoutedEventArgs e)
    {
        MiniProfilePreviewMedia.Position = TimeSpan.Zero;
        MiniProfilePreviewMedia.Play();
    }

    private async void UpdateMiniProfilePreview(SteamProfile? profile)
    {
        var requestVersion = ++_miniProfileRequestVersion;
        StopMiniProfilePreview();

        if (profile is null || string.IsNullOrWhiteSpace(profile.MiniProfileBackgroundVideoUrl))
        {
            return;
        }

        try
        {
            var cachedVideoPath = await CacheMediaVideoAsync(profile.MiniProfileBackgroundVideoUrl);
            if (requestVersion != _miniProfileRequestVersion)
            {
                return;
            }

            MiniProfilePreviewMedia.Source = new Uri(cachedVideoPath, UriKind.Absolute);
            MiniProfilePreviewMedia.Visibility = Visibility.Visible;
            MiniProfilePreviewMedia.Play();
        }
        catch
        {
            StopMiniProfilePreview();
        }
    }

    private void StopMiniProfilePreview()
    {
        MiniProfilePreviewMedia.Stop();
        MiniProfilePreviewMedia.Source = null;
        MiniProfilePreviewMedia.Visibility = Visibility.Collapsed;
    }

    private async void UpdateAnimatedAvatar(SteamProfile? profile)
    {
        if (profile is null || !profile.HasAnimatedAvatarLayer)
        {
            _hasAnimatedAvatarContent = false;
            AnimatedAvatarWebView.Visibility = Visibility.Collapsed;
            return;
        }

        try
        {
            AnimatedAvatarWebView.Visibility = Visibility.Visible;
            _hasAnimatedAvatarContent = true;
            AnimatedAvatarWebView.DefaultBackgroundColor = System.Drawing.Color.Transparent;
            await AnimatedAvatarWebView.EnsureCoreWebView2Async();
            AnimatedAvatarWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            AnimatedAvatarWebView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            AnimatedAvatarWebView.NavigateToString(BuildAnimatedAvatarHtml(profile));
        }
        catch
        {
            _hasAnimatedAvatarContent = false;
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
        var requestVersion = ++_backgroundRequestVersion;

        if (profile is null || !profile.HasProfileBackground)
        {
            ProfileBackgroundImage.Visibility = Visibility.Collapsed;
            ProfileBackgroundImage.Source = null;
            StopProfileBackgroundVideo();
            return;
        }

        try
        {
            StopProfileBackgroundVideo();

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
                var cachedVideoPath = await CacheMediaVideoAsync(profile.ProfileBackgroundVideoUrl);
                if (requestVersion != _backgroundRequestVersion)
                {
                    return;
                }

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
        if (!File.Exists(videoPath))
        {
            return;
        }

        _backgroundMediaPlayer = new MediaPlayer { Volume = 0 };
        _backgroundMediaPlayer.MediaEnded += BackgroundMediaPlayer_OnMediaEnded;
        _backgroundMediaPlayer.Open(new Uri(videoPath, UriKind.Absolute));

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
        ProfileBackgroundImage.Visibility = Visibility.Collapsed;
        _backgroundMediaPlayer.Play();
    }

    private void StopProfileBackgroundVideo()
    {
        if (_backgroundMediaPlayer is not null)
        {
            _backgroundMediaPlayer.MediaEnded -= BackgroundMediaPlayer_OnMediaEnded;
            _backgroundMediaPlayer.Stop();
            _backgroundMediaPlayer.Close();
        }

        _backgroundMediaPlayer = null;
        ProfileBackgroundVideo.Fill = null;
        ProfileBackgroundVideo.Visibility = Visibility.Collapsed;
    }

    private void BackgroundMediaPlayer_OnMediaEnded(object? sender, EventArgs e)
    {
        if (_backgroundMediaPlayer is null)
        {
            return;
        }

        _backgroundMediaPlayer.Position = TimeSpan.Zero;
        _backgroundMediaPlayer.Play();
    }


    private static async Task<string> CacheMediaVideoAsync(string videoUrl)
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

        using var request = new HttpRequestMessage(HttpMethod.Get, videoUrl);
        request.Headers.UserAgent.ParseAdd("OpenSteamAnalyzer/1.0");
        using var response = await BackgroundHttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        await using var remoteStream = await response.Content.ReadAsStreamAsync();
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
