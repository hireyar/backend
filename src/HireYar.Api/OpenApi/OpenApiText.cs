namespace HireYar.Api.OpenApi;

internal static class OpenApiText
{
    public const string Introduction = """
        **ورود با موبایل:** `GET /api/auth/csrf` ← `POST /api/auth/otp/request` ← دریافت پیامک ← `POST /api/auth/otp/verify`. اگر موبایل حسابی نداشته باشد، حساب Owner و سازمان اولیه ساخته می‌شود؛ نام سازمان را با `PUT /api/organization` تکمیل کنید. API key و نام قالب VerifyLookup کاوه‌نگار باید در تنظیمات محرمانه محیط باشند.

        مسیرهای زیر برای حساب‌های ایمیلی موجود تا تعیین تکلیف مهاجرت آن‌ها باقی مانده‌اند.

        **شروع با حساب جدید:** `GET /api/auth/csrf` ← `POST /api/auth/register` ← `POST /api/auth/confirm-email` ← `POST /api/auth/login`.

        **حساب موجود:** `GET /api/auth/csrf` ← `POST /api/auth/login` ← توکن دریافتی را در **Authorize** وارد کنید ← `GET /api/auth/me`.

        برای درخواست‌های غیر GET در `/api/auth`، مقدار `token` از `GET /api/auth/csrf` را در هدر `X-CSRF-TOKEN` بفرستید و کوکی همان درخواست را نگه دارید. پس از ورود، CSRF را با Bearer جدید دوباره بگیرید. برای تمدید نشست، Bearer را حذف کنید و با کوکی refresh، ابتدا CSRF و سپس `POST /api/auth/refresh` را بزنید.

        پس از ورود: موقعیت شغلی بسازید، سپس قالب مصاحبهٔ مربوط به آن را ثبت کنید.
        """;

    public static string? Summary(string? controller, string? action) => (controller, action) switch
    {
        ("Auth", "Csrf") => "۱. دریافت توکن CSRF",
        ("Auth", "RequestOtp") => "۲. ارسال کد ورود به موبایل",
        ("Auth", "VerifyOtp") => "۳. تأیید کد و ورود یا ثبت‌نام",
        ("Auth", "Register") => "۲. ثبت‌نام سازمان و مالک",
        ("Auth", "Confirm") => "۳. تأیید ایمیل",
        ("Auth", "Resend") => "ارسال دوباره ایمیل تأیید",
        ("Auth", "Login") => "۴. ورود و دریافت توکن دسترسی",
        ("Auth", "Me") => "۵. مشاهده حساب جاری",
        ("Auth", "Update") => "ویرایش نام حساب جاری",
        ("Auth", "Refresh") => "تمدید نشست با کوکی refresh",
        ("Auth", "Logout") => "خروج از نشست جاری",
        ("Auth", "LogoutAll") => "خروج از همه نشست‌ها",
        ("Auth", "Forgot") => "درخواست بازیابی رمز",
        ("Auth", "Reset") => "ثبت رمز جدید",
        ("Auth", "Change") => "تغییر رمز حساب جاری",
        ("Auth", "Accept") => "پذیرش دعوت عضو تیم",
        ("Organization", "Get") => "مشاهده سازمان جاری",
        ("Organization", "Update") => "ویرایش سازمان؛ فقط مالک",
        ("Members", "List") => "فهرست اعضا؛ فقط مالک",
        ("Members", "Invitations") => "فهرست دعوت‌ها؛ فقط مالک",
        ("Members", "Invite") => "دعوت عضو جدید؛ فقط مالک",
        ("Members", "Resend") => "ارسال دوباره دعوت؛ فقط مالک",
        ("Members", "Cancel") => "لغو دعوت؛ فقط مالک",
        ("Members", "Active") => "فعال یا غیرفعال‌کردن عضو؛ فقط مالک",
        ("JobPositions", "List") => "فهرست موقعیت‌های شغلی",
        ("JobPositions", "Get") => "جزئیات موقعیت شغلی",
        ("JobPositions", "Create") => "ساخت موقعیت شغلی",
        ("JobPositions", "Update") => "ویرایش موقعیت شغلی",
        ("InterviewTemplates", "List") => "فهرست قالب‌های مصاحبه",
        ("InterviewTemplates", "Get") => "جزئیات قالب و سؤال‌ها",
        ("InterviewTemplates", "Create") => "ساخت قالب برای موقعیت شغلی",
        ("InterviewTemplates", "Update") => "ویرایش قالب و سؤال‌ها",
        ("Dashboard", "Get") => "آمار سازمان جاری",
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
        _ => null
    };
}
