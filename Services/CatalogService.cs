using Cronus.Ordering.Data;
using Cronus.Ordering.Errors;
using Cronus.Ordering.Models;
using Microsoft.EntityFrameworkCore;

namespace Cronus.Ordering.Services;

/// <summary>Reads the restaurant catalogue.</summary>
public sealed class CatalogService(OrderingDbContext db)
{
    public async Task<IReadOnlyList<Restaurant>> GetRestaurantsAsync(CancellationToken cancellationToken) =>
        await db.Restaurants
            .AsNoTracking()
            .Where(restaurant => restaurant.IsActive)
            .OrderBy(restaurant => restaurant.Name)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<MenuItem>> GetMenuAsync(Guid restaurantId, CancellationToken cancellationToken)
    {
        var restaurantExists = await db.Restaurants
            .AnyAsync(restaurant => restaurant.Id == restaurantId, cancellationToken);

        if (!restaurantExists)
        {
            throw new NotFoundException($"Restaurant '{restaurantId}' was not found.");
        }

        return await db.MenuItems
            .AsNoTracking()
            .Where(menuItem => menuItem.RestaurantId == restaurantId)
            .OrderBy(menuItem => menuItem.Name)
            .ToListAsync(cancellationToken);
    }
}
