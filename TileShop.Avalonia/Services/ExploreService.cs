using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using TileShop.Shared.Services;

namespace TileShop.AvaloniaUI.Services;

internal class ExploreService : IExploreService
{
    public void ExploreDiskLocation(string location)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            string command = $"explorer.exe";
            string args = $"/select, {location}";
            Process.Start(command, args);
        }
    }

    public void ExploreWebLocation(Uri uri)
    {
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            throw new NotSupportedException($"{nameof(ExploreWebLocation)} attempted to open the invalid Uri '{uri.AbsoluteUri}'");

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var psi = new ProcessStartInfo
            {
                FileName = uri.AbsoluteUri,
                UseShellExecute = true
            };

            using var p = Process.Start(psi);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            using var p = Process.Start("xdg-open", uri.AbsoluteUri);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            using var p = Process.Start("open", uri.AbsoluteUri);
        }
        else
        {
            throw new NotSupportedException($"{nameof(ExploreWebLocation)} does not support the system OS: {RuntimeInformation.OSDescription}");
        }
    }
}
