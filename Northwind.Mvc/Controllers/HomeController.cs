using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Northwind.Mvc.Models;
using EShop.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal; // Include extension method
using Microsoft.AspNetCore.Authorization; // [Authorize]


namespace Northwind.Mvc.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly NorthwindContext db;
    private readonly IHttpClientFactory clientFactory;

    public HomeController(ILogger<HomeController> logger, NorthwindContext injectedContext, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        db = injectedContext;
        clientFactory = httpClientFactory;
    }

    [ResponseCache(Duration = 10, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> Index()
    {
        try
        {
            HttpClient categoryClient = clientFactory.CreateClient("Northwind.CategoryApi");
            HttpClient productClient = clientFactory.CreateClient("Northwind.ProductApi");

            HttpRequestMessage categoryRequest = new(HttpMethod.Get, "api/categories");
            HttpRequestMessage productRequest = new(HttpMethod.Get, "api/products");

            Task<HttpResponseMessage> categoryResponseTask = categoryClient.SendAsync(categoryRequest);
            Task<HttpResponseMessage> productResponseTask = productClient.SendAsync(productRequest);
            await Task.WhenAll(categoryResponseTask, productResponseTask);

            await AddDataToViewData<Category>(await categoryResponseTask, "categories");
            await AddDataToViewData<Product>(await productResponseTask, "products");
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Error occurred: {ex.Message}");
            ViewData["categories"] = Enumerable.Empty<Category>().ToArray();
            ViewData["products"] = Enumerable.Empty<Product>().ToArray();
        }
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    public IActionResult Test()
    {
        return View();
    }

    private async Task AddDataToViewData<T>(HttpResponseMessage responseMessage, string index)
    {
        if (responseMessage.IsSuccessStatusCode)
        {
            ViewData[index] = await responseMessage.Content.ReadFromJsonAsync<T[]>();
        }
        else
        {
            _logger.LogWarning("The API service returned an error.");
            ViewData[index] = Enumerable.Empty<T>().ToArray();
        }
    }
}