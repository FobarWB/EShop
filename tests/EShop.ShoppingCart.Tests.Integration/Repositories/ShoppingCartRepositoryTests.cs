using EShop.Data.Entities;
using EShop.ShoppingCart.Repositories;
using Microsoft.EntityFrameworkCore;

namespace EShop.ShoppingCart.Tests.Integration.Repositories;

// Arrange, act and assert each use their own DbContext, so assertions read what is
// stored in PostgreSQL, not entities cached by the change tracker.
public class ShoppingCartRepositoryTests : IAsyncLifetime
{
    private const string UserId = "user-1";

    private readonly ShoppingCartDatabase _database;
    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;

    public ShoppingCartRepositoryTests(ShoppingCartDatabase database)
    {
        _database = database;
    }

    public async ValueTask InitializeAsync() => await _database.ResetAsync(_cancellationToken);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task AddItemToCart_FirstItem_CreatesCartWithItem()
    {
        var productId = await _database.SeedProductAsync(10, _cancellationToken);

        var result = await ActAsync(r =>
            r.AddItemToCartAsync(UserId, Item(productId, 2), _cancellationToken)
        );

        result.ShouldBe(AddItemResult.Added);
        var cart = (await _database.LoadCartAsync(UserId, _cancellationToken)).ShouldNotBeNull();
        cart.CartItems.ShouldHaveSingleItem().Quantity.ShouldBe(2);
    }

    [Fact]
    public async Task AddItemToCart_SameProductAgain_AddsUpQuantity()
    {
        var productId = await _database.SeedProductAsync(10, _cancellationToken);
        await ActAsync(r => r.AddItemToCartAsync(UserId, Item(productId, 2), _cancellationToken));

        await ActAsync(r => r.AddItemToCartAsync(UserId, Item(productId, 3), _cancellationToken));

        (await _database.LoadCartAsync(UserId, _cancellationToken))
            .ShouldNotBeNull()
            .CartItems.ShouldHaveSingleItem()
            .Quantity.ShouldBe(5);
    }

    [Fact]
    public async Task AddItemToCart_ProductMissing_ReturnsProductNotFoundAndCreatesNoCart()
    {
        var result = await ActAsync(r =>
            r.AddItemToCartAsync(UserId, Item(999, 1), _cancellationToken)
        );

        result.ShouldBe(AddItemResult.ProductNotFound);
        (await _database.LoadCartAsync(UserId, _cancellationToken)).ShouldBeNull();
    }

    [Fact]
    public async Task AddItemToCart_ExceedsStock_ReturnsInsufficientStockAndCreatesNoCart()
    {
        var productId = await _database.SeedProductAsync(3, _cancellationToken);

        var result = await ActAsync(r =>
            r.AddItemToCartAsync(UserId, Item(productId, 4), _cancellationToken)
        );

        result.ShouldBe(AddItemResult.InsufficientStock);
        (await _database.LoadCartAsync(UserId, _cancellationToken)).ShouldBeNull();
    }

    [Fact]
    public async Task AddItemToCart_OutOfStock_ReturnsInsufficientStockAndCreatesNoCart()
    {
        var productId = await _database.SeedProductAsync(0, _cancellationToken);

        var result = await ActAsync(r =>
            r.AddItemToCartAsync(UserId, Item(productId, 1), _cancellationToken)
        );

        result.ShouldBe(AddItemResult.InsufficientStock);
        (await _database.LoadCartAsync(UserId, _cancellationToken)).ShouldBeNull();
    }

    [Fact]
    public async Task AddItemToCart_QuantityInCartCountsTowardsStock()
    {
        var productId = await _database.SeedProductAsync(5, _cancellationToken);
        await ActAsync(r => r.AddItemToCartAsync(UserId, Item(productId, 3), _cancellationToken));

        var result = await ActAsync(r =>
            r.AddItemToCartAsync(UserId, Item(productId, 3), _cancellationToken)
        );

        result.ShouldBe(AddItemResult.InsufficientStock);
        (await _database.LoadCartAsync(UserId, _cancellationToken))
            .ShouldNotBeNull()
            .CartItems.ShouldHaveSingleItem()
            .Quantity.ShouldBe(3);
    }

    [Fact]
    public async Task CreateCart_SecondCartForSameUser_IsRejectedByUniqueIndex()
    {
        await ActAsync(r => r.CreateCartAsync(new Cart { UserId = UserId }));

        await Should.ThrowAsync<DbUpdateException>(
            ActAsync(r => r.CreateCartAsync(new Cart { UserId = UserId }))
        );
    }

    [Fact]
    public async Task RetrieveCartByUserId_UnknownUser_ReturnsNull()
    {
        var cart = await ActAsync(r => r.RetrieveCartByUserIdAsync("nobody"));

        cart.ShouldBeNull();
    }

    [Fact]
    public async Task UpdateItemQuantity_WithinStock_UpdatesQuantity()
    {
        var productId = await SeedCartWithItemAsync(unitsInStock: 10, quantity: 1);

        var updated = await ActAsync(r => r.UpdateItemQuantityAsync(UserId, productId, 4));

        updated.ShouldBeTrue();
        (await _database.LoadCartAsync(UserId, _cancellationToken))
            .ShouldNotBeNull()
            .CartItems.ShouldHaveSingleItem()
            .Quantity.ShouldBe(4);
    }

    [Fact]
    public async Task UpdateItemQuantity_AboveStock_ReturnsFalseAndKeepsQuantity()
    {
        var productId = await SeedCartWithItemAsync(unitsInStock: 5, quantity: 1);

        var updated = await ActAsync(r => r.UpdateItemQuantityAsync(UserId, productId, 6));

        updated.ShouldBeFalse();
        (await _database.LoadCartAsync(UserId, _cancellationToken))
            .ShouldNotBeNull()
            .CartItems.ShouldHaveSingleItem()
            .Quantity.ShouldBe(1);
    }

    [Fact]
    public async Task UpdateItemQuantity_OutOfStock_ReturnsFalseAndKeepsQuantity()
    {
        var productId = await _database.SeedProductAsync(0, _cancellationToken);
        await _database.SeedAsync(
            new Cart { UserId = UserId, CartItems = [Item(productId, 1)] },
            _cancellationToken
        );

        var updated = await ActAsync(r => r.UpdateItemQuantityAsync(UserId, productId, 2));

        updated.ShouldBeFalse();
        (await _database.LoadCartAsync(UserId, _cancellationToken))
            .ShouldNotBeNull()
            .CartItems.ShouldHaveSingleItem()
            .Quantity.ShouldBe(1);
    }

    [Fact]
    public async Task UpdateItemQuantity_ItemNotInCart_ReturnsFalse()
    {
        await SeedCartWithItemAsync(unitsInStock: 10, quantity: 1);
        var otherProductId = await _database.SeedProductAsync(10, _cancellationToken);

        var updated = await ActAsync(r => r.UpdateItemQuantityAsync(UserId, otherProductId, 2));

        updated.ShouldBeFalse();
    }

    [Fact]
    public async Task RemoveItemFromCart_RemovesOnlyThatItem()
    {
        var removedId = await SeedCartWithItemAsync(unitsInStock: 10, quantity: 1);
        var keptId = await _database.SeedProductAsync(10, _cancellationToken);
        await ActAsync(r => r.AddItemToCartAsync(UserId, Item(keptId, 1), _cancellationToken));

        var removed = await ActAsync(r => r.RemoveItemFromCartAsync(UserId, removedId));

        removed.ShouldBeTrue();
        (await _database.LoadCartAsync(UserId, _cancellationToken))
            .ShouldNotBeNull()
            .CartItems.ShouldHaveSingleItem()
            .ProductId.ShouldBe(keptId);
    }

    [Fact]
    public async Task RemoveItemFromCart_ItemNotInCart_ReturnsFalse()
    {
        await SeedCartWithItemAsync(unitsInStock: 10, quantity: 1);

        var removed = await ActAsync(r => r.RemoveItemFromCartAsync(UserId, 999));

        removed.ShouldBeFalse();
    }

    [Fact]
    public async Task ClearCart_RemovesItemsAndKeepsCart()
    {
        await SeedCartWithItemAsync(unitsInStock: 10, quantity: 1);

        var cleared = await ActAsync(r => r.ClearCartAsync(UserId));

        cleared.ShouldBeTrue();
        (await _database.LoadCartAsync(UserId, _cancellationToken))
            .ShouldNotBeNull()
            .CartItems.ShouldBeEmpty();
    }

    [Fact]
    public async Task DeleteCart_DeletesCartAndItsItems()
    {
        await SeedCartWithItemAsync(unitsInStock: 10, quantity: 1);

        var deleted = await ActAsync(r => r.DeleteCartAsync(UserId));

        deleted.ShouldBe(true);
        (await _database.LoadCartAsync(UserId, _cancellationToken)).ShouldBeNull();
        await using var db = _database.CreateContext();
        (await db.CartItems.CountAsync(_cancellationToken)).ShouldBe(0); // ON DELETE CASCADE
    }

    [Fact]
    public async Task DeleteCart_UnknownUser_ReturnsNull()
    {
        var deleted = await ActAsync(r => r.DeleteCartAsync("nobody"));

        deleted.ShouldBeNull();
    }

    [Fact]
    public async Task UpdateItemQuantity_NoCart_ReturnsFalse()
    {
        var productId = await _database.SeedProductAsync(10, _cancellationToken);

        var updated = await ActAsync(r => r.UpdateItemQuantityAsync(UserId, productId, 2));

        updated.ShouldBeFalse();
    }

    [Fact]
    public async Task RemoveItemFromCart_NoCart_ReturnsFalse()
    {
        var removed = await ActAsync(r => r.RemoveItemFromCartAsync(UserId, 999));

        removed.ShouldBeFalse();
    }

    [Fact]
    public async Task ClearCart_NoCart_ReturnsFalse()
    {
        var cleared = await ActAsync(r => r.ClearCartAsync(UserId));

        cleared.ShouldBeFalse();
    }

    private static CartItem Item(int productId, int quantity) =>
        new() { ProductId = productId, Quantity = quantity };

    // Runs one repository call on a fresh DbContext, as one HTTP request would.
    private async Task<T> ActAsync<T>(Func<ShoppingCartRepository, Task<T>> action)
    {
        await using var db = _database.CreateContext();
        return await action(new ShoppingCartRepository(db));
    }

    private async Task<int> SeedCartWithItemAsync(short unitsInStock, int quantity)
    {
        var productId = await _database.SeedProductAsync(unitsInStock, _cancellationToken);
        await ActAsync(r =>
            r.AddItemToCartAsync(UserId, Item(productId, quantity), _cancellationToken)
        );
        return productId;
    }
}
