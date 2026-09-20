# حسابداری آسان Native v4.1 — Performance RC1

این شاخه، نسخه‌ی واقعی Windows Desktop با **C# + .NET 8 + WPF + SQLite** است. WebView/HTML/CSS/JavaScript موتور اصلی برنامه نیست؛ ظاهر و UX نسخه‌ی WebView v3.4 فقط به‌عنوان مرجع بازسازی Native استفاده شده است.

## ارتقاهای v4.1 نسبت به RC2

- Schema دیتابیس از v6 به **v7** ارتقا یافت.
- SQLite با `Pooling=True`، Shared Cache، `temp_store=MEMORY`، cache بزرگ‌تر و `mmap_size` تنظیم شده است.
- ایندکس‌های ترکیبی جدید برای آرشیو فاکتور، وضعیت فاکتور، مشتری، کالا، کاردکس، خرید، هزینه و Audit اضافه شد.
- مسیر **Barcode Lookup** از شرط سنگین `OR/EXISTS` به Join مستقیم روی `product_barcodes.barcode` تغییر کرد.
- جست‌وجوی سراسری، صفحات لیستی و POS دارای **Debounce** شدند تا با هر کلید یک Query کامل اجرا نشود.
- Data Support اکنون حجم DB/WAL، Schema، تعداد Index و شمار رکوردهای اصلی را نشان می‌دهد.
- دکمه‌ی **بهینه‌سازی دیتابیس** برای `PRAGMA optimize` + WAL checkpoint اضافه شد.
- نسخه‌ی Assembly/Installer به `4.1.0-rc.1` ارتقا یافت.

## قابلیت‌های Native موجود

- Dashboard، فروش/POS، کالاها، دسته‌ها، مشتریان/شرکت‌ها، خرید، هزینه، انبار، گزارش‌ها، آرشیو، Audit، تنظیمات و Data Support.
- فروش Transactional: فاکتور + اقلام + موجودی + کاردکس + قرض در یک Transaction.
- Raw Input برای Barcode Scanner USB/HID.
- چند بارکد برای هر کالا، واحد پایه/خرید و تبدیل بسته/کارتن.
- چاپ Native فاکتور 80mm از Windows Print Driver.
- ویرایش/ابطال امن فاکتور با Revision/Audit و برگشت موجودی/قرض.
- Backup/Restore، quick_check، Backup ایمنی قبل از Restore/Reset و مهاجرت v3.4.
- Dark/Light، RTL، انیمیشن صفحه، Smooth Scroll و دیالوگ‌های Native.

## تست و Benchmark این بسته

- 30 فایل XAML: XML معتبر.
- x:Class ↔ code-behind: بدون خطای یافت‌شده در اعتبارسنجی استاتیک.
- Event handlerها: بدون handler گمشده.
- 101 Resource key و 1147 reference: بدون Resource گمشده.
- تست فرمول‌های حسابداری: PASS.
- تست Reset داده‌ها: PASS.
- `PRAGMA quick_check = ok`.
- Benchmark SQLite با **100,000 کالا + 1,000,000 فاکتور + 3,000,000 ردیف فاکتور**:
  - Barcode lookup قدیمی: median حدود **50.33 ms**.
  - Barcode lookup ایندکس‌شده v4.1: median حدود **0.0044 ms**.
  - آرشیو صفحه اول 50 رکورد: median حدود **0.039 ms**.
  - آرشیو عمیق نزدیک Offset 900,000: median حدود **28.49 ms**.

گزارش‌ها:
- `HesabdariAsan.Native/RC1_VALIDATION_REPORT.json`
- `HesabdariAsan.Native/PERFORMANCE_BENCHMARK_V41.json`

## Build Gate

در این محیط Linux، .NET/WPF Windows SDK نصب نیست؛ بنابراین Compile واقعی WPF اینجا قابل تأیید نیست. Workflow موجود GitHub Actions روی `windows-latest` به‌ترتیب Restore، Build، Publish win-x64، Startup smoke-test، ساخت Setup و Startup smoke-test نسخه نصب‌شده را اجرا می‌کند. تا سبز شدن Workflow، این بسته **Performance RC1** است، نه Production Final.

Version: **4.1.0-rc.1**
