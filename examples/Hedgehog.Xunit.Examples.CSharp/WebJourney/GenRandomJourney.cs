using System.Collections.ObjectModel;
using FluentAssertions;
using Hedgehog.Linq;


namespace Hedgehog.Xunit.Examples.CSharp.WebJourney;

public class GenRandomJourney : GenAttribute<List<JourneyAction>>
{
    private static Gen<JourneyAction> AddItemGen =>
        from item in Standard.GenItem
        let action = new JourneyAction.AddItem(item)
        select (JourneyAction)action;

    public static Gen<JourneyAction> ReduceItemGen =>
        from values in Linq.Gen.Int32(Linq.Range.Constant(0, 200)).Tuple2()
        let action = new JourneyAction.ReduceItem(values.Item1, values.Item2)
        select (JourneyAction)action;

    public static Gen<JourneyAction> AddAddressGen =>
        from address in Linq.Gen.Alpha.String(Linq.Range.Constant(20, 25))
        let action = new JourneyAction.AddAddress(address)
        select (JourneyAction)action;

    public static Gen<JourneyAction> AddPaymentDetailsGen =>
        from paymentDetails in Linq.Gen.Digit.String(Linq.Range.FromValue(16))
        let action = new JourneyAction.AddPaymentDetails(paymentDetails)
        select (JourneyAction)action;

    public static Gen<JourneyAction> CompleteOrderGen =>
        from action in Linq.Gen.FromValue(new JourneyAction.CreateOrder())
        select (JourneyAction)action;

    public override Gen<List<JourneyAction>> Generator
        => Linq.Gen.Choice(new Collection<Gen<JourneyAction>>
            {
                CompleteOrderGen,
                AddPaymentDetailsGen,
                AddAddressGen,
                ReduceItemGen,
                AddItemGen
            })
            //.List(Linq.Range.LinearInt32(0, 400)).Resize(150);
            .List(Linq.Range.Constant(100, 400));

}

public abstract record JourneyAction
{
    public record AddItem(
        Item Item) : JourneyAction;

    public record ReduceItem(
        int ItemNumber,
        int ItemAmount) : JourneyAction;

    public record AddAddress(
        string Address) : JourneyAction;

    public record AddPaymentDetails(
        string PaymentDetails) : JourneyAction;

    public record CreateOrder() : JourneyAction;

    public ActionResult PerformAction(
        BasketService service,
        ActionResult pre)
        => this switch
        {
            AddItem { Item: var item } => new ActionResult(service.AddItem(pre.Basket, item),pre.Model.AddItem(item)),
            ReduceItem reduction => PerformReduceItem(reduction, service, (Basket.WithItems)pre.Basket, pre.Model),
            AddAddress { Address: var address } =>new ActionResult (service.AddAddress((Basket.WithItems)pre.Basket, address), pre.Model with { HasAddress = true }),
            AddPaymentDetails { PaymentDetails: var payment } =>new ActionResult (service.AddPaymentDetails((Basket.WithAddress)pre.Basket, payment), pre.Model with { HasPaymentDetails = true }),
            CreateOrder =>new ActionResult (service.MakeOrder((Basket.WithPaymentDetails)pre.Basket), pre.Model with { OrderCreated = true }),
        };

    private static ActionResult PerformReduceItem(
        ReduceItem reduction,
        BasketService service,
        Basket.WithItems basket,
        Model model)
    {
        var itemIndex = reduction.ItemNumber % basket.Items.Count;
        var item = basket.Items[itemIndex];
        var reductionAmount = reduction.ItemAmount % item.Quantity + 1;
        var reductionItem = item with { Quantity = reductionAmount };
        return new ActionResult(service.ReduceItemCount(basket, reductionItem),
            model.ReduceItemCount(reductionItem.SKU, reductionItem.Quantity));
    }
}

public record ActionResult(
    Basket Basket,
    Model Model)
{
    public static ActionResult Empty() => new ActionResult(
        new Basket.Empty(),
        new Model(new Dictionary<string, int>(), false, false, false));

    public  void Validate()
    {
      
        if (Basket is Basket.Empty)
        {
            this.Model.Items.Should().BeEmpty("basket empty so model should be ");
        }

        if (Basket is Basket.WithItems { Items: var items })
        {
            items.ToDictionary(i => i.SKU, v => v.Quantity)
                .Should().BeEquivalentTo(Model.Items, "the model items list empty so basket items list should be");
        }

        (Basket is Basket.WithAddress).Should().Be(Model.HasAddress);
        (Basket is Basket.WithPaymentDetails).Should().Be(Model.HasPaymentDetails);
        (Basket is Basket.Order).Should().Be(Model.OrderCreated);
    }
}

public class Test
{

    [Recheck("1_15394381673532429804_4669077780501989921_100000000000000000011100000000000000001100000000000000000000000000000100000000000000010101000000000000001010100000000000000011000000000000000000000000000000000000000000000000000000000000000000000000000000000000000")]
    [Property(200)]
    public void TestJourney(
        [GenRandomJourney] List<JourneyAction> journey)
    {
        CheckJourneyDiscountingInvalidTransitions(journey);
    }

    private void CheckJourneyDiscountingInvalidTransitions(
        List<JourneyAction> journey)
    {
        var testState = ActionResult.Empty();
        var service = new BasketService();
        foreach (var journeyAction in journey)
        {
            if (IsValidActionForModel(journeyAction, testState.Model))
            {
                testState = journeyAction.PerformAction(service, testState);
                testState.Validate();
            }

            if (testState.Basket is Basket.Order)
            {
                break;
            }
        }
    }

    private bool IsValidActionForModel(
        JourneyAction journeyAction,
        Model model)
        => (journeyAction, model) switch
        {
            (JourneyAction.CreateOrder, { HasPaymentDetails: true, Items: var dic }) => dic.Any(),
            (JourneyAction.AddPaymentDetails, { HasAddress: var hasAddress }) => hasAddress,
            (JourneyAction.AddAddress, { ItemEverAdded: var itemEverAdded }) => itemEverAdded,
            (JourneyAction.ReduceItem, { Items: var dic }) => dic.Any(),
            (JourneyAction.AddItem, _) => true,
            _ => false
        };
}
