using System.ComponentModel.DataAnnotations;

namespace UsefulWebsites.Models;

public sealed class Category
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Category name is required.")]
    [StringLength(80, MinimumLength = 2, ErrorMessage = "Category name must be between 2 and 80 characters.")]
    public string Name { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<Website> Websites { get; set; } = new List<Website>();
}
