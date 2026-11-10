using Xunit;
using Moq;
using FluentAssertions;
using Dsw2025Tpi.Domain.Entities;
using Dsw2025Tpi.Application.Services;
using Dsw2025Tpi.Domain.Interfaces;
using Dsw2025Tpi.Domain.Exceptions;
public class ProductsManagementServiceTests
{
[Fact] // Test 1: AddProduct_WithValidData_ReturnsProductAndCallsRepositoryAdd
public async Task AddProduct_WithValidData_ReturnsProductAndCallsRepositoryAdd()
{
var repoMock = new Mock<IProductRepository>();
var newProduct = new Product { Id = 1, Sku = "PROD-01", Name = "Laptop", CurrentUnitPrice = 1200m,
StockQuantity = 10 };
repoMock.Setup(r => r.AddAsync(It.IsAny<Product>())).ReturnsAsync(newProduct);
var service = new ProductsManagementService(repoMock.Object);
var result = await service.AddProductAsync(newProduct);
result.Should().NotBeNull();
result.Id.Should().Be(1);
repoMock.Verify(r => r.AddAsync(It.IsAny<Product>()), Times.Once);
}

[Fact] // Test 2: AddProduct_WithDuplicateSku_ThrowsDuplicatedEntityException
public async Task AddProduct_WithDuplicateSku_ThrowsDuplicatedEntityException()
{
var repoMock = new Mock<IProductRepository>();
repoMock.Setup(r => r.ExistsBySkuAsync("PROD-01")).ReturnsAsync(true);
var service = new ProductsManagementService(repoMock.Object);
var product = new Product { Sku = "PROD-01", Name = "Laptop", CurrentUnitPrice = 1200m, StockQuantity
= 10 };
var act = async () => await service.AddProductAsync(product);
await act.Should().ThrowAsync<DuplicatedEntityException>();
repoMock.Verify(r => r.AddAsync(It.IsAny<Product>()), Times.Never);
}

[Theory] // Tests 3, 4, 5: AddProduct_WithInvalidStockOrPrice_ThrowsInvalidDataException
[InlineData(0, 100)]
[InlineData(-5, 100)]
[InlineData(10, -10)]
public async Task AddProduct_WithInvalidStockOrPrice_ThrowsInvalidDataException(int stock, decimal price)
{
var repoMock = new Mock<IProductRepository>();
var service = new ProductsManagementService(repoMock.Object);
var product = new Product { Sku = "PROD-01", Name = "Test", CurrentUnitPrice = price, StockQuantity =
stock };
var act = async () => await service.AddProductAsync(product);
await act.Should().ThrowAsync<InvalidDataException>();
repoMock.Verify(r => r.AddAsync(It.IsAny<Product>()), Times.Never);
}

[Fact] // Test 6: GetProducts_WhenIncludeInactiveIsFalse_ReturnsOnlyActiveProducts
public async Task GetProducts_WhenIncludeInactiveIsFalse_ReturnsOnlyActiveProducts()
{
var repoMock = new Mock<IProductRepository>();
var list = new List<Product> {
new Product { Id = 1, IsActive = true, Name = "Prod 1" },
new Product { Id = 2, IsActive = false, Name = "Prod 2" }
};
repoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(list);
var service = new ProductsManagementService(repoMock.Object);
var result = await service.GetProductsAsync(includeInactive: false);
result.Should().HaveCount(1);
}

[Fact] // Test 7: GetProducts_WithPaginationFilter_AppliesSkipAndTakeCorrectly
public async Task GetProducts_WithPaginationFilter_AppliesSkipAndTakeCorrectly()
{

 var repoMock = new Mock<IProductRepository>();
 var list = Enumerable.Range(1, 15).Select(i => new Product { Id = i, Name = $"Prod {i}", IsActive =
true }).ToList();
repoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(list);
var service = new ProductsManagementService(repoMock.Object);

    result.Should().HaveCount(5);
    result.First().Id.Should().Be(6);
}

[Fact] // Test 8: GetProducts_WhenPageOutOfBounds_ReturnsEmptyList
public async Task GetProducts_WhenPageOutOfBounds_ReturnsEmptyList()
{
    var repoMock = new Mock<IProductRepository>();
    var list = Enumerable.Range(1, 5).Select(i => new Product { Id = i, Name = $"Prod {i}", IsActive = true }).ToList();
    repoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(list);
    var service = new ProductsManagementService(repoMock.Object);

    // Act (pidiendo una página muy lejana)
    var result = await service.GetProductsAsync(page: 10, pageSize: 5);

    // Assert
    result.Should().HaveCount(1);
    result.First().Name.Should().Be("Mouse Gamer");

}
}