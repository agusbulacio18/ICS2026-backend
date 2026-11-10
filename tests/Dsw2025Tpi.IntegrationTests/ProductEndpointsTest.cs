using System.Net;
using System.Net.Http.Json;
using Dsw2025Tpi.Application.Dtos;
using Dsw2025Tpi.Data;
using Dsw2025Tpi.Domain.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dsw2025Tpi.IntegrationTests;

// Tests in this class share one SQLite database (IClassFixture), so every test uses its own SKUs.
public class ProductsEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _anonymousClient;

    public ProductsEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _anonymousClient = factory.CreateClient();
    }

    [Fact] // Test 1
    public async Task AddProduct_WhenUnauthenticatedOrNotAdmin_ReturnsUnauthorizedOrForbidden()
    {
        var request = TestHelpers.NewProduct("SEC-01");
        var (customerClient, _) = await TestHelpers.CreateCustomerClientAsync(_factory, "no.admin");

        var anonymousResponse = await _anonymousClient.PostAsJsonAsync("/api/products", request);
        var customerResponse = await customerClient.PostAsJsonAsync("/api/products", request);

        anonymousResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        customerResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await CountBySkuAsync("SEC-01")).Should().Be(0);
    }

    [Fact] // Test 2
    public async Task AddProduct_WhenAuthenticatedAsAdmin_PersistsInSqliteAndReturnsCreated()
    {
        var admin = await TestHelpers.CreateAdminClientAsync(_factory);

        var response = await admin.PostAsJsonAsync("/api/products", TestHelpers.NewProduct("PROD-NEW", "Nuevo Producto"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        var created = await response.Content.ReadFromJsonAsync<ProductModel.ResponseProduct>(TestHelpers.JsonOptions);
        created!.Sku.Should().Be("PROD-NEW");
        created.IsActive.Should().BeTrue();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Dsw2025TpiContext>();
        var stored = await db.Set<Product>().SingleAsync(p => p.Id == created.Id);
        stored.Name.Should().Be("Nuevo Producto");
        stored.StockQuantity.Should().Be(10);
    }

    [Fact] // Test 3
    public async Task AddProduct_WhenSkuAlreadyExistsInDatabase_ReturnsBadRequest()
    {
        // ExceptionHandlingMiddleware maps DuplicatedEntityException to 400 Bad Request.
        var admin = await TestHelpers.CreateAdminClientAsync(_factory);
        var request = TestHelpers.NewProduct("DUPL-SKU", "Duplicado", stock: 5, price: 50m);

        var first = await admin.PostAsJsonAsync("/api/products", request);
        var second = await admin.PostAsJsonAsync("/api/products", request);

        first.StatusCode.Should().Be(HttpStatusCode.Created);
        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await CountBySkuAsync("DUPL-SKU")).Should().Be(1);
    }

    [Fact] // Test 4
    public async Task GetProducts_PublicEndpoint_ReturnsOkWithPaginatedJsonStructure()
    {
        var admin = await TestHelpers.CreateAdminClientAsync(_factory);
        await TestHelpers.CreateProductAsync(admin, TestHelpers.NewProduct("PUB-01", "Publico"));

        var response = await _anonymousClient.GetAsync("/api/products?search=PUB-01");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<ProductModel.ResponsePagination>(TestHelpers.JsonOptions);
        page!.Total.Should().Be(1);
        page.ProductItems.Should().ContainSingle().Which.Sku.Should().Be("PUB-01");
    }

    [Fact] // Test 5
    public async Task GetProducts_WhenNoProductMatches_ReturnsNoContent()
    {
        var response = await _anonymousClient.GetAsync("/api/products?search=NO-MATCH-XYZ");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact] // Test 6
    public async Task GetProducts_WithQueryStringFilters_ReturnsFilteredResultsFromSqlite()
    {
        var admin = await TestHelpers.CreateAdminClientAsync(_factory);
        await TestHelpers.CreateProductAsync(admin, TestHelpers.NewProduct("FLT-01", "Laptop Filtro"));
        await TestHelpers.CreateProductAsync(admin, TestHelpers.NewProduct("FLT-02", "Mouse Filtro"));

        var response = await _anonymousClient.GetAsync("/api/products?search=Laptop&pageNumber=1&pageSize=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<ProductModel.ResponsePagination>(TestHelpers.JsonOptions);
        page!.ProductItems.Should().OnlyContain(p => p.Name.Contains("Laptop"));
        page.ProductItems.Select(p => p.Sku).Should().Contain("FLT-01").And.NotContain("FLT-02");
    }

    [Fact] // Test 7
    public async Task GetAuthProducts_AdminEndpoint_WhenCalledByAdmin_IncludesInactiveProducts()
    {
        var admin = await TestHelpers.CreateAdminClientAsync(_factory);
        var product = await TestHelpers.CreateProductAsync(admin, TestHelpers.NewProduct("INACT-01", "Inactivo"));
        var disable = await admin.PatchAsync($"/api/products/{product.Id}", content: null);
        disable.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var adminResponse = await admin.GetAsync("/api/products/admin?search=INACT-01");
        var publicResponse = await _anonymousClient.GetAsync("/api/products?search=INACT-01");

        adminResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await adminResponse.Content.ReadFromJsonAsync<ProductModel.ResponsePagination>(TestHelpers.JsonOptions);
        page!.ProductItems.Should().ContainSingle().Which.IsActive.Should().BeFalse();
        publicResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private async Task<int> CountBySkuAsync(string sku)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Dsw2025TpiContext>();
        return await db.Set<Product>().CountAsync(p => p.Sku == sku);
    }
}
