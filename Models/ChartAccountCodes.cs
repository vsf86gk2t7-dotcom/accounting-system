namespace AccountingSystem.Models
{
public static class ChartAccountCodes
{
// ===== أصول =====
public const string Cash = "1101";
public const string Customers = "1103";
public const string Inventory = "1104";
public const string VatPurchase = "1105";
public const string SalesTaxPurchase = "1106";
public const string EmployeeAdvances = "1107";

// ===== خصوم =====
public const string VatSales = "2101";
public const string Suppliers = "2102";
public const string SalesTaxPayable = "2103";
public const string PayrollPayable = "2105";

	// ===== إيرادات =====
	public const string Revenue = "4001";
	public const string SalesDiscounts = "4901";

// ===== مصروفات =====
public const string CostOfGoodsSold = "5001";
public const string PayrollExpense = "5101";
}
}