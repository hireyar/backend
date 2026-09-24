# HireYar

بک‌اند سازمانی مرحله اول با .NET 10، Controller، EF Core 10، SQL Server و Identity؛ پنل Angular در این ریپو نیست.

پیش از تغییر، به ترتیب بخوان: [PROJECT](docs/PROJECT.md)، [ARCHITECTURE](docs/ARCHITECTURE.md)، [STATUS](docs/STATUS.md)، [BACKLOG](docs/BACKLOG.md)، [HANDOFF](docs/HANDOFF.md)، سپس کد مرتبط.

- وضعیت Git را بررسی و تغییرات موجود کاربر را حفظ کن. توسعه روی `dev`؛ `production` فقط نسخه آماده انتشار. بدون درخواست صریح push، deploy یا تغییر production نکن.
- Domain مستقل؛ Application فقط به Domain و EF abstraction وابسته است؛ Infrastructure به Application؛ Api به Application و Infrastructure برای composition. Identity و SQL provider فقط Infrastructure؛ HttpContext فقط Api.
- هر کاربر یک سازمان؛ شناسه سازمان و نقش از هویت بررسی‌شده سرور. تمام دسترسی‌ها scoped؛ رابطه بین سازمان‌ها ممنوع؛ نبود رکورد در سازمان جاری 404.
- بدون MediatR، CQRS، Generic Repository، distributed infrastructure یا زیرساخت مراحل آینده. DTO مجزا، mapping دستی، UTC، Guid و CancellationToken.
- secret، token، cookie، لینک ایمیل حساس و داده واقعی کاربران در Git یا لاگ ثبت نشود. ایمیل تست فقط به SMTP محلی.
- قبل از اعلام تکمیل، build و تست مرتبط را واقعاً اجرا کن. SQL InMemory/SQLite اثبات رفتار SQL Server نیست.
- STATUS و BACKLOG هنگام پایان/توقف، ARCHITECTURE هنگام تغییر تصمیم، و HANDOFF پیش از تحویل به‌روز شوند.
- یادداشت قدیمی دلیل تکمیل یا نقص نیست؛ اختلاف کد و مستندات را بررسی و ثبت کن. تصمیم پذیرفته‌شده را بدون دلیل بازطراحی نکن. پاسخ موجود در مستندات را دوباره نپرس. Backlog مجوز اجرای خودکار مراحل دیگر نیست.

فرمان‌ها از ریشه:

```powershell
dotnet tool restore
dotnet restore HireYar.slnx
dotnet build HireYar.slnx --no-restore
dotnet test HireYar.slnx
# HIREYAR_TEST_SQL فقط SQL محلی؛ fixture دیتابیس اختصاصی تصادفی می‌سازد و همان را حذف می‌کند.
# ConnectionStrings__Default برای EF tooling الزامی است.
dotnet ef database update --project src/HireYar.Infrastructure --startup-project src/HireYar.Api
dotnet ef migrations script --idempotent --project src/HireYar.Infrastructure --startup-project src/HireYar.Api --output artifacts/migrations.sql
```
