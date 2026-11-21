using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace UsefulWebsites.ViewModels;

public sealed class WebsiteFormViewModel : IValidatableObject
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "Website name is required.")]
    [StringLength(150, MinimumLength = 2, ErrorMessage = "Website name must be between 2 and 150 characters.")]
    [Display(Name = "Website name")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Website URL is required.")]
    [StringLength(2048, ErrorMessage = "Website URL is too long.")]
    [Url(ErrorMessage = "Please enter a valid website URL.")]
    [Display(Name = "Website URL")]
    public string Url { get; set; } = string.Empty;

    [StringLength(2048, ErrorMessage = "Logo URL is too long.")]
    [Url(ErrorMessage = "Please enter a valid logo URL.")]
    [Display(Name = "Logo URL")]
    public string? LogoUrl { get; set; }

    [Required(ErrorMessage = "Description is required.")]
    [StringLength(6000, MinimumLength = 20, ErrorMessage = "Description must be between 20 and 6000 characters.")]
    [DataType(DataType.MultilineText)]
    [Display(Name = "Description")]
    public string Description { get; set; } = string.Empty;

    [Display(Name = "Existing category")]
    public int? CategoryId { get; set; }

    [StringLength(80, MinimumLength = 2, ErrorMessage = "New category name must be between 2 and 80 characters.")]
    [Display(Name = "New category name")]
    public string? NewCategoryName { get; set; }

    public List<SelectListItem> CategoryOptions { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        bool existingCategoryNotSelected = !CategoryId.HasValue || CategoryId.Value <= 0;
        bool newCategoryNotEntered = string.IsNullOrWhiteSpace(NewCategoryName);

        if (existingCategoryNotSelected && newCategoryNotEntered)
        {
            yield return new ValidationResult(
                "Select an existing category or enter a new category name.",
                new[] { nameof(CategoryId), nameof(NewCategoryName) });
        }
    }
}
