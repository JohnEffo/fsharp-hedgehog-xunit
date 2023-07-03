using Hedgehog;
using Hedgehog.Linq;
using Gen = Hedgehog.Linq.Gen;
using Range = Hedgehog.Linq.Range;

namespace csharp_examples.WebJourney;

public static class Standard
{
    public static  Gen<Item> GenItem =>
        from sku in Gen.AlphaNumeric.String(Range.FromValue(8))
        from price in Gen.Decimal(Range.Constant(0.5m, 230m))
        from amount in Gen.Int32(Range.Constant(1, 6))
        select new Item(sku, price, amount);


    
}
