# معماری و تصمیم‌ها

## ساختار واقعی

`HireYar.slnx` شامل Domain (Organizations/JobPositions/InterviewTemplates)، Application (سرویس‌ها، DTO، validation و قراردادها)، Infrastructure (Identity، SQL/DbContext/mapping/migrations، JWT، SMTP، دعوت) و Api (Controller، HTTP، مجوزها، CSRF، composition) و `tests/HireYar.Tests` است.

وابستگی: Application → Domain؛ Infrastructure → Application/Domain؛ Api → Application و Infrastructure. مسیر درخواست Controller → Application Service → DbContext abstraction یا مرز Identity/SMTP است. سرویس Auth مرز Identity را فراخوانی می‌کند؛ UserManager و تراکنش‌های وابسته به آن در Infrastructure می‌مانند. HttpContext فقط در Api است.

## تصمیم‌های پذیرفته‌شده

| شناسه | تصمیم، دلیل و پیامد |
|---|---|
| ADR-001 | .NET 10، EF Core 10، PostgreSQL و Controller؛ یک برنامه و یک دیتابیس برای سادگی عملیاتی |
| ADR-002 | Clean Architecture سبک؛ EF DbSet در IApplicationDbContext مجاز است تا repository عمومی نسازیم؛ Application به EF وابسته می‌شود ولی به SQL provider نه |
| ADR-003 | Identity فقط Infrastructure؛ یک کاربر/سازمان و نقش enum محدود Owner/Recruiter؛ unique index ایمیل نرمال و Owner سازمان |
| ADR-004 | JWT با HS256، issuer/audience/expiry/jti؛ وضعیت و نقش در هر درخواست از DB دوباره خوانده می‌شود؛ هزینه یک query در مقابل اثر فوری مجوز |
| ADR-005 | refresh تصادفی ۲۵۶بیتی، ذخیره SHA-256، یک family در هر login؛ تمام mutationهای نشست زیر قفل SELECT FOR UPDATE کاربر و تراکنش؛ replay خانواده همان نشست را باطل می‌کند |
| ADR-006 | cookie host-only محدود به auth و CSRF استاندارد ASP.NET؛ SameSite Strict برای deployment زیر یک سایت؛ SPA توکن CSRF را با هویت فعلی تازه می‌کند |
| ADR-007 | queryها صریحاً OrganizationId سرور را شرط می‌کنند؛ query filter عمومی نداریم. کلید مرکب JobPosition/Organization در FK قالب از اتصال بین سازمان‌ها در DB جلوگیری می‌کند |
| ADR-008 | RowVersion تصادفی ۱۶بایتی application-managed و 409؛ ویرایش parent و جایگزینی سؤال‌ها تراکنشی، شناسه سؤال‌های باقی‌مانده حفظ می‌شود؛ delete/insert داخلی سؤال امکان جابه‌جایی ترتیب زیر unique index می‌دهد |
| ADR-009 | دعوت Recruiter با hash و انقضای سه‌روزه؛ قفل سازمان عملیات دعوت را سریالی می‌کند؛ unique index رقابت ایمیل و دعوت باز را نیز پوشش می‌دهد |
| ADR-010 | بدون MediatR/CQRS/Generic Repository/worker/OTP؛ توسعه آینده فقط با تسک مستقل |

همه Guid و تاریخ‌ها DateTimeOffset UTC هستند. cascade delete تمام روابط Restrict است. enumهای وضعیت و نقش در SQL به متن محدود و check constraint ذخیره می‌شوند. DTOها اجازه overposting ندارند و Entity مستقیم برنمی‌گردد. pagination پیش‌فرض ۲۰، سقف ۱۰۰ و ترتیب CreatedAtUtc سپس Id است.

حدود متن: نام شخص/سازمان/عنوان ۲۰۰، ایمیل ۲۵۶، شرح موقعیت ۱۰۰۰۰، seniority صد، language بیست، سؤال ۲۰۰۰، competency دویست، راهنمای ارزیابی ۴۰۰۰. رمز ۱۲ تا ۱۲۸ و الزامات استاندارد Identity. قالب ۱ تا ۳۰ سؤال، ترتیب پیوسته از ۱، زمان ۵ تا ۶۰ و follow-up صفر تا سه.

Identity token provider استاندارد با اعتبار دو ساعت و Data Protection دائمی استفاده می‌شود. تأیید دوباره ایمیل با flag و قفل رد می‌شود؛ reset با security stamp یک‌بارمصرف است. ساخت سازمان و Owner تراکنشی؛ ارسال ایمیل پس از commit است. شکست SMTP داده ناقص نمی‌سازد و resend ممکن است. forgot/resend همیشه پاسخ عمومی یکسان دارند.

ProblemDetails/ValidationProblemDetails با code و traceId؛ ۴۰۰ validation، ۴۰۱ authentication، ۴۰۳ role، ۴۰۴ رکورد خارج scope، ۴۰۹ conflict، ۴۲۹ rate limit، ۵۰۰ خطای عمومی. لاگ درخواست حساس یا body/token فعال نیست؛ رخداد ارسال ناموفق بدون recipient/link ثبت می‌شود.

ADR-013: Swagger UI و OpenAPI JSON در همه محیط‌ها بدون توکن در دسترس‌اند تا قرارداد API روی سرور قابل مشاهده باشد؛ Swagger middleware پیش از fallback authorization اجرا می‌شود. endpointهای داده و حساب همچنان سیاست احراز هویت و نقش خود را دارند. تنظیمات اتصال و کلید امضا برای هر محیط جداگانه و خارج Git نگهداری می‌شوند.

موضوع باز محیطی: صحت Docker و SMTP واقعی محلی تا اجرای آن‌ها قابل تأیید نیست؛ [STATUS](STATUS.md). روش حفاظت کلیدها و TLS میزبان production باید هنگام تسک deployment تعیین شود. این موارد تصمیم پیاده‌شده تلقی نمی‌شوند.

توسعه آینده AI و فایل از قرارداد مصرف‌شده Application و implementation Infrastructure انجام می‌شود؛ اکنون abstraction یا جدول آن‌ها نداریم. جلسه آینده به snapshot سؤال و معیار نیاز دارد؛ engine نسخه‌بندی اکنون ساخته نمی‌شود.


## پوشه‌بندی پس از بازآرایی

طبق انتخاب صریح کاربر، این تغییر فقط سازمان‌دهی فایل‌هاست؛ مسیر Service → IApplicationDbContext حفظ شده و Repository جدید ساخته نشده است.

```text
src/
  HireYar.Api/
    Controllers/       # هر شش کنترلر
    DTOs/              # پاسخ‌های مخصوص HTTP
    Security/
    Errors/
  HireYar.Application/
    Services/          # Auth, Members, Organizations, JobPositions, InterviewTemplates, Dashboard
    DTOs/              # درخواست/پاسخ و validation بر اساس قابلیت
    Interfaces/        # IAccounts, ITeamMembers, ICurrentUser, IEmailSender, IApplicationDbContext
    Common/            # AppException, Page, PageQuery
  HireYar.Infrastructure/
    Services/          # Auth, Members, Email
    Auth/              # مدل Identity و تنظیمات امنیتی
    Members/           # مدل دعوت
    Persistence/       # DbContext، mapping و migrations
  HireYar.Domain/       # مدل‌های مستقل بر اساس قابلیت
```

ADR-011: پوشه‌ها بر اساس مسئولیت و سپس قابلیت مرتب شدند. namespaceهای موجود برای حفظ هویت typeها ثابت ماندند؛ نام پوشه الزاماً namespace نیست. API routeها، DI و مدل دیتابیس تغییر نکردند. سرویس/DTO/interface اکنون فایل مستقل دارند و AuthService دیگر داخل Contracts.cs نیست.


ADR-012: طبق درخواست کاربر، SQL Server با PostgreSQL و Npgsql EF Core 10.0.3 جایگزین شد؛ داده قبلی نیاز به انتقال ندارد. migration InitialPostgres مبنای دیتابیس خالی است. قفل user/organization در تراکنش Read Committed با SELECT FOR UPDATE تا commit حفظ می‌شود؛ خواندن توکن پس از گرفتن قفل انجام می‌شود. check constraint و partial indexها از شناسه‌های quoted PostgreSQL استفاده می‌کنند؛ FK مرکب و unique email حفظ شده‌اند.

RowVersion از نوع bytea و concurrency token مدیریت‌شده توسط AppDbContext است؛ هر insert/update موقعیت/قالب با Guid تصادفی جدید ۱۶بایتی ذخیره می‌شود. شرط UPDATE شامل نسخه اصلی است؛ stale version پاسخ 409 می‌دهد. استفاده از این روش نوع JSON قبلی Base64 را حفظ می‌کند؛ قرارداد طول از ۸ به ۱۶ بایت تغییر کرده است. تاریخ‌ها timestamptz و UTC هستند. خطای 23505 به conflict و 40P01/40001 به concurrency conflict نگاشت می‌شود.
