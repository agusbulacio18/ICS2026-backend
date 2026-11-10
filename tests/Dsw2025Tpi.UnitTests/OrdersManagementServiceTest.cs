using Dsw2025Tpi.Application.Dtos;
using Dsw2025Tpi.Application.Exceptions;
using Dsw2025Tpi.Application.Services;
using Dsw2025Tpi.Domain.Entities;
using Dsw2025Tpi.Domain.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Dsw2025Tpi.UnitTests;

public class OrdersManagementServiceTests
{
    [Fact] // Test 9
    public async Task CreateOrder_WhenProductStockIsInsufficient_ThrowsInsufficientStockException()
    {
        var repoMock = new Mock<IRepository>();
        var customer = new Customer { Name = "Cliente Test", Email = "cliente@test.com" };
        var product = new Product("PROD-01", "Laptop", 100m, stock: 2);

        repoMock
            .Setup(r => r.GetById<Customer>(customer.Id, It.IsAny<string[]>()))
            .ReturnsAsync(customer);
        repoMock
            .Setup(r => r.GetById<Product>(product.Id, It.IsAny<string[]>()))
            .ReturnsAsync(product);

        var service = new OrdersManagementService(
            repoMock.Object,
            NullLogger<OrdersManagementService>.Instance);

        var request = new OrderRequest(
            CustomerId: customer.Id,
            ShippingAddress: "Laprida 456",
            BillingAddress: "Laprida 456",
            Notes: null,
            Items: new List<OrderItemRequest> { new(ProductId: product.Id, Quantity: 5) });

        var act = async () => await service.CreateOrder(request);

        await act.Should().ThrowAsync<InsufficientStockException>();
        product.StockQuantity.Should().Be(2);
        repoMock.Verify(r => r.Update(It.IsAny<Product>()), Times.Never);
        repoMock.Verify(r => r.Add(It.IsAny<Order>()), Times.Never);
    }
}