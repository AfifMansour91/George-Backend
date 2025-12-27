using George.Common;
using System.ComponentModel.DataAnnotations;

namespace George.Services.Request
{
    public abstract class BusinessTypeReq
    {
        public string? Name { get; set; }
        public string? Description { get; set; }

        public string? Icon { get; set; }
    }

    public class CreateBusinessTypeReq : BusinessTypeReq
    {

    }

    public class UpdateBusinessTypeReq : BusinessTypeReq
    {
        [Required]
        [ValidId]
        public int Id { get; set; }
    }
}
