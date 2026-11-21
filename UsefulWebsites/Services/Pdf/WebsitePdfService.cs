using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using UsefulWebsites.Data;

namespace UsefulWebsites.Services.Pdf;

public sealed class WebsitePdfService : IWebsitePdfService
{
    private const string LogoHttpClientName = "WebsitePdfLogoClient";
    private const int MaxLogoSizeInBytes = 512 * 1024;
    private const int MaxConcurrentLogoDownloads = 6;

    private readonly ApplicationDbContext _dbContext;
    private readonly IHttpClientFactory _httpClientFactory;

    public WebsitePdfService(
        ApplicationDbContext dbContext,
        IHttpClientFactory httpClientFactory)
    {
        _dbContext = dbContext;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<byte[]> GenerateAllWebsitesPdfAsync(
        CancellationToken cancellationToken = default)
    {
        var websiteSources = await _dbContext.Websites
            .AsNoTracking()
            .OrderByDescending(website => website.CreatedAtUtc)
            .ThenBy(website => website.Name)
            .Select(website => new WebsitePdfSource
            {
                Name = website.Name,
                Url = website.Url,
                LogoUrl = website.LogoUrl,
                Description = website.Description,
                CategoryName = website.Category.Name
            })
            .ToListAsync(cancellationToken);

        var categoryNames = await _dbContext.Categories
            .AsNoTracking()
            .Select(category => category.Name)
            .ToListAsync(cancellationToken);

        var websites = await PrepareWebsiteItemsAsync(websiteSources, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        return GeneratePdf(websites, categoryNames);
    }

    private async Task<IReadOnlyList<WebsitePdfItem>> PrepareWebsiteItemsAsync(
        IReadOnlyList<WebsitePdfSource> websiteSources,
        CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient(LogoHttpClientName);
        using var semaphore = new SemaphoreSlim(MaxConcurrentLogoDownloads);

        var tasks = websiteSources.Select(async website =>
        {
            await semaphore.WaitAsync(cancellationToken);

            try
            {
                var logoBytes = await TryDownloadLogoAsync(
                    httpClient,
                    website.LogoUrl,
                    cancellationToken);

                return new WebsitePdfItem(
                    website.Name,
                    website.Url,
                    website.Description,
                    website.CategoryName,
                    logoBytes);
            }
            finally
            {
                semaphore.Release();
            }
        });

        return await Task.WhenAll(tasks);
    }

    private static byte[] GeneratePdf(
        IReadOnlyList<WebsitePdfItem> websites,
        IReadOnlyList<string> categoryNames)
    {
        var categories = categoryNames
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Select(name => new
            {
                Name = name,
                Websites = websites
                    .Where(website => string.Equals(website.CategoryName, name, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(website => website.Name, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(website => website.Url, StringComparer.OrdinalIgnoreCase)
                    .ToList()
            })
            .ToList();

        var generatedAtUtc = DateTime.UtcNow;

        return Document
            .Create(document =>
            {
                document.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.MarginHorizontal(32);
                    page.MarginVertical(28);
                    page.PageColor(Colors.White);

                    page.DefaultTextStyle(style => style
                        .FontSize(10)
                        .FontColor("#1E293B"));

                    page.Header()
                        .Element(container => ComposeHeader(container, websites.Count, generatedAtUtc));

                    page.Content()
                        .PaddingVertical(16)
                        .Column(column =>
                        {
                            column.Spacing(14);

                            if (categories.Count > 0)
                            {
                                column.Item()
                                    .Column(index =>
                                    {
                                        index.Spacing(5);

                                        index.Item()
                                            .Text($"Categories ({categories.Count})")
                                            .FontSize(15)
                                            .Bold()
                                            .FontColor("#4F46E5");

                                        foreach (var category in categories)
                                        {
                                            index.Item()
                                                .Text($"{category.Name} ({category.Websites.Count})")
                                                .FontSize(10)
                                                .FontColor("#334155");
                                        }
                                    });
                            }

                            if (websites.Count == 0)
                            {
                                column.Item()
                                    .PaddingTop(35)
                                    .AlignCenter()
                                    .Text("No websites have been added yet.")
                                    .FontSize(14)
                                    .FontColor("#64748B");

                                return;
                            }

                            foreach (var category in categories)
                            {
                                if (category.Websites.Count == 0)
                                {
                                    continue;
                                }

                                column.Item()
                                    .EnsureSpace(180)
                                    .Column(group =>
                                    {
                                        group.Spacing(12);

                                        group.Item()
                                            .Text($"{category.Name} ({category.Websites.Count})")
                                            .FontSize(14)
                                            .Bold()
                                            .FontColor("#4F46E5");

                                        foreach (var website in category.Websites)
                                        {
                                            group.Item()
                                                .Element(container => ComposeWebsiteCard(container, website));
                                        }
                                    });
                            }
                        });

                    page.Footer()
                        .Element(ComposeFooter);
                });
            })
            .GeneratePdf();
    }

    private static void ComposeHeader(IContainer container, int websiteCount, DateTime generatedAtUtc)
    {
        string countLabel = websiteCount == 1 ? "1 website" : $"{websiteCount} websites";

        container.Column(column =>
        {
            column.Item()
                .Text("Useful Websites")
                .FontSize(23)
                .Bold()
                .FontColor("#4F46E5");

            column.Item()
                .PaddingTop(4)
                .Text($"{countLabel} • Sorted A–Z by category • {generatedAtUtc.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture)}")
                .FontSize(9)
                .FontColor("#64748B");

            column.Item()
                .PaddingTop(11)
                .LineHorizontal(1)
                .LineColor("#CBD5E1");
        });
    }

    private static void ComposeWebsiteCard(IContainer container, WebsitePdfItem website)
    {
        container
            .EnsureSpace(145)
            .Border(1)
            .BorderColor("#CBD5E1")
            .Background("#F8FAFC")
            .Padding(13)
            .Column(column =>
            {
                column.Item()
                    .Row(row =>
                    {
                        row.ConstantItem(58)
                            .Element(logoContainer => ComposeLogo(logoContainer, website));

                        row.RelativeItem()
                            .PaddingLeft(13)
                            .Column(info =>
                            {
                                info.Item()
                                    .Text(website.Name)
                                    .FontSize(16)
                                    .Bold()
                                    .FontColor("#1E293B");

                                info.Item()
                                    .PaddingTop(5)
                                    .Text(text =>
                                    {
                                        text.Span("Category: ")
                                            .SemiBold()
                                            .FontColor("#64748B");

                                        text.Span(website.CategoryName)
                                            .FontColor("#4F46E5");
                                    });
                            });
                    });

                column.Item()
                    .PaddingTop(11)
                    .Text("DESCRIPTION")
                    .FontSize(8)
                    .Bold()
                    .FontColor("#64748B");

                column.Item()
                    .PaddingTop(4)
                    .Text(website.Description)
                    .FontSize(10)
                    .FontColor("#334155");

                column.Item()
                    .PaddingTop(11)
                    .Text(text =>
                    {
                        text.Span("Website URL: ")
                            .SemiBold()
                            .FontColor("#64748B");

                        text.Hyperlink(website.Url, website.Url)
                            .FontColor("#2563EB")
                            .Underline();
                    });
            });
    }

    private static void ComposeLogo(IContainer container, WebsitePdfItem website)
    {
        container
            .Width(58)
            .Height(58)
            .Border(1)
            .BorderColor("#CBD5E1")
            .Background(Colors.White)
            .Element(content =>
            {
                if (website.LogoBytes is not null)
                {
                    content
                        .Padding(6)
                        .Image(website.LogoBytes)
                        .FitArea();

                    return;
                }

                content
                    .AlignCenter()
                    .AlignMiddle()
                    .Text(GetWebsiteInitials(website.Name))
                    .FontSize(17)
                    .Bold()
                    .FontColor("#4F46E5");
            });
    }

    private static void ComposeFooter(IContainer container)
    {
        container
            .PaddingTop(8)
            .BorderTop(1)
            .BorderColor("#E2E8F0")
            .Row(row =>
            {
                row.RelativeItem()
                    .Text("Useful Websites")
                    .FontSize(8)
                    .FontColor("#64748B");

                row.AutoItem()
                    .DefaultTextStyle(style => style
                        .FontSize(8)
                        .FontColor("#64748B"))
                    .Text(text =>
                    {
                        text.Span("Page ");
                        text.CurrentPageNumber();
                        text.Span(" / ");
                        text.TotalPages();
                    });
            });
    }

    private static string GetWebsiteInitials(string websiteName)
    {
        var words = websiteName.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (words.Length == 0)
        {
            return "?";
        }

        if (words.Length == 1)
        {
            return words[0][0].ToString().ToUpperInvariant();
        }

        return string.Concat(words[0][0], words[1][0]).ToUpperInvariant();
    }

    private static async Task<byte[]?> TryDownloadLogoAsync(
        HttpClient httpClient,
        string? logoUrl,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(logoUrl))
        {
            return null;
        }

        if (!Uri.TryCreate(logoUrl, UriKind.Absolute, out var uri))
        {
            return null;
        }

        if (!await IsSafePublicHttpsUriAsync(uri, cancellationToken))
        {
            return null;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            using var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var mediaType = response.Content.Headers.ContentType?.MediaType?.ToLowerInvariant();

            if (mediaType is not (
                "image/png" or
                "image/jpeg" or
                "image/jpg" or
                "image/webp" or
                "image/bmp" or
                "image/x-ms-bmp"))
            {
                return null;
            }

            var contentLength = response.Content.Headers.ContentLength;
            if (contentLength.HasValue && contentLength.Value > MaxLogoSizeInBytes)
            {
                return null;
            }

            await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var memoryStream = new MemoryStream();
            var buffer = new byte[81920];

            while (true)
            {
                var readCount = await responseStream.ReadAsync(
                    buffer.AsMemory(0, buffer.Length),
                    cancellationToken);

                if (readCount == 0)
                {
                    break;
                }

                if (memoryStream.Length + readCount > MaxLogoSizeInBytes)
                {
                    return null;
                }

                await memoryStream.WriteAsync(buffer.AsMemory(0, readCount), cancellationToken);
            }

            var imageBytes = memoryStream.ToArray();
            return IsSupportedRasterImage(imageBytes) ? imageBytes : null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    private static async Task<bool> IsSafePublicHttpsUriAsync(
        Uri uri,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (uri.IsLoopback || string.IsNullOrWhiteSpace(uri.Host) || !string.IsNullOrWhiteSpace(uri.UserInfo))
        {
            return false;
        }

        if (!uri.IsDefaultPort && uri.Port != 443)
        {
            return false;
        }

        try
        {
            var addresses = await Dns.GetHostAddressesAsync(uri.DnsSafeHost, cancellationToken);
            return addresses.Length > 0 && addresses.All(IsPublicIpAddress);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsPublicIpAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address))
        {
            return false;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            return IsPublicIpAddress(address.MapToIPv4());
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();

            if (bytes[0] is 0 or 10 or 127 || bytes[0] >= 224)
            {
                return false;
            }

            if (bytes[0] == 100 && bytes[1] is >= 64 and <= 127)
            {
                return false;
            }

            if (bytes[0] == 169 && bytes[1] == 254)
            {
                return false;
            }

            if (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
            {
                return false;
            }

            if (bytes[0] == 192 && bytes[1] == 168)
            {
                return false;
            }

            if (bytes[0] == 198 && bytes[1] is 18 or 19)
            {
                return false;
            }

            return true;
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (address.Equals(IPAddress.IPv6Any) ||
                address.Equals(IPAddress.IPv6None) ||
                address.IsIPv6LinkLocal ||
                address.IsIPv6Multicast ||
                address.IsIPv6SiteLocal)
            {
                return false;
            }

            var bytes = address.GetAddressBytes();
            if ((bytes[0] & 0xFE) == 0xFC)
            {
                return false;
            }

            return true;
        }

        return false;
    }

    private static bool IsSupportedRasterImage(byte[] imageBytes)
    {
        if (imageBytes.Length < 3)
        {
            return false;
        }

        bool isPng =
            imageBytes.Length >= 8 &&
            imageBytes[0] == 0x89 &&
            imageBytes[1] == 0x50 &&
            imageBytes[2] == 0x4E &&
            imageBytes[3] == 0x47 &&
            imageBytes[4] == 0x0D &&
            imageBytes[5] == 0x0A &&
            imageBytes[6] == 0x1A &&
            imageBytes[7] == 0x0A;

        bool isJpeg =
            imageBytes[0] == 0xFF &&
            imageBytes[1] == 0xD8 &&
            imageBytes[2] == 0xFF;

        bool isBmp =
            imageBytes.Length >= 2 &&
            imageBytes[0] == 0x42 &&
            imageBytes[1] == 0x4D;

        bool isWebP =
            imageBytes.Length >= 12 &&
            imageBytes[0] == 0x52 &&
            imageBytes[1] == 0x49 &&
            imageBytes[2] == 0x46 &&
            imageBytes[3] == 0x46 &&
            imageBytes[8] == 0x57 &&
            imageBytes[9] == 0x45 &&
            imageBytes[10] == 0x42 &&
            imageBytes[11] == 0x50;

        return isPng || isJpeg || isBmp || isWebP;
    }

    private sealed class WebsitePdfSource
    {
        public string Name { get; init; } = string.Empty;
        public string Url { get; init; } = string.Empty;
        public string? LogoUrl { get; init; }
        public string Description { get; init; } = string.Empty;
        public string CategoryName { get; init; } = string.Empty;
    }

    private sealed record WebsitePdfItem(
        string Name,
        string Url,
        string Description,
        string CategoryName,
        byte[]? LogoBytes);
}
