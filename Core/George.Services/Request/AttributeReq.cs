using George.Common;
using System.ComponentModel.DataAnnotations;

namespace George.Services.Request
{
    public class AttributeReq
    {
        [Required]
        public string Name { get; set; } = null!;
        
        [Required]
        public int SiteId { get; set; }
        
        public List<string>? Values { get; set; }
    }

    public class CreateAttributeReq : AttributeReq
    {
    }

    public class UpdateAttributeReq : AttributeReq
    {
        [Required]
        [ValidId]
        public int Id { get; set; }
    }
}
