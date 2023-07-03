using System.Collections.ObjectModel;
using Hedgehog;
using Hedgehog.Linq;
using Hedgehog.Xunit;
using static csharp_examples.WebJourney.GenRandomJourney;
using Gen = Hedgehog.Linq.Gen;
using Property = Hedgehog.Linq.Property;
using Range = Hedgehog.Linq.Range;

namespace csharp_examples.WebJourney;

public class GenRandomJourney : GenAttribute<List<JourneyAction>>
{
    public bool UseManualShrink { get; set; }

    private static Gen<JourneyAction> AddItemGen =>
        from item in Standard.GenItem
        let action = new JourneyAction.AddItem(item)
        select (JourneyAction)action;

    public static Gen<JourneyAction> ReduceItemGen =>
        from values in Gen.Int32(Range.Constant(0, 200)).Tuple2()
        let action = new JourneyAction.ReduceItem(values.Item1, values.Item2)
        select (JourneyAction)action;

    public static Gen<JourneyAction> AddAddressGen =>
        from address in Gen.Alpha.String(Range.Constant(20, 25))
        let action = new JourneyAction.AddAddress(address)
        select (JourneyAction)action;

    public static Gen<JourneyAction> AddPaymentDetailsGen =>
        from paymentDetails in Gen.Digit.String(Range.FromValue(16))
        let action = new JourneyAction.AddPaymentDetails(paymentDetails)
        select (JourneyAction)action;

    public static Gen<JourneyAction> CompleteOrderGen =>
        from action in Gen.FromValue(new JourneyAction.CreateOrder())
        select (JourneyAction)action;


    private static readonly Gen<List<JourneyAction>> gen = Gen.Choice(new Collection<Gen<JourneyAction>>
    {
        CompleteOrderGen,
        AddPaymentDetailsGen,
        AddAddressGen,
        ReduceItemGen,
        AddItemGen
    }).List(Range.Constant(100, 400));

    public override Gen<List<JourneyAction>> Generator
        => UseManualShrink
            ? gen.ShrinkLazy(PerformShrink)
            : gen;

    private IEnumerable<List<JourneyAction>> PerformShrink(
        List<JourneyAction> arg)
    {
        for (int i = 0; i < arg.Count; i++)
        {
            var head = arg.GetRange(0, i);
            var tail = i < arg.Count ? arg.GetRange(i + 1, (arg.Count - head.Count) - 1) : new List<JourneyAction>();
            head.AddRange(tail);
            yield return head;
        }
    }

}

public abstract record JourneyAction
{
    public record AddItem(
        Item Item) : JourneyAction;

    public record ReduceItem(
        int ItemNumber,
        int ItemAmount):JourneyAction;

    public record AddAddress(
        string Address) : JourneyAction;

    public record AddPaymentDetails(
        string PaymentDetails) : JourneyAction;

    public record CreateOrder() : JourneyAction;

    public (Basket, Model) PerformAction(
        BasketService service,
        Basket basket,
        Model model)
        => this switch
        {
            AddItem { Item: var item }=> ( service.AddItem(basket, item), model.AddItem(item)),
            ReduceItem reduction => PerformReduceItem(reduction, service, (Basket.WithItems)basket, model),
            AddAddress {Address: var address}=>(service.AddAddress((Basket.WithItems)basket, address), model with { HasAddress = true }),
            AddPaymentDetails {PaymentDetails:var payment}=>(service.AddPaymentDetails((Basket.WithAddress)basket, payment), model with {HasPaymentDetails = true}),
            CreateOrder=>(service.MakeOrder((Basket.WithPaymentDetails)basket),model with {OrderCreated = true}),
        };

    private static (Basket, Model) PerformReduceItem(
        ReduceItem reduction,
        BasketService service,
        Basket.WithItems basket,
        Model model)
    {
        int itemIndex = reduction.ItemNumber % basket.Items.Count;
        var item = basket.Items[itemIndex];
        var reductionAmount = (reduction.ItemAmount % item.Quantity) + 1;
        var reductionItem = item with { Quantity = reductionAmount };
        return (service.ReduceItemCount(basket, reductionItem),
            model.ReduceItemCount(reductionItem.SKU, reductionItem.Quantity));
    }
}

public class Test
{
    
    [Property(200)]
    public void XUnitAutomaticShrink(
        [GenRandomJourney] List<JourneyAction>  action)
    {
        CheckList(action);
    }

    [Property(200)]
    public void XUnitManualShrink(
        [GenRandomJourney(UseManualShrink = true)] List<JourneyAction> action)
    {
        CheckList(action);
    }

    

    [Fact]
    public void Check_old_Style_Manual()
    {
        var prop = from data in Property.ForAll(new GenRandomJourney{UseManualShrink = true}.Generator)
            select CheckList(data);

        prop.Check();
    }

    [Fact]
    public void Check_old_Style_Auto()
    {
        var prop = from data in Property.ForAll(new GenRandomJourney().Generator)
            select CheckList(data);

        prop.Check();
    }

    private void CheckList(
        List<JourneyAction> action)
    {
        var model = new Model(new Dictionary<string, int>(), false, false, false);
        Basket basket = new Basket.Empty();
        var service = new BasketService();
        foreach (var journeyAction in action)
        {
            if (IsValidActionForModel(journeyAction, model))
            {
                (basket, model) = journeyAction.PerformAction(service, basket, model);
                (basket, model).Validate();
            }

            if (basket is Basket.Order)
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
            (JourneyAction.CreateOrder, { HasPaymentDetails: true, Items: var dic })   => dic.Any(),
            (JourneyAction.AddPaymentDetails, { HasAddress: var hasAddress }) => hasAddress,
            (JourneyAction.AddAddress, { ItemEverAdded: var itemEverAdded }) => itemEverAdded,
            (JourneyAction.ReduceItem, { Items: var dic }) => dic.Any(),
            (JourneyAction.AddItem, _) => true,
            _ => false
        };
}
