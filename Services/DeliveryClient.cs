using System.Net.Http.Json;
using System.Text.Json;

namespace Cronus.Ordering.Services;

/// <summary>Synchronous delivery client; keep transport details here for the planned move to messaging.</summary>
public sealed class DeliveryClient(HttpClient httpClient, ILogger<DeliveryClient> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<DeliveryReference> CreateDeliveryAsync(
        DeliveryRequest request,
        CancellationToken cancellationToken)
    {
        HttpResponseMessage response;

        try
        {
            response = await httpClient.PostAsJsonAsync("/deliveries", request, JsonOptions, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw new DeliveryRequestException(
                DeliveryFailure.Unreachable,
                $"The delivery service at {httpClient.BaseAddress} could not be reached.",
                exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            // The caller did not cancel, so the HttpClient timeout elapsed.
            throw new DeliveryRequestException(
                DeliveryFailure.Timeout,
                $"The delivery service at {httpClient.BaseAddress} did not respond in time.",
                exception);
        }

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "The delivery service rejected order {OrderId} with status {StatusCode}: {Body}",
                request.OrderId,
                (int)response.StatusCode,
                await response.Content.ReadAsStringAsync(cancellationToken));

            throw new DeliveryRequestException(
                DeliveryFailure.Rejected,
                $"The delivery service returned status {(int)response.StatusCode}.");
        }

        DeliveryReference? delivery;

        try
        {
            delivery = await response.Content.ReadFromJsonAsync<DeliveryReference>(JsonOptions, cancellationToken);
        }
        catch (JsonException exception)
        {
            // A body that is empty or malformed must be reported as a delivery failure, not escape as
            // an unhandled exception: the order is already persisted at this point.
            throw new DeliveryRequestException(
                DeliveryFailure.InvalidResponse,
                "The delivery service returned a body that could not be read.",
                exception);
        }

        return delivery ?? throw new DeliveryRequestException(
            DeliveryFailure.InvalidResponse,
            "The delivery service returned an empty body.");
    }
}
