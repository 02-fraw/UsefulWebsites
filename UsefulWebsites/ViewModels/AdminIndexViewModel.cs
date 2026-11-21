namespace UsefulWebsites.ViewModels;

public sealed class AdminIndexViewModel
{
    public IReadOnlyList<WebsiteAdminListItemViewModel> Websites { get; init; } = [];

    public IReadOnlyList<CategoryFilterViewModel> Categories { get; init; } = [];

    public int TotalWebsiteCount { get; init; }

    public int TotalCategoryCount { get; init; }

    public int? SelectedCategoryId { get; init; }

    public string SearchTerm { get; init; } = string.Empty;
}

public sealed class WebsiteAdminListItemViewModel
{
    public int Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Url { get; init; } = string.Empty;

    public string? LogoUrl { get; init; }

    public string Description { get; init; } = string.Empty;

    public string CategoryName { get; init; } = string.Empty;

    public DateTime CreatedAtUtc { get; init; }

    public DateTime? UpdatedAtUtc { get; init; }
}
