using System.Linq.Expressions;
using Dsw2025Tpi.Application.Dtos;
using Dsw2025Tpi.Application.Exceptions;
using Dsw2025Tpi.Application.Services;
using Dsw2025Tpi.Domain.Entities;
using Dsw2025Tpi.Domain.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Dsw2025Tpi.UnitTests;

public class ProductsManagementServiceTests
{
    private readonly Mock<IRepository> _repoMock = new();
    private readonly ProductsManagementService _service;

    public ProductsManagementServiceTests()
    {
        _service = new ProductsManagementService(
            _repoMock.Object,
            NullLogger<ProductsManagementService>.Instance);
    }

    private static ProductModel.Request ValidRequest(int stock = 10, decimal price = 1200m) =>
        new(Sku: "PROD-01", InternalCode: "INT-01", Name: "Laptop",
            Description: null, CurrentUnitPrice: price, StockQuantity: stock);

    // Makes the mocked GetFiltered behave like the real repository:
    // it applies the predicate built by the service to an in-memory list.
    private void SetupGetFilteredOver(List<Product> products)
    {
        _repoMock
            .Setup(r => r.GetFiltered<Product>(
                It.IsAny<Expression<Func<Product, bool>>>(), It.IsAny<string[]>()))
            .ReturnsAsync((Expression<Func<Product, bool>> predicate, string[] _) =>
                (IEnumerable<Product>?)products.Where(predicate.Compile()).ToList());
    }

    [Fact] // Test 1
    public async Task AddProduct_WithValidData_ReturnsProductAndCallsRepositoryAdd()
    {
        _repoMock
            .Setup(r => r.Add(It.IsAny<Product>()))
            .ReturnsAsync((Product p) => p);

        var result = await _service.AddProduct(ValidRequest());

        result.Should().NotBeNull();
        result.Id.Should().NotBeEmpty();
        result.Sku.Should().Be("PROD-01");
        result.IsActive.Should().BeTrue();
        _repoMock.Verify(r => r.Add(It.IsAny<Product>()), Times.Once);
    }

    [Fact] // Test 2
    public async Task AddProduct_WithDuplicateSku_ThrowsDuplicatedEntityException()
    {
        _repoMock
            .Setup(r => r.First(It.IsAny<Expression<Func<Product, bool>>>(), It.IsAny<string[]>()))
            .ReturnsAsync(new Product("PROD-01", "Existing", 100m, 5));

        var act = async () => await _service.AddProduct(ValidRequest());

        await act.Should().ThrowAsync<DuplicatedEntityException>();
        _repoMock.Verify(r => r.Add(It.IsAny<Product>()), Times.Never);
    }

    [Theory] // Tests 3, 4, 5
    [InlineData(0, 100)]
    [InlineData(-5, 100)]
    [InlineData(10, -10)]
    public async Task AddProduct_WithInvalidStockOrPrice_ThrowsInvalidDataException(int stock, decimal price)
    {
        var act = async () => await _service.AddProduct(ValidRequest(stock, price));

        await act.Should().ThrowAsync<InvalidDataException>();
        _repoMock.Verify(r => r.Add(It.IsAny<Product>()), Times.Never);
    }

    [Fact] // Test 6
    public async Task GetProducts_WhenIncludeInactiveIsFalse_ReturnsOnlyActiveProducts()
    {
        SetupGetFilteredOver(new List<Product>
        {
            new() { Sku = "A", Name = "Prod 1", InternalCode = "I1", IsActive = true },
            new() { Sku = "B", Name = "Prod 2", InternalCode = "I2", IsActive = false },
        });
        var filter = new ProductModel.FilterProduct(Status: null, Search: null, PageNumber: null, PageSize: null);

        var result = await _service.GetProducts(filter, includeInactive: false);

        result.Total.Should().Be(1);
        result.ProductItems.Should().ContainSingle()
            .Which.Name.Should().Be("Prod 1");
    }

    [Fact] // Test 7
    public async Task GetProducts_WithPaginationFilter_AppliesSkipAndTakeCorrectly()
    {
        // Zero-padded names: the service orders by Name, and "Prod 10" < "Prod 2" as strings.
        SetupGetFilteredOver(Enumerable.Range(1, 15)
            .Select(i => new Product { Sku = $"SKU-{i:00}", Name = $"Prod {i:00}", InternalCode = $"I{i}", IsActive = true })
            .ToList());
        var filter = new ProductModel.FilterProduct(Status: null, Search: null, PageNumber: 2, PageSize: 5);

        var result = await _service.GetProducts(filter);

        result.Total.Should().Be(15);
        result.ProductItems.Should().HaveCount(5);
        result.ProductItems.First().Name.Should().Be("Prod 06");
        result.ProductItems.Last().Name.Should().Be("Prod 10");
    }

    [Fact] // Test 8
    public async Task GetProducts_WhenPageOutOfBounds_ReturnsEmptyList()
    {
        SetupGetFilteredOver(Enumerable.Range(1, 5)
            .Select(i => new Product { Sku = $"SKU-{i}", Name = $"Prod {i}", InternalCode = $"I{i}", IsActive = true })
            .ToList());
        var filter = new ProductModel.FilterProduct(Status: null, Search: null, PageNumber: 10, PageSize: 5);

        var result = await _service.GetProducts(filter);

        result.ProductItems.Should().BeEmpty();
        result.Total.Should().Be(5);
    }
}