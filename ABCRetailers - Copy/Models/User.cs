using Microsoft.AspNetCore.Identity;

namespace ABCRetailers.Models
{
    // This class INHERITS all Identity fields:
    // Id, UserName, NormalizedUserName, Email, PasswordHash,
    // SecurityStamp, LockoutEnd, PhoneNumber, etc.
    public class User : IdentityUser
    {
        

       // public string Role { get; set; } 
    }
}
