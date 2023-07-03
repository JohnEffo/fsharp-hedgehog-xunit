using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using FluentAssertions;
using Hedgehog;
using Hedgehog.Linq;
using Hedgehog.Xunit;
using Newtonsoft.Json;
using Gen= Hedgehog.Linq.Gen;
using Range = Hedgehog.Linq.Range;

namespace csharp_examples.WebJourney;

/// <summary>
/// The plan here is to only allow valid web journeys to be created.
/// We do this by maintaining a model within the generator and only performing actions allowed on the model.
/// The problem with this is that while it finds errors, it does not shrink correctly.
/// </summary>
public class GenValidJourney : GenAttribute<Journey>
{
    public override Gen<Journey> Generator =>
        from startModel in Gen.FromValue(new Model(new Dictionary<string, int>(), false, false, false))
        from remainingJourney in GenerateRemainingJourney(startModel, new BasketService())
        select new Journey(Initial(startModel), remainingJourney);

    private Gen<Journey?> GenerateRemainingJourney(
        Model currentModel,
        BasketService service)
    {
        if (currentModel.OrderCreated)
        {
            return Gen.FromValue<Journey?>(null);
        }

        var allowedSteps = currentModel switch
        {
            //Payment details and item's can perform all actions
            { HasPaymentDetails: true, Items: var dic } when dic.Any() => new Collection<Gen<Step>>
            {
                MakeOrder(currentModel, service),
                AddPaymentDetails(currentModel, service),
                AddAddress(currentModel, service),
                AddItem(currentModel, service),
                ReduceQuantity(currentModel, service)
            },
            //Payment details but no items: cannot MakeOrder or remove items
            { HasPaymentDetails: true } => new Collection<Gen<Step>>
            {
                AddAddress(currentModel, service),
                AddItem(currentModel, service),
                AddPaymentDetails(currentModel, service)
            },
            //Has address and items can add payment details, add address, add and remove items
            { HasAddress: true, Items: var dic } when dic.Any() => new Collection<Gen<Step>>
            {
                AddPaymentDetails(currentModel, service),
                AddAddress(currentModel, service),
                AddItem(currentModel, service),
                ReduceQuantity(currentModel, service)
            },
            //Has address but not items so can't progress to add payment details and can't remove items
            { HasAddress: true } => new Collection<Gen<Step>>
            {
                AddPaymentDetails(currentModel, service),
                AddAddress(currentModel, service),
                AddItem(currentModel, service),
            },
            //Has items so can add and remove items and add address.
            { Items: var dic } when dic.Any() => new Collection<Gen<Step>>
            {
                AddAddress(currentModel, service),
                AddItem(currentModel, service),
                ReduceQuantity(currentModel, service),
            },
            //Has not items so can only add items.
            _ => new Collection<Gen<Step>> { AddItem(currentModel, service), }
        };
        return from transition in Gen.Choice(allowedSteps)
            from remainingJourney in GenerateRemainingJourney(transition.Model, service)
            select new Journey(transition, remainingJourney);
    }

    private Gen<Step> AddPaymentDetails(
        Model currentModel,
        BasketService basketService) =>
        from paymentDetails in Gen.Digit.String(Range.FromValue(16))
        select new Step(currentModel with { HasPaymentDetails = true },
            basket => basketService.AddPaymentDetails((Basket.WithAddress)basket, paymentDetails),
            nameof(AddPaymentDetails));

    private Gen<Step> ReduceQuantity(
        Model currentModel,
        BasketService basketService) =>
        from sku in Gen.Item(currentModel.Items.Keys)
        let maxQuantity = currentModel.Items[sku]
        from reductionAmount in Gen.Int32(Range.Constant(1, maxQuantity))
        let newModel = currentModel.ReduceItemCount(sku,maxQuantity) 
        select new Step(newModel,
            basket => basketService.ReduceItemCount((Basket.WithItems)basket, new Item(sku, 0.0m, reductionAmount)));

    private Gen<Step> AddItem(
        Model currentModel,
        BasketService basketService) =>
        from item in Standard.GenItem
        let newModel = currentModel.AddItem(item)
        select new Step(newModel, basket => basketService.AddItem(basket, item));


    private Gen<Step> AddAddress(
        Model currentModel,
        BasketService basketService) =>
        from address in Gen.Alpha.String(Range.Constant(20, 25))
        let newModel = currentModel with { HasAddress = true }
        select new Step(newModel, basket => basketService.AddAddress((Basket.WithItems)basket, address));

    private Gen<Step> MakeOrder(
        Model currentModel,
        BasketService basketService) => Gen.FromValue(
        new Step(currentModel with { OrderCreated = true },
            basket => basketService.MakeOrder((Basket.WithPaymentDetails)basket))
    );

    private Step Initial(
        Model startModel)
    {
        return new Step(startModel, _ => new Basket.Empty());
    }
}

public class Journey
{
    public Journey(
        Step transition,
        Journey nextStep)
    {
        Transition = transition;
        NextStep = nextStep;
    }

    public Step Transition { get; }
    public Journey? NextStep { get; }

    public override string ToString()
    {
        return $"Step:{Transition.Name} Model:{JsonConvert.SerializeObject(Transition.Model)}\n{NextStep}";
    }
}
public class Step
{
    private readonly Func<Basket, Basket> _action;

    public Step(
        Model model, 
        Func<Basket, Basket> action,[CallerMemberName] string name =null)
    {
        Model = model;
        _action = action;
        Name = name;
    }

    public string Name { get; }

    public Model Model { get; }

    public Basket PerformStep(
        Basket basket)
    {
        return _action(basket);
    }

}

public class TestWebCheckout
{

    [Recheck("1_3525252562349695559_13582854979373176535_000000000000000000000000000000000000000000000000000000000000000100000000000000001010100000000000000001010100000000000000001010100000000000000010000000000000101010000000000000001010100000000000000010100000000000000101100000000000000000000000000000000")]
    [Property(200)]
    public void TestJourneySteps(
        [GenValidJourney] Journey journey)
    {
        Basket basket = new Basket.Empty();
        Journey? currentStep = journey;
        while (currentStep != null)
        {
            var transition = currentStep.Transition;
            basket = transition.PerformStep(basket);
            (basket, transition.Model).Validate();
            currentStep = currentStep.NextStep;
        }
    }
}
