using Cronus.Ordering.Contracts;
using Cronus.Ordering.Data;
using Cronus.Ordering.Errors;
using Cronus.Ordering.Models;
using Microsoft.EntityFrameworkCore;

namespace Cronus.Ordering.Services;

/// <summary>
/// Owns the cart rules. Cronus has no accounts yet, so there is a single open cart, locked to the
/// restaurant of its first item. An empty cart is discarded rather than kept as a locked shell, so
/// the customer can freely switch restaurants once they have removed everything.
/// </summary>
public sealed class CartService(OrderingDbContext db, ILogger<CartService> logger)
{
    public Task<Cart?> GetOpenCartAsync(CancellationToken cancellationToken) =>
        db.Carts
            .Include(cart => cart.Restaurant)
            .Include(cart => cart.Items)
            .ThenInclude(cartItem => cartItem.MenuItem)
            .Where(cart => cart.Status == CartStatus.Open)
            .OrderByDescending(cart => cart.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<Cart> AddItemAsync(Guid menuItemId, int quantity, CancellationToken cancellationToken)
    {
        var menuItem = await db.MenuItems
            .Include(item => item.Restaurant)
            .SingleOrDefaultAsync(item => item.Id == menuItemId, cancellationToken)
            ?? throw new NotFoundException($"Menu item '{menuItemId}' was not found.");

        if (!menuItem.IsAvailable)
        {
            throw new ConflictException($"'{menuItem.Name}' is currently unavailable.");
        }

        var cart = await GetOpenCartAsync(cancellationToken);

        if (cart is null)
        {
            cart = new Cart();
            db.Carts.Add(cart);
        }
        else if (cart.RestaurantId is not null && cart.RestaurantId != menuItem.RestaurantId)
        {
            throw new ConflictException(
                "The cart already contains items from another restaurant. Remove them first.");
        }

        var existingItem = cart.Items.FirstOrDefault(cartItem => cartItem.MenuItemId == menuItem.Id);
        var quantityAfterAdd = (existingItem?.Quantity ?? 0) + quantity;

        if (quantityAfterAdd > CartLimits.MaxQuantityPerItem)
        {
            throw new RequestValidationException(
                "quantity",
                existingItem is null
                    ? $"quantity cannot exceed {CartLimits.MaxQuantityPerItem}."
                    : $"The cart already holds {existingItem.Quantity} of '{menuItem.Name}'; " +
                      $"quantity cannot exceed {CartLimits.MaxQuantityPerItem}.");
        }

        cart.RestaurantId = menuItem.RestaurantId;
        cart.Restaurant ??= menuItem.Restaurant;
        cart.UpdatedAt = DateTimeOffset.UtcNow;

        if (existingItem is null)
        {
            // Added through the DbSet, not only through cart.Items: an entity discovered solely through a
            // navigation already has a key, so EF would track it as Modified and issue an UPDATE against a
            // row that does not exist.
            db.CartItems.Add(new CartItem
            {
                CartId = cart.Id,
                MenuItemId = menuItem.Id,
                MenuItem = menuItem,
                Quantity = quantity,
                UnitPrice = menuItem.Price,
            });
        }
        else
        {
            existingItem.Quantity = quantityAfterAdd;
        }

        await db.SaveChangesAsync(cancellationToken);


        logger.LogInformation(
            "Added {Quantity} of menu item {MenuItemId} to cart {CartId} at restaurant {RestaurantId}.",
            quantity,
            menuItem.Id,
            cart.Id,
            menuItem.RestaurantId);

        return cart;
    }

    /// <summary>Removes a line. Returns the remaining cart, or <c>null</c> when removing it emptied the cart.</summary>
    public async Task<Cart?> RemoveItemAsync(Guid cartItemId, CancellationToken cancellationToken)
    {
        var cart = await GetOpenCartAsync(cancellationToken)
            ?? throw new NotFoundException($"Cart item '{cartItemId}' was not found.");

        var cartItem = cart.Items.SingleOrDefault(item => item.Id == cartItemId)
            ?? throw new NotFoundException($"Cart item '{cartItemId}' was not found.");

        cart.Items.Remove(cartItem);
        db.CartItems.Remove(cartItem);

        if (cart.Items.Count == 0)
        {
            db.Carts.Remove(cart);
            await db.SaveChangesAsync(cancellationToken);

            // The cart is gone rather than left empty, so the cart id in the log is the only
            // remaining record that it existed.
            logger.LogInformation(
                "Removed the last item from cart {CartId}; the empty cart was discarded.",
                cart.Id);

            return null;
        }

        cart.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Removed cart item {CartItemId} from cart {CartId}; {RemainingItemCount} line(s) remain.",
            cartItem.Id,
            cart.Id,
            cart.Items.Count);

        return cart;
    }
}
