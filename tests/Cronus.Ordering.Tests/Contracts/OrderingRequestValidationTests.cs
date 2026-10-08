using Cronus.Ordering.Contracts;
using Cronus.Ordering.Errors;

namespace Cronus.Ordering.Tests.Contracts;

/// <summary>
/// These are contract-level rules, so they need no database.
/// </summary>
[TestClass]
public sealed class OrderingRequestValidationTests
{
    [TestMethod]
    public void AddCartItem_AcceptsTheMaximumQuantity() =>
        RequestValidator.EnsureValid(new AddCartItemRequest(Guid.NewGuid(), CartLimits.MaxQuantityPerItem));

    [TestMethod]
    public void AddCartItem_ReportsAnEmptyMenuItemId_UnderTheCamelCaseFieldName()
    {
        var exception = Assert.ThrowsExactly<RequestValidationException>(() =>
            RequestValidator.EnsureValid(new AddCartItemRequest(Guid.Empty, 1)));

        Assert.IsTrue(
            exception.Errors.ContainsKey("menuItemId"),
            $"Expected a 'menuItemId' error but found: {string.Join(", ", exception.Errors.Keys)}");
    }

    [TestMethod]
    public void AddCartItem_RejectsQuantitiesOutsideTheAllowedRange()
    {
        foreach (var quantity in new[] { 0, -1, CartLimits.MaxQuantityPerItem + 1 })
        {
            var exception = Assert.ThrowsExactly<RequestValidationException>(() =>
                RequestValidator.EnsureValid(new AddCartItemRequest(Guid.NewGuid(), quantity)));

            Assert.IsTrue(exception.Errors.ContainsKey("quantity"), $"quantity {quantity} should be rejected");
        }
    }

    [TestMethod]
    public void CreateOrder_RequiresACustomerNameAndAnAddress()
    {
        var exception = Assert.ThrowsExactly<RequestValidationException>(() =>
            RequestValidator.EnsureValid(new CreateOrderRequest(null, "   ", null, null)));

        CollectionAssert.AreEquivalent(
            new[] { "customerName", "addressLine" },
            exception.Errors.Keys.ToArray());
    }

    [TestMethod]
    public void CreateOrder_EnforcesMaximumLengths()
    {
        var exception = Assert.ThrowsExactly<RequestValidationException>(() =>
            RequestValidator.EnsureValid(new CreateOrderRequest(new string('a', 201), "1 Street", null, null)));

        Assert.IsTrue(exception.Errors.ContainsKey("customerName"));
    }

    [TestMethod]
    public void CreateOrder_AcceptsARequestWithoutTheOptionalFields() =>
        RequestValidator.EnsureValid(new CreateOrderRequest("Ada Lovelace", "1 Analytical Way", null, null));
}
