using Cronus.Ordering.Models;
using Microsoft.EntityFrameworkCore;

namespace Cronus.Ordering.Data;

/// <summary>
/// Seeds a small catalogue so a fresh database is immediately usable. Every entity uses a fixed
/// identifier and the method is a no-op once restaurants exist, so restarting is safe.
/// </summary>
public static class DevelopmentDataSeeder
{
    /// <summary>
    /// Inserts the sample catalogue unless the database already holds restaurants.
    /// </summary>
    /// <returns>
    /// <c>true</c> when the catalogue was inserted, <c>false</c> when it was already there, so a
    /// caller can say which of the two happened rather than reporting both as success.
    /// </returns>
    public static async Task<bool> SeedAsync(OrderingDbContext db, CancellationToken cancellationToken = default)
    {
        if (await db.Restaurants.AnyAsync(cancellationToken))
        {
            return false;
        }

        db.Restaurants.AddRange(
            new Restaurant
            {
                Id = Guid.Parse("a1000000-0000-0000-0000-000000000001"),
                Name = "Nonna's Pizzeria",
                Description = "Wood-fired Neapolitan pizza and pasta.",
                AddressLine = "12 Olive Lane",
                City = "London",
                MenuItems =
                [
                    NewMenuItem("b1000000-0000-0000-0000-000000000001", "Margherita", "Tomato, mozzarella, basil.", 9.50m),
                    NewMenuItem("b1000000-0000-0000-0000-000000000002", "Diavola", "Spicy salami, mozzarella, chilli.", 12.00m),
                    NewMenuItem("b1000000-0000-0000-0000-000000000003", "Tagliatelle Ragu", "Slow-cooked beef ragu.", 13.75m),
                    NewMenuItem("b1000000-0000-0000-0000-000000000004", "Tiramisu", "Classic mascarpone dessert.", 5.25m),
                ],
            },
            new Restaurant
            {
                Id = Guid.Parse("a1000000-0000-0000-0000-000000000002"),
                Name = "Sakura Sushi Bar",
                Description = "Fresh nigiri, maki and sashimi.",
                AddressLine = "48 Riverside Walk",
                City = "London",
                MenuItems =
                [
                    NewMenuItem("b1000000-0000-0000-0000-000000000011", "Salmon Nigiri (2pc)", "Norwegian salmon.", 4.80m),
                    NewMenuItem("b1000000-0000-0000-0000-000000000012", "Spicy Tuna Roll", "Tuna, chilli mayo, cucumber.", 7.90m),
                    NewMenuItem("b1000000-0000-0000-0000-000000000013", "Chicken Katsu Curry", "Panko chicken, curry sauce.", 11.50m),
                    NewMenuItem("b1000000-0000-0000-0000-000000000014", "Miso Soup", "Dashi, tofu, spring onion.", 3.20m),
                ],
            },
            new Restaurant
            {
                Id = Guid.Parse("a1000000-0000-0000-0000-000000000003"),
                Name = "The Green Bowl",
                Description = "Seasonal salads and grain bowls.",
                AddressLine = "5 Market Square",
                City = "London",
                MenuItems =
                [
                    NewMenuItem("b1000000-0000-0000-0000-000000000021", "Falafel Bowl", "Falafel, hummus, quinoa.", 10.25m),
                    NewMenuItem("b1000000-0000-0000-0000-000000000022", "Halloumi Salad", "Grilled halloumi, rocket, pomegranate.", 9.95m),
                    NewMenuItem("b1000000-0000-0000-0000-000000000023", "Sweet Potato Soup", "With ginger and coconut.", 6.50m),
                ],
            });

        await db.SaveChangesAsync(cancellationToken);

        return true;
    }

    private static MenuItem NewMenuItem(string id, string name, string description, decimal price) => new()
    {
        Id = Guid.Parse(id),
        Name = name,
        Description = description,
        Price = price,
    };
}
