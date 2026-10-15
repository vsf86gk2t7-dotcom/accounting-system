\# 🔴 تتبع مشاكل المشروع



آخر تحديث: \_\_\_\_\_\_\_\_\_\_\_\_



\---



\## 🚨 المرحلة 1 — حماية البيانات (P0)



\### 1️⃣ إصلاح SequenceService Transaction

\- \[ ] مراجعة `Services/SequenceService.cs`

\- \[ ] إضافة `CommitAsync()` في المسار المستقل

\- \[ ] دعم Transaction خارجية

\- \[ ] اختبار: توليد رقم بدون Transaction خارجية → يثبت في DB

\- \*\*الحالة:\*\* ⬜ لم يبدأ



\### 2️⃣ إصلاح Sales Return FIFO/Lot + COGS

\- \[ ] مراجعة `Controllers/SalesReturnController.cs`

\- \[ ] إصلاح `totalCostOfGoodsSold += weightedCost` → `fromLot \\\\\\\* alloc.UnitCost`

\- \[ ] إضافة تتبع `SalesReturnItemLot` لكل Lot

\- \[ ] حساب `Remaining Returnable From Lot` بدقة

\- \[ ] اختبار: مرتجع من Lot A + Lot B → COGS صحيح

\- \[ ] اختبار: مرتجعين متتاليين من نفس الـ Lot → ما يزيدش عن الأصل

\- \*\*الحالة:\*\* ⬜ لم يبدأ



\### 3️⃣ Stock Concurrency / RowVersion

\- \[ ] إضافة `\\\\\\\[Timestamp] public byte\\\\\\\[] RowVersion` في `Models/StockLot.cs`

\- \[ ] Migration صحيحة

\- \[ ] التأكد إن Model = Migration = DB

\- \[ ] اختبار: Two concurrent FIFO → واحد يفشل بـ Concurrency Exception

\- \*\*الحالة:\*\* ⬜ لم يبدأ



\### 4️⃣ Purchase Return + Original Invoice + Branch/Store

\- \[ ] إضافة `OriginalPurchaseInvoiceId` في `PurchaseReturnViewModel`

\- \[ ] إضافة `PurchaseInvoiceItemId` لكل بند

\- \[ ] `BranchId = store.BranchId` بدل `null`

\- \[ ] تتبع الكميات المرتجعة سابقًا

\- \*\*الحالة:\*\* ⬜ لم يبدأ



\### 5️⃣ StockTransactionItem → StockLot Relationship

\- \[ ] مراجعة `Controllers/InventoryController.cs`

\- \[ ] تغيير `StockLotId = lot.Id` → `StockLot = lot`

\- \[ ] اختبار: حفظ استلام مخزون → العلاقة صحيحة

\- \*\*الحالة:\*\* ⬜ لم يبدأ



\### 6️⃣ Posting Idempotency

\- \[ ] مراجعة `Services/PostingService.cs`

\- \[ ] إضافة تحقق: هل يوجد JournalEntry بنفس SourceType + SourceId؟

\- \[ ] إضافة Unique Index على `(SourceType, SourceId)`

\- \[ ] اختبار: استدعاء Posting مرتين → قيد واحد فقط

\- \*\*الحالة:\*\* ⬜ لم يبدأ



\### 7️⃣ Model/Migration/Database Synchronization

\- \[ ] إزالة `ConfigureWarnings(w => w.Ignore(PendingModelChangesWarning))`

\- \[ ] مراجعة كل Migration فيها `Up()` فارغ

\- \[ ] `dotnet ef migrations add FinalSync` إذا لزم

\- \[ ] التأكد: Model = Snapshot = DB

\- \*\*الحالة:\*\* ⬜ لم يبدأ



\---



\## 🟠 المرحلة 2 — الأداء والمحاسبة (P1)



\### 8️⃣ إزالة DatabaseKeepAlive الثقيل

\- \[ ] `SELECT COUNT\\\\\\\_BIG(\\\\\\\*)` → `SELECT 1`

\- \[ ] أو حذف الخدمة بالكامل

\- \*\*الحالة:\*\* ⬜ لم يبدأ



\### 9️⃣ Purchase Payment Accounting

\- \[ ] إضافة `PaymentMethod` + `CashAccountId` في `PurchaseInvoice`

\- \[ ] تعديل `PostingService` للتعامل مع الدفع النقدي

\- \[ ] تعديل `PurchaseInvoiceCreateViewModel` والـ View

\- \*\*الحالة:\*\* ⬜ لم يبدأ



\### 🔟 إصلاح Encoding التالف

\- \[ ] قائمة الملفات المتأثرة (15 ملف)

\- \[ ] استعادة النص العربي الصحيح

\- \[ ] حفظ UTF-8 with BOM

\- \*\*الحالة:\*\* ⬜ لم يبدأ



\### 1️⃣1️⃣ تحويل التقارير إلى SQL Aggregation

\- \[ ] Trial Balance

\- \[ ] Ledger

\- \[ ] Income Statement

\- \[ ] Balance Sheet

\- \[ ] Inventory reports

\- \*\*الحالة:\*\* ⬜ لم يبدأ



\### 1️⃣2️⃣ توحيد Document Number Service

\- \[ ] دمج `DocumentNumberService` + `SequenceService`

\- \[ ] استخدام مصدر واحد لكل الأرقام

\- \*\*الحالة:\*\* ⬜ لم يبدأ



\### 1️⃣3️⃣ توحيد Entities/ViewModels

\- \[ ] نقل `PurchaseReturnInvoice` من ViewModels إلى Models

\- \[ ] نقل `BranchOptionViewModel` / `StoreOptionViewModel` لمكان مشترك

\- \*\*الحالة:\*\* ⬜ لم يبدأ



\### 1️⃣4️⃣ WhatsApp Result حقيقي

\- \[ ] تغيير `return true` في حالة NotConfigured → `Simulated`

\- \[ ] إنشاء `WhatsAppResult { Status, MessageId, Error }`

\- \*\*الحالة:\*\* ⬜ لم يبدأ



\### 1️⃣5️⃣ PDF A5 / Receipt

\- \[ ] A4 (موجود)

\- \[ ] A5

\- \[ ] 80mm Receipt

\- \[ ] 58mm Receipt

\- \*\*الحالة:\*\* ⬜ لم يبدأ



\---



\## 🟡 المرحلة 3 — تحسينات (P2)



\### 1️⃣6️⃣ Cloud Backup + Restore

\- \[ ] Restore من Backup

\- \[ ] Cloud Backup

\- \[ ] Backup Verification

\- \[ ] Automatic Retention

\- \*\*الحالة:\*\* ⬜ لم يبدأ



\### 1️⃣7️⃣ Background WhatsApp/PDF

\- \[ ] Queue

\- \[ ] Background Worker

\- \[ ] إرسال بعد Commit

\- \*\*الحالة:\*\* ⬜ لم يبدأ



\### 1️⃣8️⃣ زيادة الاختبارات

\- \[ ] FIFO

\- \[ ] Concurrent FIFO

\- \[ ] Sales Return

\- \[ ] Purchase Return

\- \[ ] Partial Return

\- \[ ] Inventory Transfer

\- \[ ] Duplicate Posting

\- \*\*الحالة:\*\* ⬜ لم يبدأ



\### 1️⃣9️⃣ توحيد Package Versions

\- \[ ] HealthChecks 9.0.0 → 10.0.x

\- \[ ] Mvc.Testing 9.0.0 → 10.0.x

\- \*\*الحالة:\*\* ⬜ لم يبدأ



\---



\## 🟢 المرحلة 4 — تنظيف (P3)



\### 2️⃣0️⃣ SalesInvoiceService غير مستخدم

\- \[ ] نقل منطق Confirm/Cancel من Controller إلى Service

\- \*\*الحالة:\*\* ⬜ لم يبدأ



\### 2️⃣1️⃣ IRepository<> غير مستخدم

\- \[ ] حذف أو استخدام فعلي

\- \*\*الحالة:\*\* ⬜ لم يبدأ



\### 2️⃣2️⃣ DbSeeder Migration أثناء Startup

\- \[ ] فصل Migration عن Startup في Production

\- \*\*الحالة:\*\* ⬜ لم يبدأ



\### 2️⃣3️⃣ Customer/Supplier Balances من Accounting Ledger

\- \[ ] مصدر واحد للحقيقة

\- \*\*الحالة:\*\* ⬜ لم يبدأ



\---



\## 📊 ملخص التقدم



| المرحلة | العدد | تم |

|---|---|---|

| 🔴 P0 حماية البيانات | 7 | 0 |

| 🟠 P1 أداء ومحاسبة | 8 | 0 |

| 🟡 P2 تحسينات | 4 | 0 |

| 🟢 P3 تنظيف | 4 | 0 |

| \*\*الإجمالي\*\* | \*\*23\*\* | \*\*0\*\* |



\---



\## 🎯 الترتيب المقترح للبدء



1\. SequenceService ← نبدأ بيها الآن

2\. Sales Return FIFO + COGS

3\. Stock Concurrency

4\. Purchase Return

5\. StockTransactionItem → StockLot

6\. Posting Idempotency

7\. Model/Migration Sync

