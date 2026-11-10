public class OrdersManagementServiceTests
{
[Fact] // Test 9: CreateOrder_WhenProductStockIsInsufficient_ThrowsInsufficientStockException
public async Task CreateOrder_WhenProductStockIsInsufficient_ThrowsInsufficientStockException()
{
var productRepoMock = new Mock<IProductRepository>();
var orderRepoMock = new Mock<IOrderRepository>();
var product = new Product { Id = 1, StockQuantity = 2, CurrentUnitPrice = 100m };
productRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(product);
var service = new OrdersManagementService(orderRepoMock.Object, productRepoMock.Object);
var orderDto = new OrderCreateDto { CustomerId = 1, Items = new List<OrderItemDto> { new() {
ProductId = 1, Quantity = 5 } } };
var act = async () => await service.CreateOrderAsync(orderDto);
await act.Should().ThrowAsync<InsufficientStockException>();
orderRepoMock.Verify(r => r.AddAsync(It.IsAny<Order>()), Times.Never);
}
}