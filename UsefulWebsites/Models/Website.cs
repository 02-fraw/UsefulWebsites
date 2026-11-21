using System.ComponentModel.DataAnnotations;

namespace UsefulWebsites.Models;

public sealed class Website
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Website name is required.")]
    [StringLength(150, MinimumLength = 2, ErrorMessage = "Website name must be between 2 and 150 characters.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Website URL is required.")]
    [StringLength(2048, ErrorMessage = "Website URL is too long.")]
    [Url(ErrorMessage = "Please enter a valid website URL.")]
    public string Url { get; set; } = string.Empty;

    [StringLength(2048, ErrorMessage = "Logo URL is too long.")]
    [Url(ErrorMessage = "Please enter a valid logo URL.")]
    public string? LogoUrl { get; set; }

    [Required(ErrorMessage = "Description is required.")]
    [StringLength(6000, MinimumLength = 20, ErrorMessage = "Description must be between 20 and 6000 characters.")]
    public string Description { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Please select a category.")]
    public int CategoryId { get; set; }

    public Category Category { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }
}
