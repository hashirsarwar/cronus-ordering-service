namespace Cronus.Ordering.Contracts;

/// <summary>Limits the cart API enforces. Kept with the contracts because they are part of the API's public rules.</summary>
public static class CartLimits
{
    public const int MaxQuantityPerItem = 20;
}
