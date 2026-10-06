namespace HireYar.Api.OpenApi;

internal static class OpenApiText
{
    public const string Introduction = """
        **ورود با موبایل:** `GET /api/auth/csrf` ← `POST /api/auth/otp/request` ← دریافت پیامک ← `POST /api/auth/otp/verify`. اگر موبایل حسابی نداشته باشد، حساب Owner و سازمان اولیه ساخته می‌شود؛ نام سازمان را با `PUT /api/organization` تکمیل کنید. API key و نام قالب VerifyLookup کاوه‌نگار باید در تنظیمات محرمانه محیط باشند.

        **هر سازمان یک حساب مالک دارد؛ ورود فقط با موبایل و OTP است.**

        برای درخواست‌های غیر GET در `/api/auth`، مقدار `token` از `GET /api/auth/csrf` را در هدر `X-CSRF-TOKEN` بفرستید و کوکی همان درخواست را نگه دارید. پس از ورود، CSRF را با Bearer جدید دوباره بگیرید. برای تمدید نشست، Bearer را حذف کنید و با کوکی refresh، ابتدا CSRF و سپس `POST /api/auth/refresh` را بزنید.

        پس از ورود: موقعیت شغلی بسازید، سپس قالب مصاحبهٔ مربوط به آن را ثبت کنید.
        **ویزارد پنل:** انتخاب موقعیت با `GET /api/job-positions` یا ساخت با `POST /api/panel-wizard/positions`؛ انتخاب قالب با `GET /api/interview-templates` یا ساخت با `POST /api/panel-wizard/templates`؛ ثبت کارجو و رزومه با `POST /api/panel-wizard/invitations` (multipart). توکن دعوت محرمانه است. ارسال ایمیل و پیامک فعلاً `NotConfigured` است؛ صدور توکن را ارسال پیام نمایش ندهید.
        """;

    public static string? Summary(string? controller, string? action) => (controller, action) switch
    {
        ("Auth", "Csrf") => "۱. دریافت توکن CSRF",
        ("Auth", "RequestOtp") => "۲. ارسال کد ورود به موبایل",
        ("Auth", "VerifyOtp") => "۳. تأیید کد و ورود یا ثبت‌نام",
        ("Auth", "Me") => "۵. مشاهده حساب جاری",
        ("Auth", "Update") => "ویرایش نام حساب جاری",
        ("Auth", "Refresh") => "تمدید نشست با کوکی refresh",
        ("Auth", "Logout") => "خروج از نشست جاری",
        ("Auth", "LogoutAll") => "خروج از همه نشست‌ها",
        ("Organization", "Get") => "مشاهده سازمان جاری",
        ("Organization", "Update") => "ویرایش سازمان؛ فقط مالک",
        ("JobPositions", "List") => "فهرست موقعیت‌های شغلی",
        ("JobPositions", "Get") => "جزئیات موقعیت شغلی",
        ("JobPositions", "Create") => "ساخت موقعیت شغلی",
        ("JobPositions", "Update") => "ویرایش موقعیت شغلی",
        ("JobPositions", "Delete") => "حذف موقعیت و قالب‌های وابسته از پنل؛ حفظ سوابق مصاحبه",
        ("InterviewTemplates", "List") => "فهرست قالب‌های مصاحبه",
        ("InterviewTemplates", "Get") => "جزئیات قالب و سؤال‌ها",
        ("InterviewTemplates", "Create") => "ساخت قالب برای موقعیت شغلی",
        ("InterviewTemplates", "Update") => "ویرایش قالب و سؤال‌ها",
        ("InterviewTemplates", "Delete") => "حذف قالب از پنل؛ حفظ سوابق مصاحبه",
        ("Dashboard", "Get") => "آمار سازمان جاری",
        ("PanelWizard", "CreatePosition") => "ویزارد موقعیت: ثبت عنوان و شرح شغل",
        ("PanelWizard", "UpdatePosition") => "ویزارد موقعیت: ویرایش عنوان و شرح شغل",
        ("PanelWizard", "CreateTemplate") => "ویزارد قالب: ثبت نوع، سؤال‌ها و پیام‌ها",
        ("PanelWizard", "UpdateTemplate") => "ویزارد قالب: ویرایش سؤال‌ها و پیام‌ها",
        ("QuestionSuggestions", "Generate") => "پیشرفته: پیشنهاد موضوع‌ها و سه سطح سؤال بر اساس شرح شغل؛ بدون ذخیره",
        ("QuestionSuggestions", "RegenerateTopic") => "پیشرفته: بازسازی سؤال‌های یک موضوع؛ بدون تغییر سایر موضوع‌ها",
        ("PanelWizard", "Invite") => "ویزارد دعوت: ثبت کارجو، رزومه و صدور دعوت",
        ("Interviews", "List") => "فهرست و جستجوی مصاحبه‌ها با فیلتر وضعیت و تاریخ",
        ("Interviews", "Resend") => "صدور مجدد دعوت و ابطال توکن قبلی",
        ("Interviews", "Cancel") => "لغو مصاحبه و دسترسی داوطلب",
        ("Interviews", "Delete") => "حذف مصاحبه از پنل، ابطال دسترسی و آزادسازی رزرو اعتبار",
        ("Interviews", "Resume") => "دانلود خصوصی رزومه کارجو",
        ("Interviews", "CreateApplication") => "ثبت داوطلب برای موقعیت",
        ("Interviews", "CreateSkill") => "تعریف مهارت سازمان",
        ("Interviews", "SetSkill") => "تنظیم مهارت موقعیت",
        ("Interviews", "Questions") => "بانک سؤال موقعیت",
        ("Interviews", "CreateQuestion") => "افزودن سؤال به بانک",
        ("Interviews", "UpdateQuestion") => "ویرایش سؤال بانک",
        ("Interviews", "Invite") => "ساخت جلسه و توکن دعوت داوطلب",
        ("Interviews", "Get") => "مشاهده شواهد و نتیجه مصاحبه",
        ("Interviews", "Revoke") => "ابطال دسترسی داوطلب",
        ("Interviews", "AnswerAudio") => "دریافت خصوصی صوت پاسخ داوطلب",
        ("Interviews", "QuestionAudio") => "دریافت خصوصی صوت سؤال مصاحبه",
        ("CandidateInterviews", "Start") => "شروع مصاحبه با توکن داوطلب",
        ("CandidateInterviews", "Current") => "وضعیت جاری مصاحبه داوطلب",
        ("CandidateInterviews", "Answer") => "ثبت و پردازش پاسخ صوتی",
        ("CandidateInterviews", "Retry") => "پردازش دوباره همان پاسخ",
        ("CandidateInterviews", "Complete") => "پایان مصاحبه به درخواست داوطلب",
        ("CandidateInterviews", "Audio") => "دریافت خصوصی صوت سؤال",
        ("Pricing", _) => "مشاهده قیمت‌ها و پکیج‌های عمومی",
        ("Billing", "Panel") => "وضعیت اشتراک، اعتبار و زمان قابل استفاده سازمان",
        ("Billing", "Quote") => "خلاصه خرید، تخفیف، مالیات و اعتبار روزشمار ارتقا",
        ("Billing", "Checkout") => "ایجاد پرداخت خرید، تمدید یا ارتقای اشتراک؛ فقط مالک",
        ("Billing", "FinancialHistory") => "سوابق مالی با فیلتر و جمع بازه",
        ("Billing", "Invoice") => "جزئیات فاکتور پرداخت موفق سازمان",
        ("Billing", _) => "اشتراک، اعتبار و پرداخت سازمان جاری",
        ("AdminPricing", _) => "مدیریت قیمت‌گذاری؛ فقط مدیر پلتفرم",
        ("PaymentCallbacks", _) => "تأیید پرداخت از درگاه؛ بدون پذیرش قیمت یا موفقیت از درخواست",
        _ => null
    };
}
