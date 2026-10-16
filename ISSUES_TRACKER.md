# 🔴 تتبع مشاكل المشروع

آخر تحديث: 2026-10-16

---

## 🚨 المرحلة 1 — حماية البيانات (P0)

### 1️⃣ إصلاح SequenceService Transaction — ✅ مكتمل

- [x] `WITH (UPDLOCK, HOLDLOCK, ROWLOCK)` لمنع race أول رقم في اليوم
- [x] `effectiveDate` يتحسب مرة واحدة في `NextFormattedAsync`
- [x] `ChangeTracker` detach موجّه بدل `Clear()`
- [x] اختبار تزامن: 20 طلب متوازي → أرقام 1..20 بدون تكرار
- **الحالة:** ✅ مكتمل — 36/36 اختبار نجح

### 2️⃣ إصلاح Sales Return FIFO/Lot + COGS — ✅ مكتمل

- [x] `totalCostOfGoodsSold += weightedCost` → `+= lotCost`
- [x] LIFO → FIFO (OrderBy PurchaseDate)
- [x] `Total = QuantityInBaseUnit * UnitPrice`
- [x] `remainingInLot = alloc.QuantityBaseUnit - alreadyReturned`
- [x] يمنع `Lot.QuantityRemaining` من تجاوز `QuantityReceived`
- [ ] اختبار FIFO من Lot A + Lot B → مؤجل لبند #18
- **الحالة:** ✅ مكتمل

### 3️⃣ Stock Concurrency / RowVersion — ✅ مكتمل

- [x] `[Timestamp] public byte[] RowVersion`
- [x] `.IsRowVersion()` في DbContext
- [x] Migration مطبقة
- [ ] اختبار Two concurrent FIFO → مؤجل لبند #18
- **الحالة:** ✅ مكتمل

### 4️⃣ Purchase Return + Original Invoice + Branch/Store — ✅ مكتمل

- [x] كل الحقول + العلاقات + Validation + UI
- **الحالة:** ✅ مكتمل

### 5️⃣ StockTransactionItem → StockLot — ✅ مكتمل

- [x] `StockLotId = lot.Id` → `StockLot = lot` (3 أماكن)
- **الحالة:** ✅ مكتمل

### 6️⃣ Posting Idempotency — ✅ مكتمل

- [x] `Purpose` field + Unique Index `(SourceType, SourceId, Purpose)`
- [x] Pre-check في `PostAsync`
- **الحالة:** ✅ مكتمل

### 7️⃣ Model/Migration/Database Sync — ✅ مكتمل

- [x] إزالة `PendingModelChangesWarning`
- [x] Reset DB وإعادة البناء (62 migration)
- **الحالة:** ✅ مكتمل

**🎉 P0: 7/7 كامل**

---

## 🟠 المرحلة 2 — الأداء والمحاسبة (P1)

### 8️⃣ إزالة DatabaseKeepAlive الثقيل — ✅ مكتمل

- [x] `SELECT COUNT_BIG(*)` → `SELECT 1`
- **الحالة:** ✅ مكتمل

### 9️⃣ Purchase Payment Accounting — ✅ مكتمل

- [x] `PurchasePaymentMethod` enum + `CashAccountId`
- [x] `PostPurchaseInvoiceAsync` يقيد الخزنة أو الموردون
- [x] `TreasuryTransaction` (Pay) عند الدفع النقدي
- [x] Migration: `AddPurchasePaymentMethod`
- **الحالة:** ✅ مكتمل

### 🔟 إصلاح Encoding — ✅ لا يحتاج إصلاح

- [x] فحص بايتي لـ 427 ملف — 0 mojibake حقيقي
- **الحالة:** ✅ مكتمل

### 1️⃣1️⃣ SQL Aggregation للتقارير — ✅ مكتمل

- [x] TrialBalance, IncomeStatement, BalanceSheet, InventoryReport, ProductProfitReport
- **الحالة:** ✅ مكتمل

### 1️⃣2️⃣ توحيد Document Number Service — ✅ مكتمل

- [x] `DocumentNumberService` wrapper حول `SequenceService`
- [x] `SeedSequenceCountersAsync` في `DbSeeder`
- **الحالة:** ✅ مكتمل

### 1️⃣3️⃣ توحيد Entities/ViewModels — ✅ مكتمل

- [x] نقل `PurchaseReturnInvoices.cs` إلى `Models/`
- [x] `Shared/LookupViewModels.cs` موحّد
- [x] حذف التكرار من 3 ViewModels
- **الحالة:** ✅ مكتمل

### 1️⃣4️⃣ WhatsApp Result حقيقي — ✅ مكتمل

- [x] `WhatsAppResult` + `WhatsAppStatus` enum
- [x] 6 call sites محدّثة
- **الحالة:** ✅ مكتمل

### 1️⃣5️⃣ PDF A5 / Receipt — ✅ مكتمل

- [x] `InvoicePageSize` enum (A4/A5/Receipt80/Receipt58)
- [x] `PdfInvoiceService` layout ديناميكي
- [x] `DownloadPdf` endpoints
- [x] Dropdown في Details pages
- **الحالة:** ✅ مكتمل

**🎉 P1: 8/8 كامل**

---

## 🟡 المرحلة 3 — تحسينات (P2)

### 1️⃣6️⃣ Cloud Backup + Restore — ⚠️ جزئي (3/4)

- [x] Backup Verification (`RESTORE VERIFYONLY`)
- [x] Automatic Retention (`BackupRetentionService`)
- [x] Restore (VERIFYONLY → SINGLE_USER → RESTORE → MULTI_USER + تأكيد مزدوج)
- [ ] Cloud Backup — مؤجل (يحتاج اختيار provider)
- **الحالة:** ⚠️ جزئي (3/4)

### 1️⃣7️⃣ Background WhatsApp/PDF

- [ ] Queue + Background Worker + إرسال بعد Commit
- **الحالة:** ⬜ لم يبدأ

### 1️⃣8️⃣ زيادة الاختبارات

- [ ] FIFO, Concurrent FIFO, Sales Return, Purchase Return,
      Partial Return, Inventory Transfer, Duplicate Posting
- **الحالة:** ⬜ لم يبدأ

### 1️⃣9️⃣ توحيد Package Versions — ✅ مكتمل

- [x] `HealthChecks` 9.0.0 → 10.0.12
- [x] `HealthChecks.EntityFrameworkCore` 9.0.0 → 10.0.12
- [x] `Mvc.Testing` 9.0.0 → 10.0.12
- [x] Build succeeded
- **الحالة:** ✅ مكتمل

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
| 🟠 P1 أداء ومحاسبة | 8 | 8 |
| 🟡 P2 تحسينات | 4 | 2 |
| 🟢 P3 تنظيف | 4 | 0 |
| **الإجمالي** | **23** | **17** |

---

## 🎯 الترتيب المقترح

**✅ مكتمل:**
1. ~~SequenceService~~
2. ~~Sales Return FIFO + COGS~~
3. ~~Stock Concurrency (RowVersion)~~
4. ~~Purchase Return + Original Invoice~~
5. ~~StockTransactionItem → StockLot~~
6. ~~Posting Idempotency~~
7. ~~Model/Migration Sync~~
8. ~~DatabaseKeepAlive~~
9. ~~Purchase Payment Accounting~~
10. ~~Encoding~~
11. ~~Reports SQL Aggregation~~
12. ~~Document Number Service~~
13. ~~Entities/ViewModels Unification~~
14. ~~WhatsApp Result~~
15. ~~PDF A5 / Receipt~~
19. ~~Unified Package Versions~~

**⏳ المتبقي:**
- 16. Cloud Backup (مؤجل)
- 17. Background WhatsApp/PDF
- 18. زيادة الاختبارات
- 20-23. P3 (تنظيف)