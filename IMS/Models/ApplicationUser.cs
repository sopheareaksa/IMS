using Microsoft.AspNetCore.Identity;
using System;

namespace IMS.Models
{
    public class ApplicationUser : IdentityUser<int>
    {
        public string Status { get; set; } = "Active";
        public DateTime? LastLogin { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}

