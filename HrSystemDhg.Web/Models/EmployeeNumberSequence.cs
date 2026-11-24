namespace HrSystemDhg.Web.Models;

public class EmployeeNumberSequence
{
    public int Id { get; set; }
    public int NextPin { get; set; } = 1;
    public int NextNumber { get; set; } = 1;
}
