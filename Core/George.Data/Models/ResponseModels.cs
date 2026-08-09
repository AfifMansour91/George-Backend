using George.DB;

namespace George.Data.Models
{
    public class AccountListEntityRow
    {
        public Account Account { get; set; } = default!;
        //public WizardSession? LatestWizardSession { get; set; }
        public User? ManagerUser { get; set; }
    }

    /// <summary>Archive KPI summary over a whole filtered period (not paged) — see OrderStorage.GetOrderArchiveSummaryAsync.</summary>
    public class OrderArchiveSummaryDto
    {
        public int Total { get; set; }
        public int Completed { get; set; }
        public int Cancelled { get; set; }
        /// <summary>Orders a credit (full or partial) was issued for.</summary>
        public int Credited { get; set; }
        /// <summary>Total amount credited back across the period.</summary>
        public decimal CreditedSum { get; set; }
        /// <summary>Distinct delivery cities in the period (for the city filter options).</summary>
        public List<string> Cities { get; set; } = new();
        /// <summary>True when the period contains orders without a delivery city.</summary>
        public bool HasCityNone { get; set; }
    }
}
