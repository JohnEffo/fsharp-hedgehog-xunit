namespace Hedgehog.Xunit.Examples.CSharp.WebJourney;

public abstract record Basket
{
    public record Empty() : Basket;

    public record WithItems(
        List<Item> Items) : Basket;

    public record WithAddress : WithItems
    {
        public WithAddress(
            WithItems basket,
            string address) : base(basket.Items)
        {
            Address = address;
        }
        public string Address { get; init; }
    }

    public record WithPaymentDetails : WithAddress
    {
        public WithPaymentDetails(
            WithAddress basket,
            string paymentDetail) : base(basket, basket.Address)
        {
            PaymentDetail = paymentDetail;
        }
        public string PaymentDetail { get; init; }
    }

    public record Order : WithPaymentDetails
    {
        public Order(
            WithPaymentDetails basket) : base(basket, basket.PaymentDetail)
        {
        }
    }

}

public class BasketService
{
    public Basket.WithItems AddItem(
        Basket basket,
        Item item)
        => basket switch
        {
            Basket.Empty => new Basket.WithItems(new List<Item> { item }),
            Basket.WithItems i => i with { Items = AddItemToList(i.Items, item) },
        };

    public Basket.WithItems ReduceItemCount(
        Basket.WithItems basket,
        Item item)
    {
        if (basket.Items.FirstOrDefault(i => i.SKU == item.SKU) is { } itemToUpdate)
        {
            basket.Items.Remove(itemToUpdate);
            // Bug A we leave items with no quantity in the list of items
            //basket.Items.Add(itemToUpdate with {Quantity = itemToUpdate.Quantity - item.Quantity}); //Original failing implementation
            // Ehd bug A

            //Start Bug A fix
            var newQuantity = itemToUpdate.Quantity - item.Quantity;
            if (newQuantity > 0)
            {
                basket.Items.Add(itemToUpdate with { Quantity = itemToUpdate.Quantity - item.Quantity });
            }
            //End Bug A fix
        }
        return basket;
    }

    public Basket.WithAddress AddAddress(
        Basket.WithItems basket,
        string address)
    {
        //Bug B we always create a new with address so coming back wipes payment details
        return new Basket.WithAddress(basket, address);
        //End bug B
        //Start bug b Fix
        //return basket switch
        //{
        //    Basket.WithAddress addressBasket => addressBasket with { Address = address },
        //    _ =>new  Basket.WithAddress(basket, address)
        //};
        //End bug b Fix
    }


    private List<Item> AddItemToList(
        List<Item> currentItems,
        Item item)
    {
        if (currentItems.FirstOrDefault(i => i.SKU == item.SKU) is { } currentItem)
        {
            currentItems.Remove(currentItem);
            currentItems.Add(item with { Quantity = item.Quantity + currentItem.Quantity });
        }
        else
        {
            currentItems.Add(item);
        }
        return currentItems;
    }

    public Basket MakeOrder(
        Basket.WithPaymentDetails basket)
    => new Basket.Order(basket);


    public Basket AddPaymentDetails(
        Basket.WithAddress basket,
        string paymentDetails) => new Basket.WithPaymentDetails(basket, paymentDetails);

}


public record Item(
    string SKU,
    decimal PricePerUnit,
    int Quantity);




