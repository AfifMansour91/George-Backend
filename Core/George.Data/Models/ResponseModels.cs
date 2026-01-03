using George.DB;

namespace George.Data.Models
{
    public class AccountListEntityRow
    {
        public Account Account { get; set; } = default!;
        //public WizardSession? LatestWizardSession { get; set; }
        public User? ManagerUser { get; set; }
    }
}
