# fsharp-hedgehog-xunit

[![][nuget-shield]][nuget] [![][workflow-shield]][workflow] [![Coverage Status](https://coveralls.io/repos/github/dharmaturtle/fsharp-hedgehog-xunit/badge.svg?branch=main)](https://coveralls.io/github/dharmaturtle/fsharp-hedgehog-xunit?branch=main)

[Hedgehog][hedgehog] with convenience attributes for [xUnit.net][xunit].

<img src="https://github.com/hedgehogqa/fsharp-hedgehog/raw/master/img/hedgehog-logo.png" width="307" align="right"/>

## Features

- Test method arguments generated by the customizable [`GenX.auto`](https://github.com/hedgehogqa/fsharp-hedgehog-experimental/#auto-generation).
- `Property.check` called for each test.

## Getting Started

Install the _Hedgehog.Xunit_ [package][nuget] from Visual Studio's Package Manager Console:

```powershell
PM> Install-Package Hedgehog.Xunit
```

Suppose you have a test that uses [Hedgehog.Experimental](https://github.com/hedgehogqa/fsharp-hedgehog-experimental) and looks similar to the following:

```f#
open Xunit
open Hedgehog
[<Fact>]
let ``Reversing a list twice yields the original list`` () =
  property {
    let! xs = GenX.auto<int list>
    return List.rev (List.rev xs) = xs
  } |> Property.check
```

Then using Hedgehog.Xunit, you can simplify the above test to

```f#
open Hedgehog.Xunit
[<Property>]
let ``Reversing a list twice yields the original list, with Hedgehog.Xunit`` (xs: int list) =
  List.rev (List.rev xs) = xs
```

## Documentation

`Hedgehog.Xunit` provides the `Property`, `Properties`, and `Recheck` attributes.

### `Property` attribute

Methods with the `Property` attribute have their arguments generated by [`GenX.auto`](https://github.com/hedgehogqa/fsharp-hedgehog-experimental/#auto-generation).

```f#
type ``class with a test`` (output: Xunit.Abstractions.ITestOutputHelper) =
  [<Property>]
  let ``Can generate an int`` (i: int) =
    output.WriteLine $"Test input: {i}"
	
=== Output ===
Test input: 0
Test input: -1
Test input: 1
...
Test input: 522317518
Test input: 404306656
Test input: 1550509078
```

`Property.check` is also run.

```f#
[<Property>]
let ``This test fails`` (b: bool) =
  b

=== Output ===
Hedgehog.FailedException: *** Failed! Falsifiable (after 2 tests):
(false)
```

If the test returns `Async<_>` or `Task<_>`, then `Async.RunSynchronously` is called, _which blocks the thread._ This may have significant performance implications as tests run 100 times by default.

```f#
[<Property>]
let ``Async with exception shrinks`` (i: int) = async {
  do! Async.Sleep 100
  if i > 10 then
    failwith "whoops!"
  }

=== Output ===
Hedgehog.FailedException: *** Failed! Falsifiable (after 12 tests):
(11)
```
A test returning a `Result` in an `Error` state will be treated as a failure.

```f#
[<Property>]
let ``Result with Error shrinks`` (i: int) =
  if i > 10 then
    Error ()
  else
    Ok ()

=== Output ===
Hedgehog.FailedException: *** Failed! Falsifiable (after 13 tests and 2 shrinks):
[11]
```

Tests returning `Async<Result<_,_>>` or `Task<Result<_,_>>` are run synchronously and are expected to be in the `Ok` state.

The `Property` attribute's constructor may take several arguments: `AutoGenConfig`, `AutoGenConfigArgs`, `Tests` (count), `Shrinks` (count), and `Size`. Since the `Property` attribute extends `Xunit.FactAttribute`, it may also take `DisplayName`, `Skip`, and `Timeout`.

#### `AutoGenConfig` and `AutoGenConfigArgs`

* Default: `GenX.defaults`

Create a class with a single static property or method that returns an instance of `AutoGenConfig`. Then provide the type of this class as an argument to the `Property` attribute. This works around the constraint that [`Attribute` parameters must be a constant.](https://stackoverflow.com/a/33007272)

```f#
type AutoGenConfigContainer =
  static member __ =
    GenX.defaults |> AutoGenConfig.addGenerator (Gen.constant 13)

[<Property(typeof<AutoGenConfigContainer>)>]
let ``This test passes`` (i: int) =
  i = 13
```

If the method takes arguments, you must provide them using `AutoGenConfigArgs`.

```f#
type ConfigWithArgs =
  static member __ a b =
    GenX.defaults
    |> AutoGenConfig.addGenerator (Gen.constant a)
    |> AutoGenConfig.addGenerator (Gen.constant b)

[<Property(AutoGenConfig = typeof<ConfigWithArgs>, AutoGenConfigArgs = [|"foo"; 13|])>]
let ``This also passes`` s i =
  s = "foo" && i = 13
```

#### `Tests` (count)

Specifies the number of tests to be run, though more or less may occur due to shrinking or early failure.

```f#
[<Property(3<tests>)>]
let ``This runs 3 times`` () =
  ()
```

#### `Shrinks` (count)

Specifies the maximal number of shrinks that may run.

```f#
[<Property(Shrinks = 0<shrinks>)>]
let ``No shrinks occur`` i =
  if i > 50 then failwith "oops"
```

#### `Size`

Sets the `Size` to a value for all runs.

```f#
[<Property(Size = 2)>]
let ``"i" mostly ranges between -1 and 1`` i =
  printfn "%i" i
```

### `Properties` attribute

This optional attribute can decorate modules or classes. It sets default arguments for `AutoGenConfig`, `AutoGenConfigArgs`, `Tests`, `Shrinks`, and `Size`. These will be overridden by any arguments provided by the `Property` attribute.

```f#
type Int13   = static member __ = GenX.defaults |> AutoGenConfig.addGenerator (Gen.constant 13)
type Int2718 = static member __ = GenX.defaults |> AutoGenConfig.addGenerator (Gen.constant 2718)

[<Properties(typeof<Int13>, 1<tests>)>]
module ``Module with <Properties> tests`` =

  [<Property>]
  let ``this passes and runs once`` (i: int) =
    i = 13

  [<Property(typeof<Int2718>, 2<tests>)>]
  let ``this passes and runs twice`` (i: int) =
    i = 2718
```

### `Recheck` attribute

This optional method attribute invokes `Property.recheck` with the given `Size` and `Seed`. It must be used with `Property`.

```f#
[<Property(1<tests>)>]
[<Recheck(size = 57, value = 16596517232889608208UL, gamma = 14761040450692577973UL)>]
let ``this passes`` i =
  i = 123456
```

## Tips

Use named arguments to select the desired constructor overload.

```f#
[<Properties(Tests = 13<tests>, AutoGenConfig = typeof<AutoGenConfigContainer>)>]
module __ =
  [<Property(AutoGenConfig = typeof<AutoGenConfigContainer>, Tests = 2718<tests>, Skip = "just because")>]
  let ``Not sure why you'd do this, but okay`` () =
    ()
```

Consider extending `PropertyAttribute` or `PropertiesAttribute` to hardcode commonly used arguments.

```f#
type Int13 = static member __ = GenX.defaults |> AutoGenConfig.addGenerator (Gen.constant 13)

type PropertyInt13Attribute() = inherit PropertyAttribute(typeof<Int13>)
module __ =
  [<PropertyInt13>]
  let ``this passes`` (i: int) =
    i = 13

type PropertiesInt13Attribute() = inherit PropertiesAttribute(typeof<Int13>)
[<PropertiesInt13>]
module ___ =
  [<Property>]
  let ``this also passes`` (i: int) =
    i = 13
```

## Known issue with tuples

`GenX.autoWith` works with tuples.

```f#
[<Fact>]
let ``This passes`` () =
  Property.check <| property {
      let! a, b =
        GenX.defaults
        |> AutoGenConfig.addGenerator (Gen.constant (1, 2))
        |> GenX.autoWith<int*int>
      Assert.Equal(1, a)
      Assert.Equal(2, b)
  }
```

However, blindly converting it to `Hedgehog.Xunit` will fail.

```f#
type CustomTupleGen = static member __ = GenX.defaults |> AutoGenConfig.addGenerator (Gen.constant (1, 2))
[<Property(typeof<CustomTupleGen>)>]
let ``This fails`` ((a,b) : int*int) =
  Assert.Equal(1, a)
  Assert.Equal(2, b)
```

This is because F# functions whose only parameter is a tuple will generate IL that un-tuples that parameter, yielding a function whose arity is the number of elements in the tuple. More concretely, this F#

```f#
let ``This fails`` ((a,b) : int*int) = ()
```

yields this IL (in debug mode)

```IL
.method public static 
    void 'This fails' (
        valuetype [System.Private.CoreLib]System.Int32 _arg1_0,
        valuetype [System.Private.CoreLib]System.Int32 _arg1_1
    ) cil managed 
{
    .maxstack 8
    IL_0000: ret
}
```

Due to this behavior `Hedgehog.Xunit` can't know that the original parameter was a tuple. It will therefore not use the registered tuple generator. A workaround is to pass a second (possibly unused) parameter.

```f#
type CustomTupleGen = static member __ = GenX.defaults |> AutoGenConfig.addGenerator (Gen.constant (1, 2))
[<Property(typeof<CustomTupleGen>)>]
let ``This passes`` (((a,b) : int*int), _: bool) =
  Assert.Equal(1, a)
  Assert.Equal(2, b)
```

The updated F#

```f#
let ``This passes`` (((a,b) : int*int), _: bool) = ()
```

yields this IL

```IL
.method public static 
    void 'This passes' (
        class [System.Private.CoreLib]System.Tuple`2<valuetype [System.Private.CoreLib]System.Int32, valuetype [System.Private.CoreLib]System.Int32> _arg1,
        valuetype [System.Private.CoreLib]System.Boolean _arg2
    ) cil managed 
{
    .maxstack 8
    IL_0000: ret
}
```

[Source of IL.](https://sharplab.io/#v2:DYLgZgzgNALiCWwoBMQGoA+BbA9sgrsAKYAEAsgJ5l6FECwAUI8TCQHY4BOWAhsAGL42AYxjwcbEjxIAjEgF4pJNLMbMirAAaaAKgAt4EEmB6II2kgApLPKDICUJECXhsYAKlcxHiy/bUMLCTa+oYkAA48EBBE5ppW1rYOTi5unm72UCQA+s4yODjAPlb2QA)

[hedgehog]: https://github.com/hedgehogqa/fsharp-hedgehog
[xunit]: https://xunit.net/

[nuget]: https://www.nuget.org/packages/Hedgehog.Xunit/
[nuget-shield]: https://img.shields.io/nuget/v/Hedgehog.Xunit.svg
[workflow]: https://github.com/dharmaturtle/fsharp-hedgehog-xunit/actions?query=workflow%3AMain
[workflow-shield]: https://github.com/dharmaturtle/fsharp-hedgehog-xunit/workflows/Main/badge.svg
