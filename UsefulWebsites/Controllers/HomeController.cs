using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UsefulWebsites.Data;
using UsefulWebsites.Models;
using UsefulWebsites.ViewModels;

namespace UsefulWebsites.Controllers;

public sealed class HomeController : Controller
{
    private readonly ApplicationDbContext _context;

    public HomeController(ApplicationDbContext context)
    {
        _context = context;
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

        int totalWebsiteCount = await _context.Websites
            .AsNoTracking()
            .CountAsync();

        var websiteQuery = _context.Websites
            .AsNoTracking()
            .AsQueryable();

        if (selectedCategoryId.HasValue)
        {
            websiteQuery = websiteQuery.Where(website => website.CategoryId == selectedCategoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            websiteQuery = websiteQuery.Where(website => website.Name.Contains(searchTerm));
        }

        var websites = await websiteQuery
            .OrderByDescending(website => website.CreatedAtUtc)
            .ThenByDescending(website => website.Id)
            .Select(website => new WebsiteCardViewModel
            {
                Id = website.Id,
                Name = website.Name,
                Url = website.Url,
                LogoUrl = website.LogoUrl,
                Description = website.Description,
                CategoryId = website.CategoryId,
                CategoryName = website.Category.Name,
                CreatedAtUtc = website.CreatedAtUtc
            })
            .ToListAsync();

        var viewModel = new HomeIndexViewModel
        {
            Categories = categories,
            Websites = websites,
            SelectedCategoryId = selectedCategoryId,
            SearchTerm = searchTerm,
            TotalWebsiteCount = totalWebsiteCount
        };

        ViewData["Title"] = "Home";

        return View(viewModel);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
        });
    }
}
