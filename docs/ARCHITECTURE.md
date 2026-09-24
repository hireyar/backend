# معماری و تصمیم‌ها

## ساختار واقعی

`HireYar.slnx` شامل Domain (Organizations/JobPositions/InterviewTemplates)، Application (سرویس‌ها، DTO، validation و قراردادها)، Infrastructure (Identity، SQL/DbContext/mapping/migrations، JWT، SMTP، دعوت) و Api (Controller، HTTP، مجوزها، CSRF، composition) و `tests/HireYar.Tests` است.

وابستگی: Application → Domain؛ Infrastructure → Application/Domain؛ Api → Application و Infrastructure. مسیر درخواست Controller → Application Service → DbContext abstraction یا مرز Identity/SMTP است. سرویس Auth مرز Identity را فراخوانی می‌کند؛ UserManager و تراکنش‌های وابسته به آن در Infrastructure می‌مانند. HttpContext فقط در Api است.

## تصمیم‌های پذیرفته‌شده

| شناسه | تصمیم، دلیل و پیامد |
|---|---|
| ADR-001 | .NET 10، EF Core 10، SQL Server و Controller؛ یک برنامه و یک دیتابیس برای سادگی عملیاتی |
| ADR-002 | Clean Architecture سبک؛ EF DbSet در IApplicationDbContext مجاز است تا repository عمومی نسازیم؛ Application به EF وابسته می‌شود ولی به SQL provider نه |
| ADR-003 | Identity فقط Infrastructure؛ یک کاربر/سازمان و نقش enum محدود Owner/Recruiter؛ unique index ایمیل نرمال و Owner سازمان |
| ADR-004 | JWT با HS256، issuer/audience/expiry/jti؛ وضعیت و نقش در هر درخواست از DB دوباره خوانده می‌شود؛ هزینه یک query در مقابل اثر فوری مجوز |
| ADR-005 | refresh تصادفی ۲۵۶بیتی، ذخیره SHA-256، یک family در هر login؛ تمام mutationهای نشست زیر قفل UPDLOCK/HOLDLOCK کاربر و تراکنش؛ replay خانواده همان نشست را باطل می‌کند |
| ADR-006 | cookie host-only محدود به auth و CSRF استاندارد ASP.NET؛ SameSite Strict برای deployment زیر یک سایت؛ SPA توکن CSRF را با هویت فعلی تازه می‌کند |
| ADR-007 | queryها صریحاً OrganizationId سرور را شرط می‌کنند؛ query filter عمومی نداریم. کلید مرکب JobPosition/Organization در FK قالب از اتصال بین سازمان‌ها در DB جلوگیری می‌کند |
| ADR-008 | RowVersion بومی SQL و 409؛ ویرایش parent و جایگزینی سؤال‌ها تراکنشی، شناسه سؤال‌های باقی‌مانده حفظ می‌شود؛ delete/insert داخلی سؤال امکان جابه‌جایی ترتیب زیر unique index می‌دهد |
| ADR-009 | دعوت Recruiter با hash و انقضای سه‌روزه؛ قفل سازمان عملیات دعوت را سریالی می‌کند؛ unique index رقابت ایمیل و دعوت باز را نیز پوشش می‌دهد |
| ADR-010 | بدون MediatR/CQRS/Generic Repository/worker/OTP؛ توسعه آینده فقط با تسک مستقل |

همه Guid و تاریخ‌ها DateTimeOffset UTC هستند. cascade delete تمام روابط Restrict است. enumهای وضعیت و نقش در SQL به متن محدود و check constraint ذخیره می‌شوند. DTOها اجازه overposting ندارند و Entity مستقیم برنمی‌گردد. pagination پیش‌فرض ۲۰، سقف ۱۰۰ و ترتیب CreatedAtUtc سپس Id است.

حدود متن: نام شخص/سازمان/عنوان ۲۰۰، ایمیل ۲۵۶، شرح موقعیت ۱۰۰۰۰، seniority صد، language بیست، سؤال ۲۰۰۰، competency دویست، راهنمای ارزیابی ۴۰۰۰. رمز ۱۲ تا ۱۲۸ و الزامات استاندارد Identity. قالب ۱ تا ۳۰ سؤال، ترتیب پیوسته از ۱، زمان ۵ تا ۶۰ و follow-up صفر تا سه.

Identity token provider استاندارد با اعتبار دو ساعت و Data Protection دائمی استفاده می‌شود. تأیید دوباره ایمیل با flag و قفل رد می‌شود؛ reset با security stamp یک‌بارمصرف است. ساخت سازمان و Owner تراکنشی؛ ارسال ایمیل پس از commit است. شکست SMTP داده ناقص نمی‌سازد و resend ممکن است. forgot/resend همیشه پاسخ عمومی یکسان دارند.

ProblemDetails/ValidationProblemDetails با code و traceId؛ ۴۰۰ validation، ۴۰۱ authentication، ۴۰۳ role، ۴۰۴ رکورد خارج scope، ۴۰۹ conflict، ۴۲۹ rate limit، ۵۰۰ خطای عمومی. لاگ درخواست حساس یا body/token فعال نیست؛ رخداد ارسال ناموفق بدون recipient/link ثبت می‌شود.

موضوع باز محیطی: صحت Docker و SMTP واقعی محلی تا اجرای آن‌ها قابل تأیید نیست؛ [STATUS](STATUS.md). روش حفاظت کلیدها و TLS میزبان production باید هنگام تسک deployment تعیین شود. این موارد تصمیم پیاده‌شده تلقی نمی‌شوند.

توسعه آینده AI و فایل از قرارداد مصرف‌شده Application و implementation Infrastructure انجام می‌شود؛ اکنون abstraction یا جدول آن‌ها نداریم. جلسه آینده به snapshot سؤال و معیار نیاز دارد؛ engine نسخه‌بندی اکنون ساخته نمی‌شود.
