using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace Statik.Server.Models;

public class User: IdentityUser
{
    [MaxLength(25)]
    public required string DisplayName { get; set; }

    [MaxLength(200)]
    public string About { get; set; } = "";
}
