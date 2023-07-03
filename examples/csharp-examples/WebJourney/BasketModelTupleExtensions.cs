using FluentAssertions;

namespace csharp_examples.WebJourney;

public static class BasketModelTupleExtensions
{
    public static void Validate(this (Basket basket, Model model) input )
    {
        var (basket, model) = input;
        if (  basket is Basket.Empty)
        {
            model.Items.Should().BeEmpty("basket empty so model should be ");
        }

        if (basket is Basket.WithItems { Items: var items })
        {
            items.ToDictionary(i => i.SKU, v => v.Quantity)
                .Should().BeEquivalentTo(model.Items, "the model items list empty so basket items list should be");
        }

        model.HasAddress.Should().Be(basket is Basket.WithAddress);
        model.HasPaymentDetails.Should().Be(basket is Basket.WithPaymentDetails);
        model.OrderCreated.Should().Be(basket is Basket.Order);
    }
}
