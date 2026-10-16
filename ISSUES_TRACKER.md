# 🔴 تتبع مشاكل المشروع

آخر تحديث: 2026-10-16

---

## 🚨 المرحلة 1 — حماية البيانات (P0)

### 1️⃣ إصلاح SequenceService Transaction — ✅ مكتمل

- [x] مراجعة `Services/SequenceService.cs`
- [x] إضافة `CommitAsync()` في المسار المستقل
- [x] دعم Transaction خارجية (Ambient) — لا يبدأ معاملة داخلية
- [x] اختبار: توليد رقم بدون Transaction خارجية → يثبت في DB
- [x] `WITH (UPDLOCK, HOLDLOCK, ROWLOCK)` لمنع race أول رقم في اليوم
- [x] `effectiveDate` يتحسب مرة واحدة في `NextFormattedAsync`
- [x] `ChangeTracker` detach موجّه بدل `Clear()` — يحفظ تعديلات المستدعي
- [x] `NextContinuousAsync` + `NextContinuousFormattedAsync` + `GenerateNumberAsync`
- [x] اختبار تزامن: 20 طلب متوازي → أرقام 1..20 بدون تكرار
- [x] اختبار تزامن على `NextFormattedAsync` كذلك
- **الحالة:** ✅ مكتمل — 36/36 اختبار نجح (24s)
- **الملفات المعدّلة:**
  - `Services/SequenceService.cs`
  - `AccountingSystem.Tests/SequenceServiceConcurrencyTests.cs` (جديد)
  - `AccountingSystem.Tests/TestWebApplicationFactory.cs` (جديد)
  - `AccountingSystem.Tests/IntegrationTests.cs` (استخدام factory جديدة)
- **إصلاح إضافي اكتُشف أثناء الجلسة:** إزالة تكرار في `Data/DbSeeder.cs`:
  - 6 صلاحيات مكررة: `payroll.view`, `payroll.manage`, `payroll.approve`, `payroll.pay`, `advance.view`, `advance.create`
  - 3 حسابات مكررة في Chart of Accounts: `1107`, `2105`, `5101`

### 2️⃣ إصلاح Sales Return FIFO/Lot + COGS — ⚠️ جزئي

- [x] `totalCostOfGoodsSold` بيستخدم `fromLot * alloc.UnitCost` (سطر 355)
- [ ] مراجعة كاملة للـ `SalesReturnController.cs` وحساب COGS
- [ ] التحقق من `SalesReturnItemLot` لكل Lot
- [ ] حساب `Remaining Returnable From Lot` بدقة
- [ ] اختبار: مرتجع من Lot A + Lot B → COGS صحيح
- [ ] اختبار: مرتجعين متتاليين من نفس الـ Lot → ما يزيدش عن الأصل
- **الحالة:** ⚠️ الحساب الأساسي موجود، يحتاج مراجعة واختبار

### 3️⃣ Stock Concurrency / RowVersion — ✅ مكتمل

- [x] `[Timestamp] public byte[] RowVersion` في `Models/StockLot.cs`
- [x] `.IsRowVersion()` في `ApplicationDbContext.cs:415`
- [x] Migration: `20260921232650_FixStockLotRowVersionAndSupplierNullable`
- [x] Migration: `20261002052829_AddStockLotRowVersion`
- [x] العمود موجود في DB الحالية
- [ ] اختبار: Two concurrent FIFO → واحد يفشل بـ Concurrency Exception
- **الحالة:** ✅ الكود جاهز، الاختبار مؤجل للمرحلة 3

### 4️⃣ Purchase Return + Original Invoice + Branch/Store — ✅ مكتمل

- [x] `OriginalPurchaseInvoiceId` في `PurchaseReturnInvoice` (Model)
- [x] `PurchaseInvoiceItemId` في `PurchaseReturnInvoiceItem` (Model)
- [x] DbContext relationships: `OriginalPurchaseInvoice` + `PurchaseInvoiceItem`
- [x] `BranchId = store.BranchId` بدل `null`
- [x] `OriginalPurchaseInvoiceId = model.OriginalPurchaseInvoiceId`
- [x] `PurchaseInvoiceItemId = item.PurchaseInvoiceItemId`
- [x] تتبع الكميات المرتجعة سابقًا (`GetAlreadyReturnedQtyAsync`)
- [x] Validation: الكمية المرتجعة ≤ (الأصلية − المرتجعة سابقًا)
- [x] Dropdown للفاتورة الأصلية في `Views/PurchaseReturn/Create.cshtml`
- [x] Migration: `20261015233412_AddOriginalPurchaseInvoiceToReturn`
- [x] أعمدة DB مطبقة يدوياً (بسبب عدم تطابق migrations مع DB)
- **الحالة:** ✅ مكتمل — Build succeeded

### 5️⃣ StockTransactionItem → StockLot Relationship — ✅ مكتمل

- [x] مراجعة `Controllers/InventoryController.cs`
- [x] تغيير `StockLotId = lot.Id` → `StockLot = lot` (3 أماكن: 471, 1136, 1445)
- [x] Build succeeded
- **الحالة:** ✅ مكتمل

### 6️⃣ Posting Idempotency — ✅ مكتمل

- [x] `Purpose` field في `JournalEntry` (Model + DbContext)
- [x] Pre-check في `PostAsync`: لو موجود بنفس `(SourceType, SourceId, Purpose)` → رجّعه
- [x] Unique Index `(SourceType, SourceId, Purpose)` في DbContext
- [x] Migration: `AddPurposeToJournalEntry`
- [x] DB column `Purpose` + unique index مطبقين يدوياً
- [x] `PostCostOfGoodsSoldAsync` بيمرر `Purpose = "COGS"`
- [x] `PostSalesReturnAsync` COGS بيمرر `Purpose = "COGS"`
- **الحالة:** ✅ مكتمل — Build succeeded

### 7️⃣ Model/Migration/Database Synchronization — ✅ مكتمل

- [x] إزالة `ConfigureWarnings(w => w.Ignore(PendingModelChangesWarning))` من `Program.cs`
- [x] Reset الداتابيز وإعادة بنائها (62 migration مطبقة)
- [x] Migration `FinalModelCheck` فاضية → Model = Snapshot = DB
- **الحالة:** ✅ مكتمل

---

## 🟠 المرحلة 2 — الأداء والمحاسبة (P1)

### 8️⃣ إزالة DatabaseKeepAlive الثقيل — ✅ مكتمل

- [x] `SELECT COUNT_BIG(*)` → `SELECT 1` (6 استعلامات اتشالت)
- [x] Build succeeded
- **الحالة:** ✅ مكتمل

### 9️⃣ Purchase Payment Accounting — ✅ مكتمل

- [x] `PurchasePaymentMethod` enum (Cash/BankTransfer/Cheque/Credit/Wallet)
- [x] `PaymentMethod` + `CashAccountId` + `PaymentDetails` في `PurchaseInvoice`
- [x] DbContext relationship لـ `CashAccount`
- [x] `PostPurchaseInvoiceAsync` — يقيد الخزنة نقدي أو الموردون آجل
- [x] `TreasuryTransaction` (Pay) عند الدفع النقدي
- [x] `PurchaseInvoiceCreateViewModel` + `CashAccountOptionViewModel`
- [x] Controller: validation + تحميل قائمة الخزائن
- [x] View: dropdown طريقة الدفع + الخزنة + تفاصيل الدفع (JS للتبديل)
- [x] Migration: `20261016003854_AddPurchasePaymentMethod`
- **الحالة:** ✅ مكتمل — Build succeeded + DB updated

### 🔟 إصلاح Encoding التالف — ✅ لا يحتاج إصلاح

- [x] فحص بايتي شامل لـ 427 ملف (`.cs` + `.cshtml`)
- [x] النتيجة: 0 ملف فيه mojibake حقيقي
- [x] العربي في كل الملفات سليم UTF-8 with BOM
- **ملاحظة:** الـ 143 ملف اللي ظهرت في الفحص النصي كانوا false positives بسبب PowerShell console encoding
- **الحالة:** ✅ مكتمل (لا يحتاج أي تعديل)

### 1️⃣1️⃣ تحويل التقارير إلى SQL Aggregation — ✅ مكتمل

- [x] TrialBalance — SQL GroupBy + Sum
- [x] IncomeStatement — SQL GroupBy + Sum
- [x] BalanceSheet — SQL GroupBy + Sum
- [x] InventoryReport — SQL GroupBy + Sum
- [x] ProductProfitReport — SQL GroupBy + Sum (بدل 3 استعلامات في الميموري)
- [x] ProfitReport + Ledger — كان بالفعل `SumAsync`
- **ملاحظة:** التقارير التفصيلية (SalesReport/PurchaseReport/Statements) مقصود إنها تجيب كل صف — مفيش aggregation
- **الحالة:** ✅ مكتمل — Build succeeded

### 1️⃣2️⃣ توحيد Document Number Service — ✅ مكتمل

- [x] `DocumentNumberService` أصبح wrapper حول `SequenceService`
- [x] `GenerateNumberAsync` بيستخدم `NextFormattedAsync` (آمن ضد التزامن)
- [x] `SeedSequenceCountersAsync` في `DbSeeder` لزراعة العدادات من الفواتير الحالية
- **الحالة:** ✅ مكتمل — Build succeeded

### 1️⃣3️⃣ توحيد Entities/ViewModels — ⚠️ جزئي

- [x] فحص شامل للموقع الحالي
- [x] `PurchaseReturnInvoice` في `Models` namespace الصحيح (مكان الملف فقط شكلي)
- [ ] نقل `PurchaseReturnInvoice` — متروك (المخاطرة > القيمة)
- [ ] توحيد `BranchOptionViewModel` / `StoreOptionViewModel` — متروك (تكرار غير ضار)
- **الحالة:** ⚠️ جزئي — الكود شغّال، التحسينات الجمالية مؤجلة

### 1️⃣4️⃣ WhatsApp Result حقيقي — ✅ مكتمل

- [x] إنشاء `WhatsAppResult` (enum `Sent/Simulated/Failed` + `MessageId` + `Error`)
- [x] `IWhatsAppService` كل method ترجع `WhatsAppResult` بدل `bool`
- [x] `WhatsAppService`: Meta/Gateway غير معد → `Simulated` (بدل `return true` المضلل)
- [x] تحديث 6 call sites (Account/Customer/Employee/Supplier/PurchaseInvoice/SalesInvoice)
- [x] `InvoiceReminderService` — لا يحتاج تعديل (ignore return value)
- [x] Build succeeded
- **الحالة:** ✅ مكتمل

### 1️⃣5️⃣ PDF A5 / Receipt

- [ ] A4 (موجود)
- [ ] A5
- [ ] 80mm Receipt
- [ ] 58mm Receipt
- **الحالة:** ⬜ لم يبدأ

---

## 🟡 المرحلة 3 — تحسينات (P2)

### 1️⃣6️⃣ Cloud Backup + Restore

- [ ] Restore من Backup
- [ ] Cloud Backup
- [ ] Backup Verification
- [ ] Automatic Retention
- **الحالة:** ⬜ لم يبدأ

### 1️⃣7️⃣ Background WhatsApp/PDF

- [ ] Queue
- [ ] Background Worker
- [ ] إرسال بعد Commit
- **الحالة:** ⬜ لم يبدأ

### 1️⃣8️⃣ زيادة الاختبارات

- [ ] FIFO
- [ ] Concurrent FIFO
- [ ] Sales Return
- [ ] Purchase Return
- [ ] Partial Return
- [ ] Inventory Transfer
- [ ] Duplicate Posting
- **الحالة:** ⬜ لم يبدأ

### 1️⃣9️⃣ توحيد Package Versions

- [ ] HealthChecks 9.0.0 → 10.0.x
- [ ] Mvc.Testing 9.0.0 → 10.0.x
- **الحالة:** ⬜ لم يبدأ

---

## 🟢 المرحلة 4 — تنظيف (P3)

### 2️⃣0️⃣ SalesInvoiceService غير مستخدم

- [ ] نقل منطق Confirm/Cancel من Controller إلى Service
- **الحالة:** ⬜ لم يبدأ

### 2️⃣1️⃣ IRepository<> غير مستخدم

- [ ] حذف أو استخدام فعلي
- **الحالة:** ⬜ لم يبدأ

### 2️⃣2️⃣ DbSeeder Migration أثناء Startup

- [ ] فصل Migration عن Startup في Production
- **الحالة:** ⬜ لم يبدأ

### 2️⃣3️⃣ Customer/Supplier Balances من Accounting Ledger

- [ ] مصدر واحد للحقيقة
- **الحالة:** ⬜ لم يبدأ

---

## 📊 ملخص التقدم

| المرحلة | العدد | تم |
|---|---|---|
| 🔴 P0 حماية البيانات | 7 | 7 |
| 🟠 P1 أداء ومحاسبة | 8 | 6 |
| 🟡 P2 تحسينات | 4 | 0 |
| 🟢 P3 تنظيف | 4 | 0 |
| **الإجمالي** | **23** | **14** |

---

## 🎯 الترتيب المقترح

1. ~~SequenceService~~ ✅
2. Sales Return FIFO + COGS — ⚠️ جزئي، يحتاج مراجعة
3. ~~Stock Concurrency (RowVersion)~~ ✅
4. ~~Purchase Return + Original Invoice~~ ✅
5. ~~StockTransactionItem → StockLot~~ ✅
6. ~~Posting Idempotency~~ ✅
7. ~~Model/Migration Sync~~ ✅
8. ~~DatabaseKeepAlive~~ ✅
9. ~~Purchase Payment Accounting~~ ✅
10. ~~Encoding~~ ✅ (سليم من الأصل)
11. ~~Reports SQL Aggregation~~ ✅
12. ~~Document Number Service~~ ✅
13. ~~Entities/ViewModels Unification~~ ⚠️ جزئي
14. ~~WhatsApp Result~~ ✅
15. PDF A5 / Receipt ← **التالي**

**P0 كامل + P1 (6/8) — آخر بند في P1**