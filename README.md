# حسابداری آسان Native v4 — WebView Parity RC1

این نسخه بازنویسی واقعی Windows Desktop با **C# + .NET 8 + WPF + SQLite** است. رابط اصلی WebView/HTML/CSS/JavaScript ندارد؛ اما **ظاهر، RTL، رنگ‌ها، فاصله‌ها و جریان UX از سورس نهایی WebView v3.4 به‌عنوان مرجع مستقیم بازسازی شده‌اند**.

## WebView → Native parity

- پالت Light/Dark بر اساس توکن‌های نهایی `dist/app.css` نسخه v3.4
- Sidebar راست، Topbar 82px، جستجوی سراسری، ساعت سیستم و ساعات کاری
- آیکن‌های Vector و حذف علائم کیبوردی قدیمی
- انیمیشن ورود صفحه 160ms با Fade + TranslateY مطابق WebView
- Hover/Press و Scale فشردن دکمه‌ها
- Dashboard با 5 KPI، نمودار، فروش‌های اخیر و Quick Actions
- فروش/POS با اسکن بارکد، جستجوی حداکثر 10 نتیجه، انتخاب مشتری، پرداخت اجباری نقدی/نسیه، F4 و Ctrl+P
- کالاها، دسته‌ها، مشتریان/شرکت‌ها، هزینه‌ها، انبار، گزارش‌ها، آرشیو، داده‌ها و تنظیمات با ساختار Native
- نشان «نسخه رایگان» و Footer برند با لینک تلگرام سازنده
- فرم‌های Native برای کالا، مشتری/شرکت، دسته، هزینه، خرید، اصلاح موجودی، تسویه قرض و ویرایش/ابطال فاکتور
- Dark/Light زنده با ResourceDictionary

## قابلیت‌های حسابداری و سخت‌افزار

- فروش Transactional: فاکتور + اقلام + موجودی + کاردکس + قرض در یک Transaction
- خرید نقدی/نسیه و ورود موجودی
- واحد پایه/واحد خرید، تبدیل بسته/کارتن و میانگین موزون قیمت خرید
- چند بارکد برای هر کالا (تا 15 در UI) و جلوگیری از بارکد تکراری
- ویرایش و ابطال امن فاکتور با Revision/Audit و برگشت موجودی/قرض
- Raw Input برای Barcode Scanner USB/HID
- چاپ Native فاکتور 80mm از Windows Print Driver، بدون Auto Cut
- Backup/Restore با `PRAGMA quick_check` و Backup ایمنی قبل از Restore/Reset
- خروج با تأیید و Backup ایمن
- First Run setup، رمز مدیر و Data Support محافظت‌شده
- مهاجرت داده‌های WebView v3.4 به Native شامل کالا، بارکد، تصویر، مشتری/شرکت، فاکتور، خرید، هزینه، دریافت/پرداخت، دوره مالی و تنظیمات

## گزارش‌های Parity

- دوره‌های امروز، دیروز، 7 روز، ماه جاری، ماه قبل، دوره مالی جاری، همه اطلاعات و بازه سفارشی
- KPI فروش، هزینه، نسیه، پرداخت و سود خالص
- نمودار Sales / Expenses / Debt / Payments / Net Profit با Toggle و محور صفر برای سود منفی
- جمع‌بندی فروش، ورود/خروج نقدی، خالص نقدی، بهای تمام‌شده، خرید، تخفیف، سود فروش و سود خالص
- وضعیت قرض مشتری، قرض شرکت، ارزش انبار و خالص نقدی ماه جاری
- خروجی CSV سازگار با Excel
- دوره حسابی 6 ماهه و شروع دوره جدید بدون حذف تاریخچه
- آرشیو فاکتورها با تعداد اقلام، چاپ، ویرایش و ابطال

## تست‌های انجام‌شده در این محیط

- 29 فایل XAML: XML معتبر
- x:Class ↔ code-behind: بدون خطای یافت‌شده
- Event handlerها: بدون handler گمشده در بررسی استاتیک
- 100 Resource key و 1114 Resource reference: بدون Resource گمشده
- تست فرمول‌های گزارش نقدینگی/سود: PASS
- تست Reset داده‌ها: PASS
- `PRAGMA quick_check = ok`
- Stress test قبلی: 5,000 کالا، 12,000 فاکتور، 36,000 ردیف فاکتور، 3,500 هزینه و 2,501 خرید: PASS

گزارش RC1 در `HesabdariAsan.Native/RC1_VALIDATION_REPORT.json` است.

## Build Gate

محیط این گفتگو Windows/.NET WPF ندارد؛ بنابراین **تأیید Compile واقعی فقط در GitHub Actions** انجام می‌شود. Workflow موجود Repository به‌ترتیب `dotnet build`، Publish win-x64، Startup smoke-test، ساخت Inno Setup و Startup smoke-test نسخه نصب‌شده را اجرا می‌کند. تا زمانی که این Workflow سبز نشود، این شاخه «RC1» است و نه Production Final.

Version: **4.0.0-rc.1**
