using Microsoft.EntityFrameworkCore;
using George.DB;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace George.Common
{
	public class SearchFilter
	{
		public string? SearchTerm { get; set; }
	}

	public class RegistryUnitFilter// : SearchFilter
	{
		public bool? HasRegistrationMethod { get; set; }
		public bool? HasLandUse { get; set; }
		public bool? HasAddress { get; set; }
		public bool? HasArea { get; set; }
		public bool? IsFilled { get; set; }

		public List<OwnershipType>? OwnershipTypeIds { get; set; }
		public List<RegistrationMethod>? RegistrationMethodIds { get; set; }
		public List<int>? LandUseIds { get; set; }
		
		public int? Block { get; set; }
		public int? Parcel { get; set; }
		public decimal? MinArea { get; set; }
		public decimal? MaxArea { get; set; }
		public string? Address { get; set; }
	}

	public class UserFilter : SearchFilter
	{
		public string? Name { get; set; }
		public UserStatus? StatusId { get; set; }
	}


    /////////////////////// New Filters ////////////////////////

    public class BusinessTypeFilter
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Icon { get; set; }
        //public List<int>? CategoriesIds { get; set; }
        public SearchFilter? Search { get; set; }
    }

    public class AccountFilter
    {
        public string? Name { get; set; }
        public SearchFilter? Search { get; set; }
        public string? Status { get; set; }        // "Active" / "Inactive" (optional)
        public string? WizardStatus { get; set; }  // "InProgress" / "Completed" (optional)
    }

    public class SiteFilter
    {
        public string? Name { get; set; }
        public string? Location { get; set; }
        public string? Description { get; set; }
        public string? Status { get; set; }
        public string? ContactEmail { get; set; }
        public string? ContactPhone { get; set; }
        public bool? IsKosherSite { get; set; }
        public bool? AllowWeightedProducts { get; set; }
        public SearchFilter? Search { get; set; }
    }

    public class AttributeFilter
    {
        public string? Name { get; set; }
        public string? Value { get; set; }
        public List<int>? SiteIds { get; set; }
        public SearchFilter? Search { get; set; }
    }

    public class ProductFilter
    {
        public int? AccountId { get; set; }
        public int? SiteId { get; set; }
        public int? CategoryId { get; set; }
        public string? Status { get; set; }
        public SearchFilter? Search { get; set; }
    }

    public class CategoryFilter
    {
        public int? AccountId { get; set; }
        public int? SiteId { get; set; }
        public int? ParentCategoryId { get; set; }
        public bool? IsEnabled { get; set; }
        public SearchFilter? Search { get; set; }
    }

    public class MediaFilter
    {
        public int? AccountId { get; set; }
        /// <summary>When set with AccountId, return only media linked to this site. Required for account+site media library.</summary>
        public int? SiteId { get; set; }
        /// <summary>When true, return only global media (AccountId is null). Super-admin use.</summary>
        public bool? GlobalOnly { get; set; }
        public int? BusinessTypeId { get; set; }
        public int? CategoryId { get; set; }
        public string? Type { get; set; } // "image" | "video" | "document"
        public SearchFilter? Search { get; set; }
    }

    public class ClientFilter
    {
        public int? AccountId { get; set; }
        public int? SiteId { get; set; }
        public string? ClientRole { get; set; } // "super_admin" | "account_admin" | "site_admin"
        public string? Status { get; set; } // "active" | "inactive" | "suspended"
        public SearchFilter? Search { get; set; }
    }

    public class GlobalCategoryFilter
    {
        public int? ParentGlobalCategoryId { get; set; }
        public int? BusinessTypeId { get; set; }
        public SearchFilter? Search { get; set; }
    }

    public class TemplateAttributeFilter
    {
        public int? SiteId { get; set; }
        public List<int>? SiteIds { get; set; }
        public SearchFilter? Search { get; set; }
    }

    public class TemplateProductFilter
    {
        public string? TemplateId { get; set; }
        public int? SiteId { get; set; }
        public int? GlobalCategoryId { get; set; }
        public string? Status { get; set; }
        public SearchFilter? Search { get; set; }
        /// <summary>When true, list query omits Related and Complementary product includes for faster list/catalog loading.</summary>
        public bool LightList { get; set; }
    }

    /// <summary>Sprint 2: Filter for orders list by site.</summary>
    public class OrderFilter
    {
        public int? SiteId { get; set; }
        /// <summary>One or more statuses; bound from repeated query params (e.g. Filter.Status=Completed&amp;Filter.Status=Cancelled).</summary>
        public List<string>? Status { get; set; }       // New | InTreatment | Ready | Completed | Cancelled
        public string? Source { get; set; }       // Website | Kiosk | Phone
        public string? DeliveryType { get; set; } // Shipping | Pickup
        public string? PaymentStatus { get; set; } // Unpaid | Paid | Captured
        public DateTime? DeliveryDateFrom { get; set; }
        public DateTime? DeliveryDateTo { get; set; }
        public SearchFilter? Search { get; set; }
    }

}
