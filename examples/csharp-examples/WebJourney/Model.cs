namespace csharp_examples.WebJourney;

public record Model(
    Dictionary<string,int> Items,
    bool HasAddress,
    bool HasPaymentDetails,
    bool OrderCreated){

    private static Dictionary<string, int> AddItemToDictionary(
        Dictionary<string, int> currentModelItems,
        Item item)
    {
        var copy = currentModelItems.ToDictionary(k => k.Key, i => i.Value);
        if (!copy.TryAdd(item.SKU, item.Quantity))
        {
            copy[item.SKU] += item.Quantity;
        }
        return copy;
    }

    private static Dictionary<string, int> MakeReduction(
        Dictionary<string, int> currentModelItems,
        string sku,
        int reductionAmount)
    {
        var remaining = currentModelItems[sku] - reductionAmount;
        var copy = currentModelItems.ToDictionary(k => k.Key, v => v.Value);
        copy.Remove(sku);
        if (remaining > 0)
        {
            copy.Add(sku, remaining);
        }

        return copy;
    }

    public Model ReduceItemCount(
        string sku, int quantity ) => this with { Items = MakeReduction(Items, sku, quantity) };

    public Model AddItem(
        Item item)
    {
        ItemEverAdded = true;
      return   this with { Items = AddItemToDictionary(Items, item) };
    }

    public bool ItemEverAdded { get; private set; }
}
