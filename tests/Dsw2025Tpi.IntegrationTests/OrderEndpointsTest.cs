using System.Net;
using System.Net.Http.Json;
using Dsw2025Tpi.Application.Dtos;
using FluentAssertions;

namespace Dsw2025Tpi.IntegrationTests;

// Tests in this class share one SQLite database (IClassFixture), so every test uses its own SKUs and users.
public class OrdersEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public OrdersEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static OrderRequest NewOrder(Guid customerId, Guid productId, int quantity) =>
        new(CustomerId: customerId,
            ShippingAddress: "Calle Falsa 123",
            BillingAddress: "Calle Falsa 123",
            Notes: null,
            Items: new List<OrderItemRequest> { new(productId, quantity) });

    [Fact] // Test 8
    public async Task CreateOrder_WhenUnauthenticated_ReturnsUnauthorized()
    {
        var response = await _factory.CreateClient()
            .PostAsJsonAsync("/api/orders", NewOrder(Guid.NewGuid(), Guid.NewGuid(), 1));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact] // Test 9
    public async Task CreateOrder_WhenStockIsAvailable_ReturnsCreatedAndDecrementsStock()
    {
        var admin = await TestHelpers.CreateAdminClientAsync(_factory);
        var product = await TestHelpers.CreateProductAsync(admin, TestHelpers.NewProduct("ORD-01", stock: 10));
        var (customer, customerId) = await TestHelpers.CreateCustomerClientAsync(_factory, "cliente.stock");

        var response = await customer.PostAsJsonAsync("/api/orders", NewOrder(customerId, product.Id, 3));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var updated = await TestHelpers.GetProductAsync(customer, product.Id);
        updated.StockQuantity.Should().Be(7);
    }

    [Fact] // Test 10
    public async Task CreateOrder_WhenStockIsInsufficient_ReturnsBadRequestAndKeepsStock()
    {
        // ExceptionHandlingMiddleware maps InsufficientStockException to 400 Bad Request.
        var admin = await TestHelpers.CreateAdminClientAsync(_factory);
        var product = await TestHelpers.CreateProductAsync(admin, TestHelpers.NewProduct("ORD-02", stock: 2));
        var (customer, customerId) = await TestHelpers.CreateCustomerClientAsync(_factory, "cliente.sinstock");

        var response = await customer.PostAsJsonAsync("/api/orders", NewOrder(customerId, product.Id, 5));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var unchanged = await TestHelpers.GetProductAsync(customer, product.Id);
        unchanged.StockQuantity.Should().Be(2);
    }

    [Fact] // Test 11
    public async Task GetOrderById_AfterCreation_ReturnsOrderWithCorrectTotal()
    {
        var admin = await TestHelpers.CreateAdminClientAsync(_factory);
        var product = await TestHelpers.CreateProductAsync(admin, TestHelpers.NewProduct("ORD-03", stock: 10, price: 100m));
        var (customer, customerId) = await TestHelpers.CreateCustomerClientAsync(_factory, "cliente.total");
        var created = await customer.PostAsJsonAsync("/api/orders", NewOrder(customerId, product.Id, 2));
        var order = await created.Content.ReadFromJsonAsync<OrderResponse>(TestHelpers.JsonOptions);

        var response = await customer.GetAsync($"/api/orders/{order!.OrderId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetched = await response.Content.ReadFromJsonAsync<OrderResponse>(TestHelpers.JsonOptions);
        fetched!.TotalAmount.Should().Be(200m);
        fetched.Items.Should().ContainSingle().Which.Quantity.Should().Be(2);
    }
}
