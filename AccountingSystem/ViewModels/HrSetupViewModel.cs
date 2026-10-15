namespace AccountingSystem.ViewModels
{
public class HrSetupItem
{
public int Id { get; set; }
public string Name { get; set; } = string.Empty;
public bool IsActive { get; set; }
public int EmployeesCount { get; set; }
}

public class HrSetupViewModel
{
public List<HrSetupItem> Departments { get; set; } = new();
public List<HrSetupItem> Positions { get; set; } = new();
}
}