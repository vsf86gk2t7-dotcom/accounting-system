# Accounting System — نظام محاسبة متكامل

[![CI](https://github.com/vsf86gk2t7-dotcom/accounting-system/actions/workflows/ci.yml/badge.svg)](https://github.com/vsf86gk2t7-dotcom/accounting-system/actions/workflows/ci.yml)
![.NET](https://img.shields.io/badge/.NET-10-512BD4)
![Tests](https://img.shields.io/badge/tests-919%20passing-brightgreen)
![C#](https://img.shields.io/badge/C%23-13-239120)

نظام محاسبة احترافي مبني على **ASP.NET Core 10** مع دعم كامل للغة العربية، ويشمل إدارة المبيعات والمشتريات والمخزون والموارد البشرية والخزينة والتقارير المالية.

---

## ✨ المميزات الرئيسية

### 🧾 المبيعات والمشتريات
- فواتير بيع وشراء مع دعم تعدد الوحدات
- شاشة نقاط البيع (POS)
- مرتجعات البيع والشراء
- فواتير آجلة ونقدية مع إدارة الخزينة

### 📦 المخزون
- متابعة الأرصدة لحظياً
- إدارة اللوطات (Lots) وطرق الصرف (FIFO/LIFO/WAVG)
- التحويل بين المخازن
- الجرد والتسويات

### 👥 الموارد البشرية
- إدارة الموظفين والأقسام والمسميات الوظيفية
- الحضور والانصراف
- الرواتب والسلف والخصومات
- طلبات الإجازة

### 💰 الحسابات والخزينة
- شجرة حسابات متعددة المستويات
- القيود اليومية والترحيل التلقائي
- إدارة الخزائن والبنوك
- ميزان المراجعة والميزانية العمومية

### 📊 التقارير
- تقارير المبيعات والمشتريات
- تقارير المخزون والربحية
- كشوف حساب العملاء والموردين
- تقارير الضرائب والتدفقات النقدية

### 🔐 الأمان والصلاحيات
- مصادقة JWT مع Refresh Token
- نظام صلاحيات ديناميكي (Roles + Permissions)
- قفل الحساب بعد محاولات فاشلة
- سجل تدقيق (Audit Log) كامل

### 🚪 بوابات خارجية
- بوابة العملاء (Customer Portal)
- بوابة الموردين (Supplier Portal)
- إشعارات فورية عبر الواتساب

---

## 🛠️ التقنيات المستخدمة

| التقنية | الإصدار |
|---|---|
| .NET | 10 |
| ASP.NET Core MVC | 10 |
| Entity Framework Core | 10 |
| SQL Server | 2019+ |
| Bootstrap | 5.3 |
| Bootstrap Icons | 1.11 |
| xUnit + Moq | Latest |
| FluentAssertions | 6.x |

---

## 🚀 التشغيل السريع

### المتطلبات
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- SQL Server (أو SQL Express / LocalDB)
- Visual Studio 2022 أو VS Code

### الخطوات

```bash
# 1. استنساخ المشروع
git clone https://github.com/vsf86gk2t7-dotcom/accounting-system.git
cd accounting-system

# 2. استعادة الحزم
dotnet restore

# 3. تحديث قاعدة البيانات
dotnet ef database update

# 4. تشغيل التطبيق
dotnet run
```

التطبيق هيفتح على: `https://localhost:5001`

---

## 🧪 الاختبارات

المشروع عنده **919 اختبار** (Unit + Integration) مع نسبة نجاح **100%**.

### تشغيل كل الاختبارات

```bash
cd Tests/AccountingSystem.Tests
dotnet test
```

### تشغيل اختبار معين

```bash
dotnet test --filter "FullyQualifiedName~SalesInvoiceFlowIntegrationTests"
```

### قياس التغطية

```bash
dotnet test --collect:"XPlat Code Coverage"
```

### ملخص التغطية

| النوع | القيمة |
|---|---|
| **إجمالي الاختبارات** | 919 |
| **Unit Tests** | ~490 |
| **Integration Tests** | ~429 |
| **Coverage - Lines** | 36.27% |
| **Coverage - Branches** | 56.39% |

### المتحكمات المغطاة

- ✅ Account, Admin, Activation
- ✅ Accounting, Attendance, Branch
- ✅ Category, Company, Customer, CustomerPortal
- ✅ Employee, EmployeeAdvance, EmployeeDeduction
- ✅ HrSetup, Inventory, LeaveRequest, Notifications
- ✅ Payroll, Product, Profile, PurchaseInvoice
- ✅ PurchaseReturn, Reports, Role, SalesInvoice
- ✅ SalesRep, SalesReturn, Shipping, Store
- ✅ Supplier, SupplierPortal, Treasury, Unit, User

---

## 🔄 CI/CD

المشروع بيستخدم **GitHub Actions** لتشغيل الاختبارات تلقائياً على كل push.

- **Workflow:** `.github/workflows/ci.yml`
- **البيئة:** Ubuntu Latest
- **.NET:** 10.0.x
- **مدة التشغيل:** ~45 ثانية

---

## 📁 هيكل المشروع

```
AccountingSystem/
├── Controllers/           # متحكمات MVC
├── Models/               # الـ Models والـ ViewModels
├── Views/                # صفحات Razor
├── Services/             # الخدمات (Business Logic)
│   ├── Jwt/             # خدمات المصادقة
│   ├── Permissions/     # نظام الصلاحيات
│   ├── WhatsApp/        # تكامل الواتساب
│   └── ...
├── Filters/              # Action Filters (AdminOnly, RequirePermission)
├── Data/                 # DbContext والـ Seeder
├── Migrations/           # EF Core Migrations
├── Middleware/           # معالجة الأخطاء
├── wwwroot/              # الملفات الثابتة (CSS/JS)
└── Tests/
    └── AccountingSystem.Tests/
        ├── Controllers/  # Unit Tests
        ├── Services/     # Unit Tests
        └── Integration/  # Integration Tests
```

---

## 🔑 الصلاحيات

النظام بيستخدم نظام صلاحيات مرن:

| الدور | الصلاحيات |
|---|---|
| **Admin** | صلاحيات كاملة |
| **GeneralManager** | إدارة كل الأدوار الأدنى |
| **Accountant** | المحاسبة والقيود |
| **Storekeeper** | المخزون |
| **Cashier** | المبيعات والخزينة |
| **SalesRep** | المبيعات والعملاء |

كل دور عنده مجموعة صلاحيات مستقلة (مثل `sales.create`, `product.edit`, `report.sales`).

---

## 🤝 المساهمة

1. Fork المشروع
2. اعمل فرع جديد (`git checkout -b feature/amazing-feature`)
3. Commit التعديلات (`git commit -m 'feat: add amazing feature'`)
4. Push للفرع (`git push origin feature/amazing-feature`)
5. افتح Pull Request

---

## 📄 الترخيص

هذا المشروع مخصص للاستخدام الشخصي والتجاري.

---

## 📞 التواصل

- **GitHub:** [@vsf86gk2t7-dotcom](https://github.com/vsf86gk2t7-dotcom)
- **Repository:** [accounting-system](https://github.com/vsf86gk2t7-dotcom/accounting-system)

---
Add-Content "F:\New folder\asp_besm_allah\AccountingSystem\AccountingSystem\README.md" ""
⭐ لو المشروع عجبك، ما تنساش تعمله Star!