namespace UsefulWebsites.Services.Pdf;

public interface IWebsitePdfService
{
    Task<byte[]> GenerateAllWebsitesPdfAsync(
        CancellationToken cancellationToken = default);
}
