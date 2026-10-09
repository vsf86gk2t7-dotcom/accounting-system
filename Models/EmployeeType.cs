namespace AccountingSystem.Models
{
// نوع الموظف: يحدد الشاشات والصلاحيات لاحقًا (المندوب له بروفايل خاص)
public enum EmployeeType
{
Office = 1,     // إداري / محاسب
SalesRep = 2,   // مندوب مبيعات متنقل
Warehouse = 3,  // أمين مخزن
Driver = 4,     // سائق / توصيل
Other = 9
}
}