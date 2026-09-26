namespace George.Services.Request
{
    /// <summary>New-order screen: remember the delivery fee chosen for a city as that city's default.</summary>
    public class UpsertCityShippingCostReq
    {
        public string City { get; set; } = "";
        public decimal Cost { get; set; }
    }
}
