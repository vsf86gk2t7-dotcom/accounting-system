namespace AccountingSystem.Models.ViewModels.Shared
{
// =========================================
// ViewModels موحدة — تُستخدم من عدة شاشات
// (Purchases, Sales, Users, Reports)
// =========================================

public class BranchOptionViewModel
{
public int Id { get; set; }
public string Name { get; set; } = string.Empty;
public bool IsSelected { get; set; }
}

public class StoreOptionViewModel
{
public int Id { get; set; }
public int BranchId { get; set; }
public string Name { get; set; } = string.Empty;
public string? BranchName { get; set; }
public string? Code { get; set; }
public bool IsSelected { get; set; }
}
}
