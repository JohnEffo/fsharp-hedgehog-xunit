namespace fsharp_examples
module Tests =

open Xunit
open Hedgehog
open Hedgehog.Xunit

//Properties containing multiple parameter of the same type with different
//generator requirements.

///Initial property
let positiveInt() = Range.constant 0 System.Int32.MaxValue |> Gen.int32 
let negativeInt() = Range.constant System.Int32.MinValue 0 |> Gen.int32
//Using property attribute we need to create container types so that
//the parameters of the property can be of different types.
type PositiveInt  = {value : int}
type NegativeInt = {value : int}

type AutoGenConfigContainer =
  static member __ =
    GenX.defaults
      |> AutoGenConfig.addGenerator (positiveInt() |> Gen.map(fun x-> {PositiveInt.value=x}))
      |> AutoGenConfig.addGenerator (negativeInt() |> Gen.map(fun x -> {NegativeInt.value=x}))


[<Property(typeof<AutoGenConfigContainer>)>]
let ``Positive + Negative <= Positive`` (positive:PositiveInt) (negative:NegativeInt) =
  positive.value + negative.value <= positive.value

//Using attributes to configure what generator the property should use
type Posint() =
  inherit ParameterGeneratorBaseType<int>()
  override this.Generator = positiveInt()

type NegInt() =
  inherit ParameterGeneratorBaseType<int>()
    override this.Generator = negativeInt()

[<Property>]
let ``Positive + Negative <= Positive attribute`` ([<Posint>] positive) ([<NegInt>] negative) =
  positive + negative <= positive





