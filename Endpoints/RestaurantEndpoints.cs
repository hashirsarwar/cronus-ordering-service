using Cronus.Ordering.Contracts;
using Cronus.Ordering.Services;

namespace Cronus.Ordering.Endpoints;

public static class RestaurantEndpoints
{
    public static IEndpointRouteBuilder MapRestaurantEndpoints(this IEndpointRouteBuilder app)
    {
        var restaurants = app.MapGroup("/restaurants").WithTags("Restaurants");

        restaurants.MapGet("/", async (CatalogService catalog, CancellationToken cancellationToken) =>
        {
            var results = await catalog.GetRestaurantsAsync(cancellationToken);
            return Results.Ok(results.Select(RestaurantResponse.FromEntity));
        })
        .WithName("GetRestaurants")
        .WithSummary("Lists the active restaurants.");

        restaurants.MapGet("/{restaurantId:guid}/menu", async (
            Guid restaurantId,
            CatalogService catalog,
            CancellationToken cancellationToken) =>
        {
            var menu = await catalog.GetMenuAsync(restaurantId, cancellationToken);
            return Results.Ok(menu.Select(MenuItemResponse.FromEntity));
        })
        .WithName("GetRestaurantMenu")
        .WithSummary("Lists the menu of one restaurant.");

        return app;
    }
}
