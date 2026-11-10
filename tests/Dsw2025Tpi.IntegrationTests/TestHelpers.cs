using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dsw2025Tpi.Application.Dtos;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Dsw2025Tpi.IntegrationTests;

public static class TestHelpers
{
    public const string AdminUsername = "admin.test";
    public const string AdminPassword = "Admin123!";
    public const string CustomerPassword = "Cliente123!";

    // Same conventions as the API: camelCase and enums as strings.
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Ensures an Admin user exists in the test database, logs in through
    /// /api/auth/login and returns a client carrying the issued JWT.
    /// </summary>
    public static async Task<HttpClient> CreateAdminClientAsync(CustomWebApplicationFactory factory)
    {
        await EnsureAdminUserAsync(factory);

        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginModel(AdminUsername, AdminPassword));
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        SetBearerToken(client, body.GetProperty("token").GetString()!);
        return client;
    }

    /// <summary>
    /// Registers a new customer through /api/auth/register and returns a client
    /// carrying the issued JWT plus the created customer id.
    /// </summary>
    public static async Task<(HttpClient Client, Guid CustomerId)> CreateCustomerClientAsync(
        CustomWebApplicationFactory factory, string username)
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterModel(username, CustomerPassword, $"{username}@test.com"));
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        SetBearerToken(client, body.GetProperty("token").GetString()!);
        return (client, body.GetProperty("customerId").GetGuid());
    }

    public static ProductModel.Request NewProduct(string sku, string? name = null, int stock = 10, decimal price = 100m) =>
        new(Sku: sku, InternalCode: $"INT-{sku}", Name: name ?? $"Product {sku}",
            Description: null, CurrentUnitPrice: price, StockQuantity: stock);

    public static async Task<ProductModel.ResponseProduct> CreateProductAsync(
        HttpClient adminClient, ProductModel.Request request)
    {
        var response = await adminClient.PostAsJsonAsync("/api/products", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProductModel.ResponseProduct>(JsonOptions))!;
    }

    public static async Task<ProductModel.ResponseProduct> GetProductAsync(HttpClient client, Guid id)
    {
        var response = await client.GetAsync($"/api/products/{id}");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProductModel.ResponseProduct>(JsonOptions))!;
    }

    private static async Task EnsureAdminUserAsync(CustomWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

        if (await userManager.FindByNameAsync(AdminUsername) != null)
        {
            return;
        }

        var admin = new IdentityUser { UserName = AdminUsername, Email = "admin@test.com", EmailConfirmed = true };
        var created = await userManager.CreateAsync(admin, AdminPassword);
        if (!created.Succeeded)
        {
            throw new InvalidOperationException(
                "Could not create test admin: " + string.Join(", ", created.Errors.Select(e => e.Description)));
        }

        // The "Admin" role is created by Program.Main on startup.
        await userManager.AddToRoleAsync(admin, "Admin");
    }

    private static void SetBearerToken(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
}
