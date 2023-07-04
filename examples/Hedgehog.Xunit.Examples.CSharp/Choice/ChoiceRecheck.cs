namespace Hedgehog.Xunit.Examples.CSharp.Choice;

using System.Collections.ObjectModel;
using Gen = Hedgehog.Linq.Gen;
using Range = Hedgehog.Linq.Range;
using Linq;
public class ChoiceRecheck
{
    [Fact]
    public void Choice()
    {
        var low = Gen.Int32(Range.Constant(0, 5));
        var mid = Gen.Int32(Range.Constant(10, 50));
        var big = Gen.Int32(Range.Constant(100, 200));
        var large = Gen.Int32(Range.Constant(500, 1000));
        var chioce = Gen.Choice(new Collection<Gen<int>> { low, mid, big, large }).List(Range.Constant(100,200));

        var prop = Property.ForAll(chioce).Select(x => x.Any(Test ));
        //prop.Check();
        prop.Recheck("0_8474167946941752373_7006309460388453401_000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000");

    }

    public bool Test(
        int x) => x == 990;
}
