namespace UsefulWebsites.ViewModels;

public sealed class HomeIndexViewModel
{
    public IReadOnlyList<CategoryFilterViewModel> Categories { get; init; } = [];

    public IReadOnlyList<WebsiteCardViewModel> Websites { get; init; } = [];

    public int? SelectedCategoryId { get; init; }

    public string SearchTerm { get; init; } = string.Empty;

    public int TotalWebsiteCount { get; init; }
}

public sealed class CategoryFilterViewModel
{
    public int Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public int WebsiteCount { get; init; }
}

public sealed class WebsiteCardViewModel
{
    public int Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Url { get; init; } = string.Empty;

    public string? LogoUrl { get; init; }

    public string Description { get; init; } = string.Empty;

    public int CategoryId { get; init; }

    public string CategoryName { get; init; } = string.Empty;

    public DateTime CreatedAtUtc { get; init; }
}
