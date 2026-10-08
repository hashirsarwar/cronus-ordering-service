using Cronus.Ordering.Models;
using Microsoft.EntityFrameworkCore;

namespace Cronus.Ordering.Data;

public class OrderingDbContext(DbContextOptions<OrderingDbContext> options) : DbContext(options)
{
    public DbSet<Restaurant> Restaurants => Set<Restaurant>();

    public DbSet<MenuItem> MenuItems => Set<MenuItem>();

    public DbSet<Cart> Carts => Set<Cart>();

    public DbSet<CartItem> CartItems => Set<CartItem>();

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Restaurant>(restaurant =>
        {
            restaurant.HasKey(r => r.Id);
            restaurant.HasMany(r => r.MenuItems)
                .WithOne(m => m.Restaurant)
                .HasForeignKey(m => m.RestaurantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MenuItem>(menuItem =>
        {
            menuItem.HasKey(m => m.Id);
            menuItem.Property(m => m.Price).HasPrecision(18, 2);
            menuItem.HasIndex(m => m.RestaurantId);
        });

        modelBuilder.Entity<Cart>(cart =>
        {
            cart.HasKey(c => c.Id);
            cart.Ignore(c => c.Total);
            cart.Property(c => c.Status).HasConversion<string>().HasMaxLength(32);
            cart.HasOne(c => c.Restaurant)
                .WithMany()
                .HasForeignKey(c => c.RestaurantId)
                .OnDelete(DeleteBehavior.Restrict);
            cart.HasMany(c => c.Items)
                .WithOne(i => i.Cart)
                .HasForeignKey(i => i.CartId)
                .OnDelete(DeleteBehavior.Cascade);
            cart.HasIndex(c => new { c.Status, c.CreatedAt });
        });

        modelBuilder.Entity<CartItem>(cartItem =>
        {
            cartItem.HasKey(i => i.Id);
            cartItem.Ignore(i => i.LineTotal);
            cartItem.Property(i => i.UnitPrice).HasPrecision(18, 2);
            cartItem.HasOne(i => i.MenuItem)
                .WithMany()
                .HasForeignKey(i => i.MenuItemId)
                .OnDelete(DeleteBehavior.Restrict);
            cartItem.HasIndex(i => new { i.CartId, i.MenuItemId }).IsUnique();
        });

        modelBuilder.Entity<Order>(order =>
        {
            order.HasKey(o => o.Id);
            order.Property(o => o.Status).HasConversion<string>().HasMaxLength(32);
            order.Property(o => o.TotalAmount).HasPrecision(18, 2);
            order.HasMany(o => o.Items)
                .WithOne(i => i.Order)
                .HasForeignKey(i => i.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
            order.HasIndex(o => o.CreatedAt);
        });

        modelBuilder.Entity<OrderItem>(orderItem =>
        {
            orderItem.HasKey(i => i.Id);
            orderItem.Ignore(i => i.LineTotal);
            orderItem.Property(i => i.UnitPrice).HasPrecision(18, 2);
        });
    }
}
