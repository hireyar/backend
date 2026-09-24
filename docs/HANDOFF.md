# ادامه کار

2026-09-24: مرحله اول HireYar در `D:\Projects\viravss\HireYar` روی `dev` پیاده شد. برنچ‌های `dev` و `production` از commit اولیه ساخته شدند؛ main حفظ شد. ریپوی ChiBazIR دست‌نخورده ماند.

نقطه شروع: [AGENTS](../AGENTS.md)، سپس [STATUS](STATUS.md) برای نتایج واقعی و [ARCHITECTURE](ARCHITECTURE.md) برای تصمیم‌ها. کد حساس: Accounts، TeamMembers، AppDbContext/Configurations، CurrentAccountValidation، Program و تست‌های AuthTests/PanelTests/SecurityTests.

۲۴ تست integration در Debug و سپس Release روی دیتابیس‌های تصادفی محلی SQL Server پاس شدند؛ migration و SQL script ساخته شدند و اختلاف مدل وجود ندارد. دیتابیس‌های تست پس از اجرا حذف شدند. تست SMTP از جایگزین حافظه‌ای استفاده می‌کند؛ صحت اتصال SMTP واقعی و Docker هنوز تأیید نشده است. نتیجه قبلی را جای بررسی کد و اجرای تست پس از تغییر نگذار.

قدم بعدی HY-006: Docker Linux engine را در محیط توسعه در دسترس قرار بده، compose را با secrets محلی بالا بیاور و دریافت تأیید/بازیابی/دعوت در Mailpit را بررسی کن. سپس HY-007 برای مرورگر و نمونه HTTP. جزئیات و اولویت‌ها در [BACKLOG](BACKLOG.md) است. توسعه AI/جلسه یا Angular مجوز این تسک نیست.

تمام فایل‌های پیاده‌سازی و مستندات هنوز commit نشده‌اند؛ README جایگزین متن اولیه GitLab شده است. هیچ push/deploy/تغییر production انجام نشده. `production` هنوز نسخه محصول ندارد. گزارش TRX و SQL script در TestResults و artifacts خارج Git هستند.

نکته محیطی: SDK 10.0.400 و SQL Server محلی موجود است؛ Docker daemon در بررسی در دسترس نبود. برای تست `HIREYAR_TEST_SQL` و برای ابزار EF `ConnectionStrings__Default` لازم است؛ هیچ secret در ریپو قرار ندارد. workspace اولیه Codex به ChiBazIR متصل بود، بنابراین فرمان‌ها با workdir صریح HireYar اجرا شدند؛ دسترسی نوشتن این مسیر در sandbox ممکن است نیازمند مجوز محیط باشد.

`2026-09-24`، رفع خطای اجرای محلی: SigningKey تولید و فقط در User Secrets ذخیره شد؛ اتصال توسعه محلی نیز در همان محل تنظیم شد. Startup و health تأیید شدند؛ API موقت متوقف شد. این بررسی وجود schema دیتابیس توسعه یا SMTP را تأیید نمی‌کند.
آخرین اصلاح: 401 در /swagger ناشی از ترتیب middleware بود؛ UseSwaggerUI فقط در Development قبل از authorization منتقل شد. ۲ تست منتخب Release شامل UI assets و محدودیت Production پاس شدند. اجرای قبلی کاربر دست‌نخورده است و برای دریافت اصلاح نیاز به rebuild/restart دارد.
