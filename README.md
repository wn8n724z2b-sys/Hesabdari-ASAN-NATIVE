# حسابداری آسان Native v4.1 Ultra

نسخه Native واقعی Windows با C# / .NET 8 / WPF / SQLite. رابط WebView فقط به‌عنوان مرجع ظاهری و جریان UX استفاده شده و اجرای اصلی HTML/JS ندارد.

## تغییرات اصلی v4.1
- رفع Binding نتایج جستجوی فروش و جستجوی سراسری داخل WPF Popup.
- اسکرول نرم interruption-safe با پشتیبانی بهتر از wheel/trackpad و ScrollViewerهای تو در تو.
- Transition صفحه با Fade + Translate + Scale بسیار سبک و Render-only.
- Popupهای جستجو و انتخاب مشتری با reveal نرم.
- Virtualization/Recycling برای ListBox و Virtualization موجود DataGrid برای دیتاست‌های بزرگ.
- دسته‌بندی: ذخیره با Enter، لغو با Escape، انتقال کالاها به «عمومی» هنگام حذف.
- Backup دستی: ساخت SQLite واقعی، کپی به مسیر کاربر و Validate مجدد فایل خروجی.
- گزارش‌ها: فروش/هزینه/نسیه/پرداخت/سود، روند روزانه، دوره مالی، CSV و حذف فاکتورهای باطل از محاسبات جاری.
- فروش Transactional، کاردکس موجودی، چندبارکد، خرید، مشتری/شرکت، چاپ 80mm، Audit و Restore ایمن.

## محدودیت 100 فایل GitHub
این Repository دقیقاً برای Upload وب GitHub فشرده شده است. Models در `Models/Models.cs` و code-behind صفحه‌های ساده در `Views/Views.CodeBehind.cs` ادغام شده‌اند. Workflow قبل از Restore تعداد فایل‌ها را کنترل می‌کند و اگر از 100 بیشتر شود Build را متوقف می‌کند.

## Build
فولدرها را در ریشه Repository آپلود کنید و GitHub Actions را اجرا کنید. Workflow روی Windows این مراحل را اجرا می‌کند: Restore، Compile، Publish win-x64 self-contained، Startup smoke test، ساخت Setup با Inno Setup و smoke test نسخه نصب‌شده.

نسخه: 4.1.0
