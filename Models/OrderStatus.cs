namespace Cronus.Ordering.Models;

/// <summary>Lifecycle of an order as the ordering service sees it.</summary>
public enum OrderStatus
{
    /// <summary>Accepted and persisted, but fulfilment has not been arranged yet.</summary>
    Placed = 0,

    /// <summary>A delivery has been arranged with the delivery service.</summary>
    Confirmed = 1,
}
