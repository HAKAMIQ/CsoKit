using System.Windows;

namespace CsoKit.App.Localization;

public enum UiLanguage
{
    Arabic,
    English,
}

public sealed class UiText
{
    public required UiLanguage Language { get; init; }

    public required FlowDirection FlowDirection { get; init; }

    public required string WindowTitle { get; init; }

    public required string AppTitle { get; init; }

    public required string AppSubtitle { get; init; }

    public required string EngineStatus { get; init; }

    public required string LanguageToggleToolTip { get; init; }

    public required string SourceDestinationSection { get; init; }

    public required string InputFile { get; init; }

    public required string InputFileHint { get; init; }

    public required string OutputFile { get; init; }

    public required string OutputFileHint { get; init; }

    public required string BrowseInput { get; init; }

    public required string BrowseFolder { get; init; }

    public required string BrowseOutput { get; init; }

    public required string ChangeOutput { get; init; }

    public required string OpenOutput { get; init; }

    public required string StopOperation { get; init; }

    public required string DropTarget { get; init; }

    public required string Tasks { get; init; }

    public required string AutomaticOutput { get; init; }

    public required string KeepDefaultsHint { get; init; }

    public required string OperationsSection { get; init; }

    public required string OperationsHint { get; init; }

    public required string SelectedOperationSection { get; init; }

    public required string Detect { get; init; }

    public required string Analyze { get; init; }

    public required string Measure { get; init; }

    public required string Verify { get; init; }

    public required string Compress { get; init; }

    public required string Decompress { get; init; }

    public required string Repair { get; init; }

    public required string CompactCompress { get; init; }

    public required string CompactDecompress { get; init; }

    public required string CompactVerify { get; init; }

    public required string CompactRepair { get; init; }

    public required string DetectDescription { get; init; }

    public required string AnalyzeDescription { get; init; }

    public required string MeasureDescription { get; init; }

    public required string VerifyDescription { get; init; }

    public required string CompressDescription { get; init; }

    public required string DecompressDescription { get; init; }

    public required string RepairDescription { get; init; }

    public required string StartOperation { get; init; }

    public required string ClearLog { get; init; }

    public required string OptionsSection { get; init; }

    public required string CompressionProfile { get; init; }

    public required string ProfileGameSafe { get; init; }

    public required string ProfileCompat { get; init; }

    public required string ProfileFast { get; init; }

    public required string ProfileSmallest { get; init; }

    public required string ProfileArchiveSmallest { get; init; }

    public required string ForceOverwrite { get; init; }

    public required string AdvancedOptions { get; init; }

    public required string Threads { get; init; }

    public required string BlockSize { get; init; }

    public required string DeepVerify { get; init; }

    public required string Sha256 { get; init; }

    public required string CodecReport { get; init; }

    public required string DeepVerifyToolTip { get; init; }

    public required string Sha256ToolTip { get; init; }

    public required string CodecReportToolTip { get; init; }

    public required string ForceOverwriteToolTip { get; init; }

    public required string ThreadsToolTip { get; init; }

    public required string BlockSizeToolTip { get; init; }

    public required string ProgressSection { get; init; }

    public required string Ready { get; init; }

    public required string Running { get; init; }

    public required string OperationStarted { get; init; }

    public required string OperationSucceeded { get; init; }

    public required string OperationFailed { get; init; }

    public required string ReadyToCompress { get; init; }

    public required string ReadyToExtractIso { get; init; }

    public required string ReadyToVerify { get; init; }

    public required string ReadyToRepair { get; init; }

    public required string ReadyToDetect { get; init; }

    public required string ReadyToAnalyze { get; init; }

    public required string ReadyToMeasure { get; init; }

    public required string PreparingDetectInput { get; init; }

    public required string PreparingReadHeader { get; init; }

    public required string PreparingCheckIndex { get; init; }

    public required string PreparingRepair { get; init; }

    public required string PreparingAnalyzeImage { get; init; }

    public required string PreparingMeasure { get; init; }

    public required string ProgressReadHeader { get; init; }

    public required string ProgressCheckIndex { get; init; }

    public required string ProgressWriteCso { get; init; }

    public required string ProgressWriteIso { get; init; }

    public required string ProgressRepair { get; init; }

    public required string ProgressVerifyData { get; init; }

    public required string ProgressMeasure { get; init; }

    public required string ProgressRunningOperation { get; init; }

    public required string ProgressCheckingOutput { get; init; }

    public required string RepairCompleted { get; init; }

    public required string VerifyCompleted { get; init; }

    public required string WriteCompleted { get; init; }

    public required string Completed { get; init; }

    public required string LastResult { get; init; }

    public required string NoResultsYet { get; init; }

    public required string TechnicalDetails { get; init; }

    public required string Error { get; init; }

    public required string Input { get; init; }

    public required string Output { get; init; }

    public required string OriginalSize { get; init; }

    public required string ResultSize { get; init; }

    public required string SavedSize { get; init; }

    public required string Footer { get; init; }

    public required string SelectInputFile { get; init; }

    public required string SelectOutputFile { get; init; }

    public required string InputFileDialogFilter { get; init; }

    public required string OutputFileDialogFilter { get; init; }

    public required string InputPathRequired { get; init; }

    public required string InvalidCompressionProfile { get; init; }

    public required string InvalidBlockSize { get; init; }

    public required string InvalidThreads { get; init; }

    public required string InvalidOutputFileNameLength { get; init; }

    public required string UnsupportedOperation { get; init; }

    public required string OperationDoesNotUseOutput { get; init; }

    public required string OutputPathNotRequired { get; init; }
}

public static class ArabicUiText
{
    public static UiText Arabic { get; } = new()
    {
        Language = UiLanguage.Arabic,
        FlowDirection = FlowDirection.RightToLeft,
        WindowTitle = "CsoKit",
        AppTitle = "CsoKit",
        AppSubtitle = "محول صور ألعاب PSP: ضغط، فك ضغط، فحص، تحليل، وإصلاح.",
        EngineStatus = "متصل مباشرة بالمحرك الأساسي",
        LanguageToggleToolTip = "تبديل اللغة بين العربية والإنجليزية",

        SourceDestinationSection = "المهمة",
        InputFile = "ملف المصدر",
        InputFileHint = "اختر ملف ISO أو CSO أو ZSO أو DAX، أو اسحبه إلى هذه المنطقة.",
        OutputFile = "ملف الإخراج",
        OutputFileHint = "يتم اقتراح مسار الإخراج تلقائياً واسم الملف من حرفين إلى 10 أحرف بدون الامتداد.",
        BrowseInput = "اختيار ملفات",
        BrowseFolder = "اختيار مجلد",
        BrowseOutput = "حفظ باسم",
        ChangeOutput = "تغيير",
        OpenOutput = "فتح الناتج",
        StopOperation = "إيقاف",
        DropTarget = "اسحب ملفاً أو مجموعة ملفات هنا",
        Tasks = "المهام",
        AutomaticOutput = "تلقائي",
        KeepDefaultsHint = "اتركها كما هي لأفضل توافق.",

        OperationsSection = "نوع العملية",
        OperationsHint = "اختر نوع العملية ثم اضغط بدء التنفيذ.",
        SelectedOperationSection = "تفاصيل العملية المحددة",
        Detect = "كشف نوع الملف",
        Analyze = "تحليل PSP",
        Measure = "تقدير الحجم",
        Verify = "فحص السلامة",
        Compress = "ضغط إلى CSO",
        Decompress = "فك إلى ISO",
        Repair = "إصلاح الملف",
        CompactCompress = "ضغط إلى CSO",
        CompactDecompress = "فك إلى ISO",
        CompactVerify = "فحص",
        CompactRepair = "إصلاح",
        DetectDescription = "يفحص ترويسة الملف ويعرض نوع الحاوية والحقول التقنية بدون إنشاء ملف جديد.",
        AnalyzeDescription = "يتحقق من بنية صورة PSP ويعرض معلومات اللعبة والملفات الأساسية داخل الصورة.",
        MeasureDescription = "يقدر حجم CSO المتوقع باستخدام نمط الضغط وحجم الكتلة قبل إنشاء ملف فعلي.",
        VerifyDescription = "يفحص سلامة الملف، ويمكن تفعيل الفحص العميق أو حساب SHA256 عند الحاجة.",
        CompressDescription = "ينشئ ملف CSO من ISO مع اختيار نمط الضغط وحجم الكتلة.",
        DecompressDescription = "يفك ملف CSO إلى ISO مع اقتراح مسار إخراج مناسب تلقائياً.",
        RepairDescription = "ينشئ نسخة معالجة آمنة من الملف باستخدام نمط الألعاب وفحص عميق إلزامي.",
        StartOperation = "بدء",
        ClearLog = "مسح",

        OptionsSection = "الإعدادات",
        CompressionProfile = "نمط الضغط",
        ProfileGameSafe = "آمن للألعاب",
        ProfileCompat = "توافق أعلى",
        ProfileFast = "سريع",
        ProfileSmallest = "أصغر حجم",
        ProfileArchiveSmallest = "أرشفة قصوى",
        ForceOverwrite = "استبدال الملف الموجود",
        AdvancedOptions = "خيارات متقدمة",
        Threads = "العمليات المتزامنة",
        BlockSize = "حجم الكتلة",
        DeepVerify = "فحص عميق",
        Sha256 = "SHA256",
        CodecReport = "تقرير الضغط",
        DeepVerifyToolTip = "يزيد دقة الفحص وقد يستغرق وقتاً أطول.",
        Sha256ToolTip = "يحسب بصمة الملف للتحقق أو المقارنة.",
        CodecReportToolTip = "يعرض ملخصاً تقنياً مختصراً بعد العملية.",
        ForceOverwriteToolTip = "يسمح بالكتابة فوق ملف الناتج إذا كان موجوداً.",
        ThreadsToolTip = "عدد الملفات أو العمليات التي يمكن تشغيلها في نفس الوقت. اتركها 1 لأفضل استقرار.",
        BlockSizeToolTip = "حجم كتلة CSO؛ اتركه كما هو غالباً.",

        ProgressSection = "الحالة",
        Ready = "جاهز",
        Running = "جارٍ التنفيذ",
        OperationStarted = "بدأت العملية",
        OperationSucceeded = "اكتملت العملية بنجاح",
        OperationFailed = "فشلت العملية",
        ReadyToCompress = "جاهز للضغط",
        ReadyToExtractIso = "جاهز للفك إلى ISO",
        ReadyToVerify = "جاهز للفحص",
        ReadyToRepair = "جاهز للإصلاح",
        ReadyToDetect = "جاهز للكشف",
        ReadyToAnalyze = "جاهز للتحليل",
        ReadyToMeasure = "جاهز للتقدير",
        PreparingDetectInput = "اكتشاف نوع الإدخال...",
        PreparingReadHeader = "قراءة الترويسة...",
        PreparingCheckIndex = "فحص الفهرس...",
        PreparingRepair = "تحضير الإصلاح...",
        PreparingAnalyzeImage = "تحليل بنية الصورة...",
        PreparingMeasure = "تقدير الحجم...",
        ProgressReadHeader = "قراءة الترويسة...",
        ProgressCheckIndex = "فحص الفهرس...",
        ProgressWriteCso = "كتابة ملف CSO...",
        ProgressWriteIso = "كتابة ملف ISO...",
        ProgressRepair = "تنفيذ الإصلاح...",
        ProgressVerifyData = "فحص البيانات...",
        ProgressMeasure = "تقدير الحجم...",
        ProgressRunningOperation = "تنفيذ العملية...",
        ProgressCheckingOutput = "فحص الناتج...",
        RepairCompleted = "اكتمل الإصلاح",
        VerifyCompleted = "اكتمل الفحص",
        WriteCompleted = "اكتملت الكتابة",
        Completed = "اكتمل",
        LastResult = "ملخص النتيجة",
        NoResultsYet = "لم يتم تنفيذ أي عملية بعد.",
        TechnicalDetails = "سجل المهام",
        Error = "خطأ",
        Input = "الإدخال",
        Output = "الإخراج",
        OriginalSize = "الحجم الأصلي",
        ResultSize = "الحجم الناتج",
        SavedSize = "الموفر",

        Footer = "واجهة WPF تستدعي CsoKit.Core مباشرة دون تشغيل CLI كعملية منفصلة.",

        SelectInputFile = "اختيار ملف المصدر",
        SelectOutputFile = "تحديد ملف الإخراج",
        InputFileDialogFilter = "ملفات الألعاب المدعومة|*.iso;*.cso;*.zso;*.dax|ملفات ISO|*.iso|ملفات CSO|*.cso|ملفات ZSO|*.zso|ملفات DAX|*.dax|جميع الملفات|*.*",
        OutputFileDialogFilter = "ملف CSO|*.cso|ملف ISO|*.iso|جميع الملفات|*.*",

        InputPathRequired = "اختر ملف المصدر أولاً.",
        InvalidCompressionProfile = "نمط الضغط المحدد غير صحيح.",
        InvalidBlockSize = "حجم الكتلة يجب أن يكون رقماً صحيحاً أكبر من الصفر.",
        InvalidThreads = "عدد العمليات المتزامنة يجب أن يكون رقماً صحيحاً أكبر من الصفر.",
        InvalidOutputFileNameLength = "اسم ملف الإخراج يجب ألا يقل عن حرفين وألا يزيد على 10 أحرف بدون الامتداد.",
        UnsupportedOperation = "هذه العملية غير مدعومة حالياً.",
        OperationDoesNotUseOutput = "هذه العملية لا تتطلب ملف إخراج.",
        OutputPathNotRequired = "مسار الإخراج غير مطلوب لهذه العملية.",
    };

    public static UiText English { get; } = new()
    {
        Language = UiLanguage.English,
        FlowDirection = FlowDirection.LeftToRight,
        WindowTitle = "CsoKit",
        AppTitle = "CsoKit",
        AppSubtitle = "PSP image converter: compress, decompress, verify, analyze, and repair.",
        EngineStatus = "Connected directly to the core engine",
        LanguageToggleToolTip = "Switch language between Arabic and English",

        SourceDestinationSection = "Task",
        InputFile = "Source file",
        InputFileHint = "Choose an ISO, CSO, ZSO, or DAX file, or drop it into this area.",
        OutputFile = "Output file",
        OutputFileHint = "The output path is suggested automatically; the file name must be 2 to 10 characters excluding the extension.",
        BrowseInput = "Choose files",
        BrowseFolder = "Choose folder",
        BrowseOutput = "Save as",
        ChangeOutput = "Change",
        OpenOutput = "Open output",
        StopOperation = "Stop",
        DropTarget = "Drop one or more files here",
        Tasks = "Tasks",
        AutomaticOutput = "Automatic",
        KeepDefaultsHint = "Leave these as-is for best compatibility.",

        OperationsSection = "Operation type",
        OperationsHint = "Choose the operation, then start processing.",
        SelectedOperationSection = "Selected operation details",
        Detect = "Detect file type",
        Analyze = "Analyze PSP image",
        Measure = "Estimate output size",
        Verify = "Verify integrity",
        Compress = "Compress to CSO",
        Decompress = "Decompress to ISO",
        Repair = "Repair file",
        CompactCompress = "Compress to CSO",
        CompactDecompress = "Decompress to ISO",
        CompactVerify = "Verify",
        CompactRepair = "Repair",
        DetectDescription = "Reads the file header and reports the container type and technical fields without creating output.",
        AnalyzeDescription = "Checks the PSP image structure and reports game metadata and required image files.",
        MeasureDescription = "Estimates the expected CSO size using the selected profile and block size before writing output.",
        VerifyDescription = "Checks file integrity, with optional deep verification and SHA256 calculation.",
        CompressDescription = "Creates a CSO from an ISO with profile and block size options.",
        DecompressDescription = "Expands a CSO back to ISO and suggests a matching output path automatically.",
        RepairDescription = "Creates a safe repaired copy using the game-safe profile and required deep verification.",
        StartOperation = "Start",
        ClearLog = "Clear",

        OptionsSection = "Settings",
        CompressionProfile = "Compression profile",
        ProfileGameSafe = "Game-safe",
        ProfileCompat = "High compatibility",
        ProfileFast = "Fast",
        ProfileSmallest = "Smallest size",
        ProfileArchiveSmallest = "Archive smallest",
        ForceOverwrite = "Overwrite existing file",
        AdvancedOptions = "Advanced options",
        Threads = "Parallel operations",
        BlockSize = "Block size",
        DeepVerify = "Deep verify",
        Sha256 = "SHA256",
        CodecReport = "Codec report",
        DeepVerifyToolTip = "Runs a stricter check and may take longer.",
        Sha256ToolTip = "Calculates a file fingerprint for verification or comparison.",
        CodecReportToolTip = "Shows a short technical compression summary after the operation.",
        ForceOverwriteToolTip = "Allows the output file to be replaced when it already exists.",
        ThreadsToolTip = "Number of files or operations that can run at the same time. Leave it at 1 for best stability.",
        BlockSizeToolTip = "CSO block size; leaving the default is usually best.",

        ProgressSection = "Status",
        Ready = "Ready",
        Running = "Running",
        OperationStarted = "Operation started",
        OperationSucceeded = "Operation completed successfully",
        OperationFailed = "Operation failed",
        ReadyToCompress = "ready to compress",
        ReadyToExtractIso = "ready to extract to ISO",
        ReadyToVerify = "ready to verify",
        ReadyToRepair = "ready to repair",
        ReadyToDetect = "ready to detect",
        ReadyToAnalyze = "ready to analyze",
        ReadyToMeasure = "ready to measure",
        PreparingDetectInput = "Detecting input...",
        PreparingReadHeader = "Reading header...",
        PreparingCheckIndex = "Checking index...",
        PreparingRepair = "Preparing repair...",
        PreparingAnalyzeImage = "Analyzing image structure...",
        PreparingMeasure = "Estimating size...",
        ProgressReadHeader = "Reading header...",
        ProgressCheckIndex = "Checking index...",
        ProgressWriteCso = "Writing CSO...",
        ProgressWriteIso = "Writing ISO...",
        ProgressRepair = "Running repair...",
        ProgressVerifyData = "Checking data...",
        ProgressMeasure = "Estimating size...",
        ProgressRunningOperation = "Running operation...",
        ProgressCheckingOutput = "Checking output...",
        RepairCompleted = "Repair completed",
        VerifyCompleted = "Verification completed",
        WriteCompleted = "Write completed",
        Completed = "Completed",
        LastResult = "Result summary",
        NoResultsYet = "No operation has been executed yet.",
        TechnicalDetails = "Task log",
        Error = "Error",
        Input = "Input",
        Output = "Output",
        OriginalSize = "Original size",
        ResultSize = "Result size",
        SavedSize = "Saved",

        Footer = "WPF interface calling CsoKit.Core directly without launching the CLI as a separate process.",

        SelectInputFile = "Select source file",
        SelectOutputFile = "Select output file",
        InputFileDialogFilter = "Supported game images|*.iso;*.cso;*.zso;*.dax|ISO files|*.iso|CSO files|*.cso|ZSO files|*.zso|DAX files|*.dax|All files|*.*",
        OutputFileDialogFilter = "CSO file|*.cso|ISO file|*.iso|All files|*.*",

        InputPathRequired = "Select a source file first.",
        InvalidCompressionProfile = "The selected compression profile is invalid.",
        InvalidBlockSize = "Block size must be a positive integer.",
        InvalidThreads = "Parallel operations must be a positive integer.",
        InvalidOutputFileNameLength = "The output file name must be between 2 and 10 characters, excluding the extension.",
        UnsupportedOperation = "This operation is not supported.",
        OperationDoesNotUseOutput = "This operation does not require an output file.",
        OutputPathNotRequired = "An output path is not required for this operation.",
    };
}
