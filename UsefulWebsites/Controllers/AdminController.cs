using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using UsefulWebsites.Data;
using UsefulWebsites.Models;
using UsefulWebsites.Security;
using UsefulWebsites.Services.Pdf;
using UsefulWebsites.ViewModels;

namespace UsefulWebsites.Controllers;

[TypeFilter(typeof(LocalOnlyAdminFilter))]
[AutoValidateAntiforgeryToken]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AdminController> _logger;
    private readonly IWebsitePdfService _websitePdfService;

    public AdminController(
        ApplicationDbContext context,
        ILogger<AdminController> logger,
        IWebsitePdfService websitePdfService)
    {
        _context = context;
        _logger = logger;
        _websitePdfService = websitePdfService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? categoryId, string? search)
    {
        int? selectedCategoryId = categoryId.HasValue && categoryId.Value > 0 ? categoryId.Value : null;

        string searchTerm = (search ?? string.Empty).Trim();
        if (searchTerm.Length > 100)
        {
            searchTerm = searchTerm[..100];
        }

        var categories = await _context.Categories
            .AsNoTracking()
            .OrderBy(category => category.Name)
            .Select(category => new CategoryFilterViewModel
            {
                Id = category.Id,
                Name = category.Name,
                WebsiteCount = category.Websites.Count
            })
            .ToListAsync();

        var websiteQuery = _context.Websites
            .AsNoTracking()
            .AsQueryable();

        if (selectedCategoryId.HasValue)
        {
            websiteQuery = websiteQuery.Where(website => website.CategoryId == selectedCategoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            websiteQuery = websiteQuery.Where(website =>
                website.Name.Contains(searchTerm) ||
                website.Description.Contains(searchTerm) ||
                website.Url.Contains(searchTerm) ||
                website.Category.Name.Contains(searchTerm));
        }

        var websites = await websiteQuery
            .OrderByDescending(website => website.CreatedAtUtc)
            .ThenByDescending(website => website.Id)
            .Select(website => new WebsiteAdminListItemViewModel
            {
                Id = website.Id,
                Name = website.Name,
                Url = website.Url,
                LogoUrl = website.LogoUrl,
                Description = website.Description,
                CategoryName = website.Category.Name,
                CreatedAtUtc = website.CreatedAtUtc,
                UpdatedAtUtc = website.UpdatedAtUtc
            })
            .ToListAsync();

        var viewModel = new AdminIndexViewModel
        {
            Websites = websites,
            Categories = categories,
            TotalWebsiteCount = await _context.Websites.AsNoTracking().CountAsync(),
            TotalCategoryCount = categories.Count,
            SelectedCategoryId = selectedCategoryId,
            SearchTerm = searchTerm
        };

        ViewData["Title"] = "Management";

        return View(viewModel);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var viewModel = new WebsiteFormViewModel
        {
            CategoryOptions = await BuildCategoryOptionsAsync(null)
        };

        ViewData["Title"] = "Add New Website";

        return View(viewModel);
    }

    [HttpPost]
    public async Task<IActionResult> Create(WebsiteFormViewModel viewModel)
    {
        NormalizeFormValues(viewModel);

        ModelState.Clear();
        TryValidateModel(viewModel);

        ValidateHttpUrl(viewModel.Url, nameof(viewModel.Url), "Website URL");

        if (!string.IsNullOrWhiteSpace(viewModel.LogoUrl))
        {
            ValidateHttpUrl(viewModel.LogoUrl, nameof(viewModel.LogoUrl), "Logo URL");
        }

        if (!ModelState.IsValid)
        {
            viewModel.CategoryOptions = await BuildCategoryOptionsAsync(viewModel.CategoryId);
            ViewData["Title"] = "Add New Website";
            return View(viewModel);
        }

        bool duplicateWebsiteExists = await _context.Websites
            .AnyAsync(website => website.Url == viewModel.Url);

        if (duplicateWebsiteExists)
        {
            ModelState.AddModelError(
                nameof(viewModel.Url),
                "A website with this URL already exists.");

            viewModel.CategoryOptions = await BuildCategoryOptionsAsync(viewModel.CategoryId);
            ViewData["Title"] = "Add New Website";
            return View(viewModel);
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            int categoryId = await ResolveCategoryIdAsync(viewModel);

            if (categoryId == 0)
            {
                await transaction.RollbackAsync();

                ModelState.AddModelError(
                    nameof(viewModel.CategoryId),
                    "The selected category was not found.");

                viewModel.CategoryOptions = await BuildCategoryOptionsAsync(null);
                ViewData["Title"] = "Add New Website";
                return View(viewModel);
            }

            var website = new Website
            {
                Name = viewModel.Name,
                Url = viewModel.Url,
                LogoUrl = viewModel.LogoUrl,
                Description = viewModel.Description,
                CategoryId = categoryId,
                CreatedAtUtc = DateTime.UtcNow
            };

            _context.Websites.Add(website);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            TempData["SuccessMessage"] = $"{website.Name} was added successfully.";
            return RedirectToAction(nameof(Index));
        }
        catch (DbUpdateException exception)
        {
            await transaction.RollbackAsync();
            _logger.LogError(exception, "Database error while creating a website.");
            _context.ChangeTracker.Clear();

            ModelState.AddModelError(
                string.Empty,
                "Could not save website. Please review the form and try again.");

            viewModel.CategoryOptions = await BuildCategoryOptionsAsync(viewModel.CategoryId);
            ViewData["Title"] = "Add New Website";
            return View(viewModel);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var website = await _context.Websites
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id);

        if (website is null)
        {
            return NotFound();
        }

        var viewModel = new WebsiteFormViewModel
        {
            Id = website.Id,
            Name = website.Name,
            Url = website.Url,
            LogoUrl = website.LogoUrl,
            Description = website.Description,
            CategoryId = website.CategoryId,
            CategoryOptions = await BuildCategoryOptionsAsync(website.CategoryId)
        };

        ViewData["Title"] = "Edit Website";

        return View(viewModel);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, WebsiteFormViewModel viewModel)
    {
        if (!viewModel.Id.HasValue || id != viewModel.Id.Value)
        {
            return BadRequest();
        }

        NormalizeFormValues(viewModel);

        ModelState.Clear();
        TryValidateModel(viewModel);

        ValidateHttpUrl(viewModel.Url, nameof(viewModel.Url), "Website URL");

        if (!string.IsNullOrWhiteSpace(viewModel.LogoUrl))
        {
            ValidateHttpUrl(viewModel.LogoUrl, nameof(viewModel.LogoUrl), "Logo URL");
        }

        if (!ModelState.IsValid)
        {
            viewModel.CategoryOptions = await BuildCategoryOptionsAsync(viewModel.CategoryId);
            ViewData["Title"] = "Edit Website";
            return View(viewModel);
        }

        var website = await _context.Websites
            .FirstOrDefaultAsync(item => item.Id == id);

        if (website is null)
        {
            return NotFound();
        }

        bool duplicateWebsiteExists = await _context.Websites
            .AnyAsync(existing => existing.Id != id && existing.Url == viewModel.Url);

        if (duplicateWebsiteExists)
        {
            ModelState.AddModelError(
                nameof(viewModel.Url),
                "Another website with this URL already exists.");

            viewModel.CategoryOptions = await BuildCategoryOptionsAsync(viewModel.CategoryId);
            ViewData["Title"] = "Edit Website";
            return View(viewModel);
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            int categoryId = await ResolveCategoryIdAsync(viewModel);

            if (categoryId == 0)
            {
                await transaction.RollbackAsync();

                ModelState.AddModelError(
                    nameof(viewModel.CategoryId),
                    "The selected category was not found.");

                viewModel.CategoryOptions = await BuildCategoryOptionsAsync(null);
                ViewData["Title"] = "Edit Website";
                return View(viewModel);
            }

            website.Name = viewModel.Name;
            website.Url = viewModel.Url;
            website.LogoUrl = viewModel.LogoUrl;
            website.Description = viewModel.Description;
            website.CategoryId = categoryId;
            website.UpdatedAtUtc = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            TempData["SuccessMessage"] = $"{website.Name} was updated successfully.";
            return RedirectToAction(nameof(Index));
        }
        catch (DbUpdateException exception)
        {
            await transaction.RollbackAsync();
            _logger.LogError(exception, "Database error while updating website {WebsiteId}.", id);
            _context.ChangeTracker.Clear();

            ModelState.AddModelError(
                string.Empty,
                "Could not update website. Please review the form and try again.");

            viewModel.CategoryOptions = await BuildCategoryOptionsAsync(viewModel.CategoryId);
            ViewData["Title"] = "Edit Website";
            return View(viewModel);
        }
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var website = await _context.Websites
            .FirstOrDefaultAsync(item => item.Id == id);

        if (website is null)
        {
            TempData["WarningMessage"] = "The requested website was not found.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            string websiteName = website.Name;
            _context.Websites.Remove(website);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"{websiteName} was deleted successfully.";
        }
        catch (DbUpdateException exception)
        {
            _logger.LogError(exception, "Database error while deleting website {WebsiteId}.", id);
            TempData["ErrorMessage"] = "Could not delete website. Please try again.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> BulkDelete(List<int>? selectedIds)
    {
        var validIds = selectedIds?
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        if (validIds is null || validIds.Count == 0)
        {
            TempData["WarningMessage"] = "Select at least one website to delete.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            int deletedCount = await _context.Websites
                .Where(website => validIds.Contains(website.Id))
                .ExecuteDeleteAsync();

            TempData["SuccessMessage"] = deletedCount == 1
                ? "1 website was deleted."
                : $"{deletedCount} websites were deleted.";
        }
        catch (DbUpdateException exception)
        {
            _logger.LogError(exception, "Database error during bulk delete.");
            TempData["ErrorMessage"] = "Could not delete selected websites. Please try again.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> DeleteAll(string? confirmation)
    {
        const string requiredConfirmation = "DELETE ALL";

        if (!string.Equals(confirmation?.Trim(), requiredConfirmation, StringComparison.Ordinal))
        {
            TempData["WarningMessage"] = $"Type \"{requiredConfirmation}\" to confirm deleting all websites.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            int deletedCount = await _context.Websites.ExecuteDeleteAsync();

            TempData["SuccessMessage"] = deletedCount == 1
                ? "1 website was deleted."
                : $"{deletedCount} websites were deleted.";
        }
        catch (DbUpdateException exception)
        {
            _logger.LogError(exception, "Database error while deleting all websites.");
            TempData["ErrorMessage"] = "Could not delete websites. Please try again.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> ExportWebsitesPdf(CancellationToken cancellationToken)
    {
        try
        {
            byte[] pdfBytes = await _websitePdfService.GenerateAllWebsitesPdfAsync(cancellationToken);
            string fileName = $"UsefulWebsites_{DateTime.UtcNow:yyyy-MM-dd_HH-mm}.pdf";

            return File(pdfBytes, "application/pdf", fileName);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new EmptyResult();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to generate PDF catalog.");
            TempData["ErrorMessage"] = "Could not generate PDF. Please try again.";
            return RedirectToAction(nameof(Index));
        }
    }

    private async Task<int> ResolveCategoryIdAsync(WebsiteFormViewModel viewModel)
    {
        if (!string.IsNullOrWhiteSpace(viewModel.NewCategoryName))
        {
            string newCategoryName = viewModel.NewCategoryName.Trim();

            var existingCategory = await _context.Categories
                .FirstOrDefaultAsync(category => category.Name == newCategoryName);

            if (existingCategory is not null)
            {
                return existingCategory.Id;
            }

            var newCategory = new Category
            {
                Name = newCategoryName,
                CreatedAtUtc = DateTime.UtcNow
            };

            _context.Categories.Add(newCategory);
            await _context.SaveChangesAsync();

            return newCategory.Id;
        }

        int categoryId = viewModel.CategoryId!.Value;
        bool categoryExists = await _context.Categories.AnyAsync(category => category.Id == categoryId);

        return categoryExists ? categoryId : 0;
    }

    private async Task<List<SelectListItem>> BuildCategoryOptionsAsync(int? selectedCategoryId)
    {
        var categories = await _context.Categories
            .AsNoTracking()
            .OrderBy(category => category.Name)
            .Select(category => new { category.Id, category.Name })
            .ToListAsync();

        return categories
            .Select(category => new SelectListItem
            {
                Value = category.Id.ToString(),
                Text = category.Name,
                Selected = selectedCategoryId.HasValue && category.Id == selectedCategoryId.Value
            })
            .ToList();
    }

    private static void NormalizeFormValues(WebsiteFormViewModel viewModel)
    {
        viewModel.Name = viewModel.Name.Trim();
        viewModel.Url = NormalizeHttpUrl(viewModel.Url);
        viewModel.LogoUrl = string.IsNullOrWhiteSpace(viewModel.LogoUrl)
            ? null
            : NormalizeHttpUrl(viewModel.LogoUrl);
        viewModel.Description = viewModel.Description.Trim();
        viewModel.NewCategoryName = string.IsNullOrWhiteSpace(viewModel.NewCategoryName)
            ? null
            : viewModel.NewCategoryName.Trim();
    }

    private static string NormalizeHttpUrl(string? value)
    {
        string normalizedValue = (value ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(normalizedValue))
        {
            return string.Empty;
        }

        if (!normalizedValue.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !normalizedValue.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            normalizedValue = $"https://{normalizedValue}";
        }

        if (!Uri.TryCreate(normalizedValue, UriKind.Absolute, out Uri? uri))
        {
            return normalizedValue;
        }

        var uriBuilder = new UriBuilder(uri)
        {
            Host = uri.Host.ToLowerInvariant()
        };

        return uriBuilder.Uri.AbsoluteUri.TrimEnd('/');
    }

    private void ValidateHttpUrl(string? value, string propertyName, string displayName)
    {
        bool isValid =
            Uri.TryCreate(value, UriKind.Absolute, out Uri? uri) &&
            !string.IsNullOrWhiteSpace(uri.Host) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

        if (!isValid)
        {
            ModelState.AddModelError(
                propertyName,
                $"{displayName} must start with http:// or https://.");
        }
    }
}
