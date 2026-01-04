using George.Common;
using System.ComponentModel.DataAnnotations;

namespace George.Services.Request
{
    public class TemplateAttributeReq
    {
        [Required]
        public string Name { get; set; } = null!;

        public List<string>? Values { get; set; }

        public List<int>? SiteIds { get; set; }
    }

    public class CreateTemplateAttributeReq : TemplateAttributeReq
    {
    }

    public class UpdateTemplateAttributeReq : TemplateAttributeReq
    {
        [Required]
        [ValidId]
        public int Id { get; set; }
    }
}

