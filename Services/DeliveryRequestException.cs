namespace Cronus.Ordering.Services;

/// <summary>Why the delivery service could not be reached, or would not accept the request.</summary>
public enum DeliveryFailure
{
    Unreachable,
    Timeout,
    Rejected,
    InvalidResponse,
}

/// <summary>Raised when arranging a delivery fails. Carries no HTTP detail beyond an opaque reference.</summary>
public sealed class DeliveryRequestException(
    DeliveryFailure failure,
    string message,
    Exception? innerException = null) : Exception(message, innerException)
{
    public DeliveryFailure Failure { get; } = failure;

    /// <summary>A coarse reason safe to persist and show to customers.</summary>
    /// <remarks>Log technical details separately; never include internal addresses in the customer response.</remarks>
    public string UserSafeReason => Failure switch
    {
        DeliveryFailure.Unreachable => "The delivery service is currently unavailable.",
        DeliveryFailure.Timeout => "The delivery service did not respond in time.",
        DeliveryFailure.Rejected => "The delivery service rejected the request.",
        _ => "The delivery service returned an unexpected response.",
    };
}

/// <summary>Outbound contract for the delivery service's <c>POST /deliveries</c> endpoint.</summary>
public sealed record DeliveryRequest(
    Guid OrderId,
    string CustomerName,
    string AddressLine,
    string? City,
    string? PostalCode);

/// <summary>
/// The subset of the delivery service's response that the ordering service depends on. Extra JSON
/// members in the response are ignored, so the delivery service can add fields without breaking this.
/// </summary>
public sealed record DeliveryReference(Guid Id, string? Status);
