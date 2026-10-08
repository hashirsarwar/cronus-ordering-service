namespace Cronus.Ordering.Errors;

/// <summary>The request conflicts with the current state of a resource. Reported as HTTP 409.</summary>
public sealed class ConflictException(string message) : Exception(message);
