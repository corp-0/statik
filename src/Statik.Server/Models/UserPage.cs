using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace Statik.Server.Models;

public class UserPage
{
    public required string UserId { get; set; }
    [ForeignKey(nameof(UserId))]
    public required User Owner { get; set; }
    [DataType(DataType.MultilineText), DisplayFormat(ConvertEmptyStringToNull = false)]
    public required string BodyTemplate { get; set; } = "<main></main>";
    [DataType(DataType.MultilineText), DisplayFormat(ConvertEmptyStringToNull = false)]
    public required string Css { get; set; } = "";
}
