using Cronus.Ordering.Data;
using Cronus.Ordering.Models;

namespace Cronus.Ordering.Tests.Infrastructure;

/// <summary>
/// Builds the catalogue data each test needs, so tests describe their own preconditions instead of
/// depending on shared seed data.
/// </summary>
internal static class TestData
{
    public static MenuItem MenuItem(string name, decimal price = 9.50m, bool isAvailable = true) => new()
    {
        Name = name,
        Description = $"{name} description",
        Price = price,
        IsAvailable = isAvailable,
    };

    public static async Task<Restaurant> AddRestaurantAsync(
        OrderingDbContext db,
        string name = "Test Kitchen",
        bool isActive = true,
        params MenuItem[] menuItems)
    {
        var restaurant = new Restaurant
        {
            Name = name,
            Description = $"{name} description",
            AddressLine = "1 Test Street",
            City = "London",
            IsActive = isActive,
        };

        foreach (var menuItem in menuItems)
        {
            restaurant.MenuItems.Add(menuItem);
        }

        db.Restaurants.Add(restaurant);
        await db.SaveChangesAsync();

        return restaurant;
    }
}
