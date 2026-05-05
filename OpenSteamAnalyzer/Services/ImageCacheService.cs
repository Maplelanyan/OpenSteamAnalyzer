using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;

namespace OpenSteamAnalyzer.Services;

public sealed class ImageCacheService : IImageCacheService
{
    private readonly HttpClient _httpClient;
    private readonly string _cacheDirectory;

    public ImageCacheService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _cacheDirectory = Path.Combine(appData, "OpenSteamAnalyzer", "image-cache");
        Directory.CreateDirectory(_cacheDirectory);
    }

    public async Task<string> GetCachedImagePathAsync(string imageUrl, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return string.Empty;
        }

        var filePath = Path.Combine(_cacheDirectory, BuildFileName(imageUrl));
        if (File.Exists(filePath) && new FileInfo(filePath).Length > 0)
        {
            return filePath;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, imageUrl);
        request.Headers.UserAgent.ParseAdd("OpenSteamAnalyzer/1.0");
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var remoteStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var fileStream = File.Create(filePath);
        await remoteStream.CopyToAsync(fileStream, cancellationToken);
        return filePath;
    }

    private static string BuildFileName(string imageUrl)
    {
        var extension = ".jpg";
        if (Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri))
        {
            var uriExtension = Path.GetExtension(uri.AbsolutePath);
            if (!string.IsNullOrWhiteSpace(uriExtension) && uriExtension.Length <= 8)
            {
                extension = uriExtension;
            }
        }

        return $"{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(imageUrl)))}{extension}";
    }
}
