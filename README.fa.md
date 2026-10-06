<!-- (c) DeZ4p | t.me/DeZ4p | All Rights Reserved -->

**Language / زبان:** &nbsp;
[English](README.md) &nbsp;|&nbsp; [فارسی](README.fa.md)

---

<div align="center">

# DeZ4p Android Manager

**جعبه‌ابزار کامل ویندوز برای مدیریت دستگاه‌های اندروید**

**ADB - Fastboot - فلش - مانیتور - مدیریت فایل - همه چیز**

[![Build](https://github.com/DeZ4p/DeZ4pAndroidManager/actions/workflows/build.yml/badge.svg)](https://github.com/DeZ4p/DeZ4pAndroidManager/actions/workflows/build.yml)
[![Release](https://img.shields.io/github/v/release/DeZ4p/DeZ4pAndroidManager?include_prereleases&color=4F8CFF)](https://github.com/DeZ4p/DeZ4pAndroidManager/releases)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%2B-0078D6)](#نیازمندی‌های-سیستم)
[![Android](https://img.shields.io/badge/Android-7%20→%2016-3DDC84)](#نیازمندی‌های-سیستم)

**ساخته شده توسط [DeZ4p](https://t.me/DeZ4p) - t.me/DeZ4p**

</div>

---

## فهرست مطالب

- [این برنامه چیه؟](#این-برنامه-چیه)
- [قابلیت‌ها](#قابلیت‌ها)
- [دانلود](#دانلود)
- [اولین استفاده](#اولین-استفاده)
- [نیازمندی‌های سیستم](#نیازمندی‌های-سیستم)
- [تست‌شده روی](#تست‌شده-روی)
- [ساخت از سورس](#ساخت-از-سورس)
- [ساختار پروژه](#ساختار-پروژه)
- [تکنولوژی‌ها](#تکنولوژی‌ها)
- [گزارش باگ](#گزارش-باگ)
- [لایسنس](#لایسنس)

---

## این برنامه چیه؟

**DeZ4p Android Manager** یک برنامه‌ی حرفه‌ای ویندوزی است که **کنترل کامل روی هر دستگاه اندرویدی** رو در یک رابط کاربری تمیز و سریع به شما می‌ده.

تمام کارهای روزمره‌ی یک کاربر حرفه‌ای اندروید رو در یک برنامه جمع کرده:

- **مانیتور زنده** باتری، رم، CPU، فضا، شبکه
- **مدیریت کامل فایل** (مرور، ویرایش، آپلود، دانلود، کپی، تغییر نام)
- **نصب APK** (تک‌فایل و Split)
- **مدیریت برنامه‌ها** (لیست، پشتیبان، حذف)
- **عملیات بوت‌لودر / Fastboot / ریکاوری**
- **مدیریت پارتیشن** و پشتیبان‌گیری
- **آینه‌سازی صفحه** با scrcpy داخلی
- **Logcat، کنسول شل، مدیریت پردازش، سنسورها**
- **مرکز امنیت** (SELinux، روت، Verified Boot)
- **ADB بی‌سیم** (اتصال از طریق Wi-Fi بدون کابل)

و خیلی چیزهای دیگه — همه **دوزبانه (انگلیسی + فارسی)** با **پشتیبانی کامل RTL**، **تم تیره و روشن**، و **بدون نیاز به نصب چیزی** روی سیستم کاربر.

---

## قابلیت‌ها

### 📊 مانیتور و اطلاعات

- **داشبورد زنده** — باتری، سلامت، دما، ولتاژ، فضا، رم، CPU، سرعت شبکه، زمان روشن بودن
- **امتیاز سلامت** — نمره‌ی کلی دستگاه بر اساس چند فاکتور
- **برنامه‌های پرمصرف** — لیست زنده بر اساس CPU و RAM
- **آزمایشگاه باتری** — تعداد چرخه، ظرفیت طراحی، ظرفیت فعلی، سلامت، سن
- **ناحیه‌های حرارتی** — CPU، GPU، باتری، سنسورهای سطح
- **تحلیل فضا** — تفکیک فضای دستگاه بر اساس دسته
- **پنل سنسورها** — همه‌ی سنسورهای سخت‌افزاری با مقادیر زنده
- **اطلاعات دستگاه** — بیش از ۶۰ فیلد: برند، مدل، کدنام، نسخه اندروید، SDK، پچ امنیتی، CPU، GPU، رم، فضا، رزولوشن، تراکم، نرخ رفرش، وای‌فای، بلوتوث، SIM، بیس‌بند، کرنل، بوت‌لودر، روت، SELinux، Verified Boot

### 📁 مدیریت فایل و برنامه

- **مدیریت فایل** — مرور، دانلود، آپلود، تغییر نام، حذف، ساخت پوشه، انتخاب چندگانه
- **کپی / برش / پیست** روی دستگاه
- **نصب‌کننده APK** — تک‌فایل `.apk` و Split `.apks` (تشخیص خودکار با `install-multiple`)
- **مدیریت برنامه‌ها** — لیست اپ‌های نصب‌شده، اطلاعات APK، حذف نصب
- **پشتیبان APK** — استخراج APK از دستگاه
- **ابزار Split APK** — تجزیه، تحلیل، دوباره‌امضا کردن
- **پشتیبان‌گیری و بازگردانی** — پشتیبان از اپ‌ها و داده‌ها
- **گالری رسانه** — مرور عکس، ویدیو، صدا
- **مخاطبین و پیام‌ها** — خواندن و پشتیبان‌گیری

### 🔥 فلش و بوت‌لودر

- **ابزار فلش** — فلش پارتیشن‌های boot، recovery، system، vendor
- **ابزار بوت‌لودر** — Unlock/Lock، دریافت اطلاعات، دستورات OEM
- **مدیریت ریکاوری** — ری‌استارت به ریکاوری، sideload، ADB در ریکاوری
- **ابزار پارتیشن** — لیست پارتیشن‌ها، سایز، نوع، پشتیبان از پارتیشن تکی
- **گزینه‌های ری‌استارت** — معمولی، ریکاوری، بوت‌لودر، Fastbootd، EDL، خاموش، ری‌استارت رابط کاربری

### 🛠 پیشرفته

- **کنسول ADB** — `adb shell` تعاملی با ضبط خروجی
- **کنسول Fastboot** — اجرای دستورات `fastboot`
- **نمایشگر Logcat** — لاگ‌های زنده با فیلتر
- **مدیریت پردازش** — پردازش‌های در حال اجرا با CPU، RAM، PID
- **ابزار شبکه** — وای‌فای، شبکه موبایل، قدرت سیگنال
- **اجرای اسکریپت** — اجرای اسکریپت‌های ADB/Shell
- **زمان‌بند کارها** — زمان‌بندی کارهای ADB خودکار

### 🛡 امنیت و حریم خصوصی

- **مرکز امنیت** — وضعیت SELinux، تشخیص روت، Verified Boot
- **مدیریت مجوزها** — مشاهده‌ی مجوزهای اپ‌ها
- **ابزار حریم خصوصی** — ابزارهای بررسی حریم خصوصی
- **ADB بی‌سیم** — اتصال از طریق Wi-Fi (اندروید ۱۱+)

### 🎥 رسانه

- **آینه‌سازی صفحه** — با **scrcpy v5.0** داخلی
- **اسکرین‌شات** — با یک کلیک
- **ضبط صفحه** — ضبط صفحه‌ی دستگاه به MP4

### 🎨 رابط کاربری

- **دوزبانه** — English + فارسی با پشتیبانی کامل RTL
- **تم تیره و روشن** — تشخیص خودکار از ویندوز، تغییر دستی
- **انیمیشن روان** — انتقال‌های ۶۰ FPS
- **DPI aware** — تیز روی ۱۰۰%، ۱۲۵%، ۱۵۰%، ۲۰۰%
- **قفل هوشمند** — قابلیت‌ها در حالت ناسازگار قفل می‌شن
- **Hot-plug** — تشخیص خودکار اتصال/قطع دستگاه
- **لاگ فعالیت** — تاریخچه‌ی پایدار (۵۰۰ رویداد آخر)

### ⚡ کارایی

- **Self-contained** — بدون نیاز به نصب .NET
- **تک‌فایل** — یک فایل `.exe`
- **آفلاین** — کاملاً بدون اینترنت کار می‌کنه
- **استارت سریع** — Splash → پنجره اصلی زیر ۲ ثانیه

---

## دانلود

آخرین نسخه رو از صفحه‌ی [**Releases**](https://github.com/DeZ4p/DeZ4pAndroidManager/releases/latest) بگیرید.

| فایل | توضیح | حجم |
|------|-------|-----|
| `DeZ4pAndroidManager-Setup-x64-1.0.0.exe` | **نصب‌کننده** — توصیه‌شده | ~۹۵ MB |
| `DeZ4pAndroidManager-Portable-x64-1.0.0.zip` | **Portable** — بدون نصب | ~۱۰۳ MB |

**چرا این‌قدر حجم داره؟** چون کاملاً Self-Contained است — شامل runtime .NET 8 (~۷۰ MB) + ابزارهای `adb`، `fastboot`، و `scrcpy`. **نیازی به دانلود اضافه نیست.**

---

## اولین استفاده

### مرحله ۱ — فعال‌سازی USB Debugging روی گوشی

۱. **تنظیمات** → **درباره‌ی گوشی**
۲. روی **شماره ساخت** ۷ بار ضربه بزنید (پیام "You are now a developer" نمایش داده می‌شه)
۳. برگردید → **گزینه‌های توسعه‌دهنده** → **USB Debugging** رو فعال کنید

### مرحله ۲ — اتصال گوشی

۱. گوشی رو با کابل USB به کامپیوتر وصل کنید
۲. روی گوشی، پیام **Allow USB Debugging?** ظاهر می‌شه — **Allow** رو بزنید (اگه می‌خواید "Always allow" رو تیک بزنید)

### مرحله ۳ — اجرای برنامه

۱. `DeZ4pAndroidManager.exe` رو اجرا کنید
۲. Dashboard خودکار باز می‌شه وقتی دستگاه شناسایی شد
۳. از سایدبار بین ابزارها جابه‌جا شید

---

## نیازمندی‌های سیستم

| قطعه | حداقل | توصیه‌شده |
|------|--------|-----------|
| **سیستم‌عامل** | Windows 10 (build 17763 / 1809) | Windows 11 آخرین نسخه |
| **همچنین** | Server 2019, Server 2022 | — |
| **CPU** | x64 (AMD64) | هر x64 مدرن |
| **RAM** | ۴ GB | ۸ GB |
| **فضا** | ۲۵۰ MB | ۵۰۰ MB |
| **.NET** | *لازم نیست* (self-contained) | — |
| **دستگاه اندروید** | اندروید ۷ (API 24) | اندروید ۱۳ تا ۱۶ |
| **ADB** | 1.0.36 | 1.0.41+ |

---

## تست‌شده روی

| دستگاه | برند | اندروید | نتیجه |
|--------|------|---------|-------|
| **Poco X6 Pro** | Xiaomi | HyperOS (Android 14) | ✅ کامل |
| **Poco C71** | Xiaomi | MIUI (Android 13) | ✅ کامل |
| **Huawei P Smart 2019** | Huawei | EMUI 9 (Android 9) | ✅ کامل |
| **Samsung Galaxy J7 Prime 2** | Samsung | One UI (Android 9) | ✅ کامل |

**هدف سازگاری:** اندروید ۷ تا ۱۶ روی تمام برندهای اصلی (Pixel، Samsung، Xiaomi، OnePlus، Oppo، Vivo، Huawei، Asus، Sony، Nothing، Motorola، Nokia، TCL، Tecno، Infinix).

---

## ساخت از سورس

### پیش‌نیازها

- **.NET 8 SDK** یا جدیدتر
- **Inno Setup 6 یا 7** (فقط برای ساخت installer)
- **Git**

### مراحل

```powershell
# ۱. Clone
git clone https://github.com/DeZ4p/DeZ4pAndroidManager.git
cd DeZ4pAndroidManager

# ۲. دانلود ابزارها (adb + fastboot + scrcpy)
cd src\DeZ4pAndroidManager\tools
.\download-tools.ps1
cd ..\..\..

# ۳. Restore
dotnet restore

# ۴. Build (Debug)
dotnet build

# ۵. اجرا
dotnet run --project src\DeZ4pAndroidManager\DeZ4pAndroidManager.csproj

# ۶. ساخت Release (portable)
dotnet publish src\DeZ4pAndroidManager\DeZ4pAndroidManager.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true

# ۷. ساخت Installer (نیاز به Inno Setup)
cd installer
.\build-installer.ps1
```

---

## ساختار پروژه

```
DeZ4pAndroidManager/
├── src/DeZ4pAndroidManager/
│   ├── Controls/         کنترل Sparkline
│   ├── Converters/       Converter های XAML
│   ├── Localization/     Strings.en.xaml + Strings.fa.xaml
│   ├── Models/           ۳۵+ مدل داده
│   ├── Services/         ۴۲ سرویس (ADB، Fastboot، Analytics، ...)
│   ├── Themes/           DarkTheme.xaml + LightTheme.xaml
│   ├── ViewModels/       ۳۷ ViewModel (MVVM)
│   ├── Views/            ۳۸ View XAML
│   ├── tools/
│   │   ├── platform-tools/    adb.exe + fastboot.exe
│   │   └── scrcpy/            scrcpy v5.0
│   ├── App.xaml
│   ├── MainWindow.xaml
│   └── app.manifest
├── installer/
├── .github/workflows/
├── docs/screenshots/
├── LICENSE
└── README.md
```

---

## تکنولوژی‌ها

| لایه | تکنولوژی |
|------|-----------|
| زبان | C# 12 |
| Runtime | .NET 8 |
| UI | WPF (XAML) |
| الگو | MVVM + Dependency Injection |
| DI | Microsoft.Extensions.DependencyInjection 8.0 |
| ADB | AdvancedSharpAdbClient 3.6.16 |
| پردازش تصویر | System.Drawing.Common 8.0 |
| Installer | Inno Setup 7 |
| CI/CD | GitHub Actions |

---

## گزارش باگ

باگ پیدا کردی؟ لطفاً یه issue باز کن:

**[→ باز کردن Bug Report](https://github.com/DeZ4p/DeZ4pAndroidManager/issues/new?template=bug_report.yml)**

لطفاً این اطلاعات رو بفرست:

- **نسخه ویندوز** — با دستور `winver`
- **نسخه‌ی برنامه** — از سایدبار
- **مدل دستگاه + نسخه‌ی اندروید**
- **مراحل بازتولید**
- **انتظار vs واقعیت**
- **لاگ‌ها** — از `%LOCALAPPDATA%\DeZ4pAndroidManager\activity.json`
- **اسکرین‌شات** اگه مشکل UI بود

### گزارش مشکلات امنیتی

**برای آسیب‌پذیری‌های امنیتی issue عمومی باز نکنید.**
گزارش خصوصی: **[Security Advisory](https://github.com/DeZ4p/DeZ4pAndroidManager/security/advisories/new)**

---

## لایسنس

این پروژه تحت **MIT License** منتشر شده — به فایل [LICENSE](LICENSE) مراجعه کنید.

استفاده، تغییر و توزیع آزاد است، از جمله برای اهداف تجاری.

---

## مشارکت

مشارکت خوش‌آمد است! لطفاً [CONTRIBUTING.md](CONTRIBUTING.md) رو بخونید و [Code of Conduct](CODE_OF_CONDUCT.md) رو رعایت کنید.

### قوانین سریع

- زبان: C# 12 / .NET 8
- الگو: MVVM سخت‌گیرانه
- هر فایل با `// (c) DeZ4p | t.me/DeZ4p | All Rights Reserved` شروع شه
- بدون `TODO`، بدون stub، بدون placeholder
- متن UI به انگلیسی (از LocalizationService استفاده کن)
- `dotnet build -c Release` باید بدون warning پاس شه

---

## پشتیبانی

- **تلگرام:** [t.me/DeZ4p](https://t.me/DeZ4p)
- **Issues:** [GitHub Issues](https://github.com/DeZ4p/DeZ4pAndroidManager/issues)
- **Discussions:** [GitHub Discussions](https://github.com/DeZ4p/DeZ4pAndroidManager/discussions)

---

<div align="center">

**© DeZ4p | [t.me/DeZ4p](https://t.me/DeZ4p) | All Rights Reserved**

ساخته شده با ❤️ برای جامعه‌ی اندروید

</div>