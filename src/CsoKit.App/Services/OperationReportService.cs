using System.Globalization;
using System.IO;
using System.Text;
using CsoKit.App.Localization;
using CsoKit.App.Models;
using CsoKit.Application;

namespace CsoKit.App.Services;

internal sealed record OperationReportRequest(
    UiOperationKind Kind,
    string InputPath,
    string OutputPath);

internal sealed record OperationReportWriteResult(string? ReportPath, string? ErrorMessage)
{
    public bool Success => !string.IsNullOrWhiteSpace(ReportPath);
}

internal static class OperationReportService
{
    public static OperationReportWriteResult TryWrite(
        OperationReportRequest request,
        CsoOperationResult result,
        string operationName,
        UiLanguage language)
    {
        try
        {
            string directory = GetReportDirectory(request);
            Directory.CreateDirectory(directory);
            string reportPath = CreateUniqueReportPath(directory, request);
            string report = language == UiLanguage.Arabic
                ? BuildArabicOperationReport(request, result, operationName)
                : BuildEnglishOperationReport(request, result, operationName);
            File.WriteAllText(reportPath, report, Encoding.UTF8);
            return new OperationReportWriteResult(reportPath, null);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return new OperationReportWriteResult(null, ex.Message);
        }
    }

    private static string GetReportDirectory(OperationReportRequest request)
    {
        string anchorPath = !string.IsNullOrWhiteSpace(request.OutputPath)
            ? request.OutputPath
            : request.InputPath;

        string fullPath = Path.GetFullPath(anchorPath);
        string? directory = Directory.Exists(fullPath)
            ? fullPath
            : Path.GetDirectoryName(fullPath);

        return string.IsNullOrWhiteSpace(directory)
            ? Directory.GetCurrentDirectory()
            : directory;
    }

    private static string CreateUniqueReportPath(string directory, OperationReportRequest request)
    {
        string baseName = CreateReportBaseName(request);
        string reportKind = CreateReportKindName(request.Kind);
        string candidate = Path.Combine(directory, $"{baseName}.{reportKind}.txt");

        if (!File.Exists(candidate) && !Directory.Exists(candidate))
        {
            return candidate;
        }

        for (int number = 2; number < int.MaxValue; number++)
        {
            candidate = Path.Combine(directory, $"{baseName}.{reportKind}-{number}.txt");

            if (!File.Exists(candidate) && !Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new IOException("Could not create a unique operation report path.");
    }

    private static string CreateReportBaseName(OperationReportRequest request)
    {
        string anchorPath = !string.IsNullOrWhiteSpace(request.InputPath)
            ? request.InputPath
            : request.OutputPath;
        string baseName = SanitizeFileName(Path.GetFileNameWithoutExtension(anchorPath));

        if (string.IsNullOrWhiteSpace(baseName))
        {
            return "csokit";
        }

        return StripGeneratedSuffixes(baseName);
    }

    private static string CreateReportKindName(UiOperationKind operationKind)
    {
        return operationKind switch
        {
            UiOperationKind.Compress => "compress-report",
            UiOperationKind.Decompress => "decompress-report",
            UiOperationKind.Verify => "verify-report",
            UiOperationKind.Repair => "repair-report",
            UiOperationKind.Detect => "detect-report",
            UiOperationKind.Analyze => "analyze-report",
            UiOperationKind.Measure => "measure-report",
            _ => "operation-report",
        };
    }

    private static string StripGeneratedSuffixes(string baseName)
    {
        string normalized = baseName.Trim();

        while (normalized.EndsWith(".repaired", StringComparison.OrdinalIgnoreCase) ||
               normalized.EndsWith(" - CsoKit Repaired", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized.EndsWith(".repaired", StringComparison.OrdinalIgnoreCase)
                ? normalized[..^".repaired".Length].Trim()
                : normalized[..^" - CsoKit Repaired".Length].Trim();
        }

        return string.IsNullOrWhiteSpace(normalized) ? "csokit" : normalized;
    }

    private static string SanitizeFileName(string value)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        StringBuilder builder = new(value.Length);

        foreach (char c in value)
        {
            builder.Append(invalid.Contains(c) ? '_' : c);
        }

        return builder.ToString().Trim();
    }

    private static string BuildEnglishOperationReport(
        OperationReportRequest request,
        CsoOperationResult result,
        string operationName)
    {
        StringBuilder builder = new();
        builder.AppendLine("CsoKit Operation Report");
        builder.AppendLine($"Generated: {DateTime.Now:O}");
        builder.AppendLine($"Operation: {FormatEnglishOperationName(operationName, request.Kind)}");
        builder.AppendLine($"Success: {result.Success}");
        builder.AppendLine($"Status: {result.Status}");
        builder.AppendLine($"Input: {request.InputPath}");

        if (!string.IsNullOrWhiteSpace(request.OutputPath))
        {
            builder.AppendLine($"Output: {request.OutputPath}");
        }

        builder.AppendLine();
        builder.AppendLine("Details:");
        builder.AppendLine(result.Details ?? string.Empty);
        return builder.ToString();
    }

    private static string BuildArabicOperationReport(
        OperationReportRequest request,
        CsoOperationResult result,
        string operationName)
    {
        StringBuilder builder = new();
        builder.AppendLine("تقرير عمليات CsoKit");
        builder.AppendLine($"تم الإنشاء: {DateTime.Now:O}");
        builder.AppendLine($"العملية: {FormatArabicOperationName(operationName, request.Kind)}");
        builder.AppendLine($"النتيجة النهائية: {FormatArabicSuccess(result.Success)}");
        builder.AppendLine($"الحالة: {TranslateStatusToArabic(result.Status)}");
        builder.AppendLine($"الإدخال: {request.InputPath}");

        if (!string.IsNullOrWhiteSpace(request.OutputPath))
        {
            builder.AppendLine($"الإخراج: {request.OutputPath}");
        }

        builder.AppendLine();
        builder.AppendLine("التفاصيل:");
        AppendArabicDetails(builder, result.DetailLines);
        return builder.ToString();
    }

    private static string CreateArabicOperationKindName(UiOperationKind operationKind)
    {
        return operationKind switch
        {
            UiOperationKind.Compress => "ضغط",
            UiOperationKind.Decompress => "فك الضغط",
            UiOperationKind.Verify => "فحص السلامة",
            UiOperationKind.Repair => "إصلاح الملف",
            UiOperationKind.Detect => "كشف الصيغة",
            UiOperationKind.Analyze => "تحليل ISO",
            UiOperationKind.Measure => "تقدير الحجم",
            _ => operationKind.ToString(),
        };
    }

    private static string FormatEnglishOperationName(string operationName, UiOperationKind operationKind)
    {
        string kindName = operationKind.ToString();
        return string.Equals(operationName, kindName, StringComparison.OrdinalIgnoreCase)
            ? operationName
            : $"{operationName} ({kindName})";
    }

    private static string FormatArabicOperationName(string operationName, UiOperationKind operationKind)
    {
        string kindName = CreateArabicOperationKindName(operationKind);
        return string.Equals(operationName, kindName, StringComparison.Ordinal)
            ? operationName
            : $"{operationName} ({kindName})";
    }

    private static string FormatArabicBoolean(bool value)
    {
        return value ? "نعم" : "لا";
    }

    private static string FormatArabicSuccess(bool value)
    {
        return value ? "نجاح" : "فشل";
    }

    private static string TranslateStatusToArabic(string status)
    {
        return status switch
        {
            "Deep verify passed; no corruption detected" => "اجتاز الفحص العميق؛ لم يُكتشف أي تلف",
            "Deep verify failed; corruption detected" => "فشل الفحص العميق؛ تم اكتشاف تلف أو مشكلة بنيوية",
            "Deep verify failed; no corruption verdict" => "فشل الفحص العميق؛ لم يصدر حكم تلف",
            "Shallow verify passed; no header/index corruption detected" => "اجتاز الفحص السطحي؛ لم تُكتشف مشكلة في الرأس أو الفهرس",
            "Shallow verify failed; structural issues detected" => "فشل الفحص السطحي؛ تم اكتشاف مشاكل بنيوية",
            "Verify failed; input format was not recognized" => "فشل الفحص؛ لم يتم التعرف على صيغة الإدخال",
            "Verify failed; unsupported shallow format" => "فشل الفحص؛ الصيغة غير مدعومة في الفحص السطحي",
            "Rebuild completed; no input corruption was proven" => "اكتملت إعادة البناء؛ لم يثبت وجود تلف في الإدخال",
            "Repair completed; recoverable input issues were detected" => "اكتمل الإصلاح؛ تم اكتشاف مشاكل قابلة للاسترداد في الإدخال",
            "Repair failed; re-dump required" => "فشل الإصلاح؛ يلزم إعادة نسخ الملف من المصدر",
            "Repair failed after detecting input issues" => "فشل الإصلاح بعد اكتشاف مشاكل في الإدخال",
            "Repair failed" => "فشل الإصلاح",
            "Compress completed" => "اكتمل الضغط",
            "Compress failed" => "فشل الضغط",
            "Decompress completed" => "اكتمل فك الضغط",
            "Decompress failed" => "فشل فك الضغط",
            "Detect completed" => "اكتمل كشف الصيغة",
            "Detect failed" => "فشل كشف الصيغة",
            "Analyze completed" => "اكتمل التحليل",
            "Analyze failed" => "فشل التحليل",
            "Measure completed" => "اكتمل تقدير الحجم",
            "Measure failed" => "فشل تقدير الحجم",
            _ => status,
        };
    }

    private static void AppendArabicDetails(
        StringBuilder builder,
        IReadOnlyList<CsoOperationDetail> detailLines)
    {
        foreach (CsoOperationDetail detail in detailLines)
        {
            switch (detail.Kind)
            {
                case CsoOperationDetailKind.Blank:
                    builder.AppendLine();
                    break;
                case CsoOperationDetailKind.Section:
                    builder.AppendLine(ArabicSectionNames.TryGetValue(detail.Label ?? string.Empty, out string? section)
                        ? section
                        : detail.Label);
                    break;
                case CsoOperationDetailKind.Field:
                    string label = detail.Label ?? string.Empty;
                    string translatedLabel = ArabicDetailLabels.TryGetValue(label, out string? localizedLabel)
                        ? localizedLabel
                        : label;
                    builder.AppendLine($"{translatedLabel}: {TranslateDetailValueToArabic(label, detail.Value ?? string.Empty)}");
                    break;
                case CsoOperationDetailKind.Bullet:
                    builder.AppendLine($"- {TranslateIssueTextToArabic(detail.Value ?? string.Empty)}");
                    break;
                default:
                    builder.AppendLine(TranslateKnownValueToArabic(detail.Value ?? string.Empty));
                    break;
            }
        }
    }

    private static string TranslateDetailValueToArabic(string label, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        if (string.Equals(label, "Input", StringComparison.Ordinal) ||
            string.Equals(label, "Output", StringComparison.Ordinal) ||
            string.Equals(label, "Format", StringComparison.Ordinal) ||
            string.Equals(label, "Magic", StringComparison.Ordinal) ||
            string.Equals(label, "Title", StringComparison.Ordinal) ||
            string.Equals(label, "DISC_ID UMD_DATA", StringComparison.Ordinal) ||
            string.Equals(label, "DISC_ID PARAM.SFO", StringComparison.Ordinal))
        {
            return value;
        }

        if (string.Equals(label, "SHA256", StringComparison.Ordinal) ||
            string.Equals(label, "Reconstructed SHA256", StringComparison.Ordinal))
        {
            return string.Equals(value, "Disabled", StringComparison.Ordinal) ? "معطّل" : value;
        }

        if (string.Equals(label, "Output written", StringComparison.Ordinal))
        {
            return TranslateBooleanVerdict(value, falseText: "لا — هذه عملية فحص فقط ولم يتم إنشاء ملف إخراج", trueText: "نعم — تم إنشاء ملف إخراج");
        }

        if (string.Equals(label, "Corruption detected", StringComparison.Ordinal))
        {
            return TranslateBooleanVerdict(value, falseText: "لا — لم يثبت وجود تلف", trueText: "نعم — تم اكتشاف تلف أو خلل بنيوي");
        }

        if (string.Equals(label, "Repair needed", StringComparison.Ordinal))
        {
            return TranslateBooleanVerdict(value, falseText: "لا — لا يحتاج إصلاح", trueText: "نعم — يحتاج إصلاح أو إعادة نسخ حسب نوع المشكلة");
        }

        if (string.Equals(label, "Issues", StringComparison.Ordinal))
        {
            return string.Equals(value, "none", StringComparison.OrdinalIgnoreCase) ? "لا توجد" : TranslateKnownValueToArabic(value);
        }

        return TranslateKnownValueToArabic(TranslateValueFragmentsToArabic(value));
    }

    private static string TranslateValueFragmentsToArabic(string value)
    {
        return value
            .Replace(" of indexed blocks", " من الأجزاء المفهرسة", StringComparison.Ordinal)
            .Replace(" MiB/s", " ميبي بايت/ثانية", StringComparison.Ordinal)
            .Replace(" MiB", " ميبي بايت", StringComparison.Ordinal)
            .Replace(" bytes", " بايت", StringComparison.Ordinal);
    }

    private static string TranslateBooleanVerdict(string value, string falseText, string trueText)
    {
        return value switch
        {
            "False" => falseText,
            "True" => trueText,
            "No" => falseText,
            "Yes" => trueText,
            "Unknown" => "غير معروف — لا يمكن إصدار حكم من هذه العملية",
            _ => TranslateKnownValueToArabic(TranslateValueFragmentsToArabic(value)),
        };
    }

    private static string TranslateKnownValueToArabic(string value)
    {
        return value switch
        {
            "True" => "نعم",
            "False" => "لا",
            "none" => "لا توجد",
            "None" => "لا توجد",
            "Unknown" => "غير معروف",
            "Yes" => "نعم",
            "No" => "لا",
            "Passed" => "سليم",
            "Failed" => "فشل",
            "Deep" => "عميق",
            "Shallow" => "سطحي",
            "Disabled" => "معطّل",
            "N/A" => "غير منطبق",
            "Not completed" => "لم يكتمل",
            "Not determined by shallow verify" => "غير محدد بواسطة الفحص السطحي",
            "N/A for this container reader" => "لا ينطبق على قارئ هذه الحاوية",
            "Passed via container reader" => "سليم عبر قارئ الحاوية",
            "Hybrid CSO verification" => "فحص CSO هجين",
            "Hybrid container verification" => "فحص حاوية هجين",
            "Header + index + block payload reconstruction" => "رأس الملف + الفهرس + إعادة بناء بيانات الأجزاء",
            "Container header + block payload reconstruction" => "رأس الحاوية + إعادة بناء بيانات الأجزاء",
            "Legacy structural CSO header/index validation" => "تحقق بنيوي محافظ لرأس CSO وفهرسه",
            "Streaming payload decode with pooled compressed buffers" => "فك بيانات متدفق باستخدام مخازن مضغوطة معاد استخدامها",
            "Coverage, topology, bounds, and reconstruction diagnostics" => "التغطية والبنية والحدود وتشخيص إعادة البناء",
            "Coverage, zero-block, and reconstruction diagnostics" => "التغطية والأجزاء الصفرية وتشخيص إعادة البناء",
            "The file was read block-by-block and payload data was reconstructed in memory. No repair output was produced." => "تمت قراءة الملف جزءاً بجزء، وأُعيد بناء بياناته داخل الذاكرة. لم يتم إنشاء ملف إخراج أو ملف إصلاح.",
            "Header and index metadata were inspected only; compressed block payloads were not decompressed." => "تم فحص بيانات الرأس والفهرس فقط؛ لم يتم فك ضغط بيانات الأجزاء.",
            "No corruption was detected by deep verification. The input was readable and all checked payload blocks reconstructed successfully." => "لم يكتشف الفحص العميق أي تلف. كان الإدخال قابلاً للقراءة، وتمت إعادة بناء كل الأجزاء المفحوصة بنجاح.",
            "Corruption or unsupported container structure was detected. The file did not fully reconstruct under deep verification." => "تم اكتشاف تلف أو بنية حاوية غير مدعومة. لم يكتمل إعادة بناء الملف أثناء الفحص العميق.",
            "This verification validates container structure, index/bounds semantics, and payload decompression. It does not prove Redump hash match, game database identity, or gameplay correctness." => "يتحقق هذا الفحص من بنية الحاوية، ومنطق الفهرس والحدود، وفك ضغط البيانات. لا يثبت هذا الفحص تطابق Redump، أو هوية اللعبة في قواعد البيانات، أو صحة التشغيل داخل اللعبة.",
            "No header/index corruption was detected. This is a metadata-only pass and does not prove that every compressed block can be decompressed." => "لم يتم اكتشاف تلف في الرأس أو الفهرس. هذا فحص بيانات وصفية فقط ولا يثبت أن كل جزء مضغوط قابل لفك الضغط.",
            "Structural CSO metadata issues were detected. Run Deep verify or Repair to classify the damage." => "تم اكتشاف مشاكل بنيوية في بيانات CSO الوصفية. شغّل الفحص العميق أو الإصلاح لتصنيف الضرر.",
            "Yes or re-dump required; see Issues for the exact failing block/condition." => "نعم أو يلزم إعادة النسخ؛ راجع المشاكل لمعرفة الجزء أو الشرط الفاشل بدقة.",
            "Counted after payload decode; may overlap compressed/stored block counts." => "تُحسب بعد فك بيانات الأجزاء، وقد تتداخل مع عدد الأجزاء المضغوطة أو المخزنة.",
            "N/A for raw image" => "غير منطبق على الصورة الخام",
            "Hybrid raw ISO verification" => "فحص ISO خام هجين",
            "ISO9660 probe + raw sector read + full payload reconstruction" => "استكشاف ISO9660 + قراءة قطاعات خام + إعادة بناء كاملة للبيانات",
            "ISO9660 primary-volume probe and strict 2048-byte sector-alignment validation" => "استكشاف واصف ISO9660 الأساسي والتحقق الصارم من محاذاة قطاعات 2048 بايت",
            "Sequential raw-sector read with pooled output buffers" => "قراءة قطاعات خام متتابعة باستخدام مخازن إخراج معاد استخدامها",
            "Coverage, zero-content, bounds, and reconstruction diagnostics" => "التغطية والمحتوى الصفري والحدود وتشخيص إعادة البناء",
            "Not reached because raw ISO alignment validation failed." => "لم يتم الوصول إلى هذه الطبقة لأن التحقق من محاذاة ISO الخام فشل.",
            "No raw-image read, alignment, or reconstruction problems were detected. The input was readable and every checked sector reconstructed successfully." => "لم تُكتشف مشاكل قراءة أو محاذاة أو إعادة بناء في الصورة الخام. كان الإدخال قابلاً للقراءة، وتمت إعادة بناء كل القطاعات المفحوصة بنجاح.",
            "Raw-image read, alignment, or unsupported container structure failed. The file did not fully reconstruct under deep verification." => "فشلت قراءة الصورة الخام أو المحاذاة أو بنية الحاوية غير المدعومة. لم تكتمل إعادة بناء الملف أثناء الفحص العميق.",
            "This verification validates raw image readability, 2048-byte sector alignment, full block coverage, and payload reconstruction. It does not prove Redump hash match, game database identity, or gameplay correctness." => "يتحقق هذا الفحص من قابلية قراءة الصورة الخام، ومحاذاة قطاعات 2048 بايت، وتغطية كل الأجزاء، وإعادة بناء البيانات. لا يثبت هذا الفحص تطابق Redump، أو هوية اللعبة في قواعد البيانات، أو صحة التشغيل داخل اللعبة.",
            _ => value,
        };
    }

    private static string TranslateIssueTextToArabic(string issueText)
    {
        if (string.Equals(issueText, "none", StringComparison.OrdinalIgnoreCase))
        {
            return "لا توجد";
        }

        return issueText;
    }

    private static readonly Dictionary<string, string> ArabicSectionNames = new(StringComparer.Ordinal)
    {
        ["Verification layers:"] = "طبقات الفحص:",
        ["Integrity checks:"] = "فحوصات السلامة:",
        ["CSO metadata:"] = "بيانات CSO الوصفية:",
        ["Raw image metadata:"] = "بيانات الصورة الخام:",
        ["Forensic statistics:"] = "إحصائيات الفحص:",
        ["Warnings:"] = "تحذيرات:",
        ["Issues:"] = "المشاكل:",
        ["Codec wins:"] = "نتائج codecs:",
    };

    private static readonly Dictionary<string, string> ArabicDetailLabels = new(StringComparer.Ordinal)
    {
        ["Input"] = "الإدخال",
        ["Output"] = "الإخراج",
        ["Verification type"] = "نوع الفحص",
        ["Output written"] = "هل تم إنشاء ملف إخراج",
        ["Action taken"] = "الإجراء المتخذ",
        ["Format"] = "الصيغة",
        ["CSO version"] = "إصدار CSO",
        ["Block size"] = "حجم الجزء",
        ["Index shift"] = "إزاحة الفهرس",
        ["Uncompressed size"] = "الحجم بعد فك الضغط",
        ["Compressed file size"] = "حجم الملف المضغوط",
        ["Container ratio"] = "نسبة الحاوية",
        ["Space saved"] = "المساحة الموفرة",
        ["Algorithm"] = "الخوارزمية",
        ["Scope"] = "النطاق",
        ["Legacy layer"] = "الطبقة القديمة",
        ["Modern layer"] = "الطبقة الحديثة",
        ["Forensic layer"] = "طبقة التشخيص",
        ["Header check"] = "فحص الرأس",
        ["Index check"] = "فحص الفهرس",
        ["Final sentinel"] = "المؤشر النهائي",
        ["Block offset order"] = "ترتيب إزاحات الأجزاء",
        ["Bounds check"] = "فحص الحدود",
        ["Payload decode"] = "فك البيانات",
        ["Reconstructed size"] = "حجم إعادة البناء",
        ["Result"] = "النتيجة",
        ["Corruption detected"] = "هل تم اكتشاف تلف",
        ["Coverage"] = "التغطية",
        ["Blocks checked"] = "الأجزاء المفحوصة",
        ["Bytes reconstructed"] = "البايتات المعاد بناؤها",
        ["Expected reconstructed bytes"] = "البايتات المتوقعة بعد إعادة البناء",
        ["File length"] = "طول الملف",
        ["Header size"] = "حجم الرأس",
        ["Index entries"] = "مدخلات الفهرس",
        ["Index table bytes"] = "بايتات جدول الفهرس",
        ["Index end offset"] = "إزاحة نهاية الفهرس",
        ["First data offset"] = "أول إزاحة بيانات",
        ["Final data offset"] = "آخر إزاحة بيانات",
        ["Physical payload bytes"] = "بايتات البيانات الفعلية",
        ["Payload blocks decoded"] = "أجزاء البيانات المفكوكة",
        ["Compressed blocks"] = "الأجزاء المضغوطة",
        ["Stored blocks"] = "الأجزاء المخزنة",
        ["Zero-content blocks"] = "الأجزاء ذات المحتوى الصفري",
        ["Decoded zero-content blocks"] = "الأجزاء ذات المحتوى الصفري بعد الفك",
        ["Zero-content note"] = "ملاحظة الأجزاء الصفرية",
        ["SHA256"] = "SHA256",
        ["Reconstructed SHA256"] = "SHA256 المعاد بناؤه",
        ["Issues"] = "المشاكل",
        ["Elapsed"] = "الوقت المستغرق",
        ["Throughput"] = "سرعة المعالجة",
        ["Repair needed"] = "هل يحتاج إصلاح",
        ["Conclusion"] = "الخلاصة",
        ["Limitations"] = "الحدود",
        ["Profile"] = "ملف الإعداد",
        ["Input format"] = "صيغة الإدخال",
        ["Repair mode"] = "وضع الإصلاح",
        ["Input verification"] = "فحص الإدخال",
        ["Output verification"] = "فحص الإخراج",
        ["Bytes read"] = "البايتات المقروءة",
        ["Bytes written"] = "البايتات المكتوبة",
        ["Padding bytes"] = "بايتات الحشو",
        ["Codec report blocks"] = "أجزاء تقرير codec",
        ["Raw image metadata"] = "بيانات الصورة الخام",
        ["Image format"] = "صيغة الصورة",
        ["Sector size"] = "حجم القطاع",
        ["Logical image size"] = "حجم الصورة المنطقي",
        ["Physical file size"] = "حجم الملف الفعلي",
        ["Payload read/decode"] = "قراءة/فك البيانات",
        ["Error"] = "الخطأ",
    };

}
