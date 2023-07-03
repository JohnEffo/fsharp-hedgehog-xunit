using Hedgehog.Linq;

namespace Hedgehog.Xunit.Examples.CSharp.WebJourney;

public static class Standard
{
    public static Gen<Item> GenItem =>
        from sku in Linq.Gen.AlphaNumeric.String(Linq.Range.FromValue(8))
        from price in Linq.Gen.Decimal(Linq.Range.Constant(0.5m, 230m))
        from amount in Linq.Gen.Int32(Linq.Range.Constant(1, 6))
        select new Item(sku, price, amount);

}
