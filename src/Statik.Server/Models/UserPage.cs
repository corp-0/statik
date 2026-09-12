// ReSharper disable EntityFramework.ModelValidation.UnlimitedStringLength
namespace Statik.Server.Models;

public class UserPage
{
    public required string UserId { get; set; }
    public required User Owner { get; set; }
    public required string BodyTemplate { get; set; } = "<main></main>";
    public required string Css { get; set; } = "";
}
