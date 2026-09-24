# ادامه کار

آخرین کار: مرتب‌سازی پوشه‌ها طبق انتخاب صریح کاربر؛ فقط Controllers، Services، DTOs و Interfaces، با حفظ دسترسی مستقیم از IApplicationDbContext و بدون افزودن Repository.

شروع بررسی: [AGENTS](../AGENTS.md)، [ARCHITECTURE](ARCHITECTURE.md) (درخت پوشه‌ها و ADR-011)، [STATUS](STATUS.md) و [BACKLOG](BACKLOG.md).

- هر شش کنترلر در `src/HireYar.Api/Controllers` هستند؛ پاسخ‌های HTTP در Api/DTOs.
- Application/Services، DTOs و Interfaces مستقل‌اند؛ سرویس‌های Infrastructure در Infrastructure/Services قرار دارند. namespaceها، routeها و مدل دیتابیس حفظ شدند.
- build Release موفق با صفر warning/error؛ تست‌های SQL: ۲۳ موفق و یک شکست. گزارش در TestResults/folder-structure.trx خارج Git است.
- شکست تست Production از قبل در commit پایه 1b86ec4 موجود است: شرط Development برای MapOpenApi در Program.cs کامنت شده. این فایل در بازآرایی تغییر نکرد؛ برای تعیین رفتار نهایی HY-010 را بررسی کنید. نتایج ۲۴/۲۴ قدیمی را به checkout فعلی تعمیم ندهید.
- Docker/SMTP واقعی همچنان مطابق STATUS بررسی نشده‌اند؛ HY-006 و HY-007 باقی‌اند.

برنچ dev؛ تغییرات این نوبت commit نشده‌اند و Git جابه‌جایی فایل‌ها را فعلاً به‌صورت حذف قدیمی و فایل جدید نشان می‌دهد. هیچ push/deploy/تغییر production انجام نشده است. SDK 10.0.400 و SQL Server محلی موجودند؛ برای تست HIREYAR_TEST_SQL لازم است. User Secrets توسعه قبلاً تنظیم شده‌اند؛ هیچ secret در ریپو ثبت نشده است.
