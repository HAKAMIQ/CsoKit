using CsoKit.Core.Formats.Cso.Codecs.Managed;
using CsoKit.Core.Formats.Cso.Codecs.Native;
using CsoKit.Core.Native;

namespace CsoKit.Core.Formats.Cso.Codecs;

public static class CsoTrialPlanner
{
    public static CsoTrialPlan CreatePlan(
        CsoCompressionProfile profile,
        bool useZopfli,
        bool useExperimental = false)
    {
        List<CsoCodecTrial> trials = [];

        switch (profile)
        {
            case CsoCompressionProfile.GameSafe:
                trials.Add(new(CsoCodecKind.ManagedDeflateSmallest, "managed-deflate-smallest", false, false, false));
                trials.Add(new(CsoCodecKind.NativeZlibDefault, "native-zlib-default", true, false, false));
                trials.Add(new(CsoCodecKind.NativeZlibFiltered, "native-zlib-filtered", true, false, false));
                trials.Add(new(CsoCodecKind.NativeZlibRle, "native-zlib-rle", true, false, false));
                trials.Add(new(CsoCodecKind.NativeLibDeflate6, "native-libdeflate-6", true, false, false));
                break;

            case CsoCompressionProfile.Fast:
                trials.Add(new(CsoCodecKind.ManagedDeflateFastest, "managed-deflate-fastest", false, false, false));
                trials.Add(new(CsoCodecKind.NativeLibDeflate1, "native-libdeflate-1", true, false, false));
                trials.Add(new(CsoCodecKind.NativeLibDeflate6, "native-libdeflate-6", true, false, false));
                break;

            case CsoCompressionProfile.Compat:
                trials.Add(new(CsoCodecKind.ManagedDeflateSmallest, "managed-deflate-smallest", false, false, false));
                trials.Add(new(CsoCodecKind.NativeZlibDefault, "native-zlib-default", true, false, false));
                trials.Add(new(CsoCodecKind.NativeZlibFiltered, "native-zlib-filtered", true, false, false));
                trials.Add(new(CsoCodecKind.NativeZlibRle, "native-zlib-rle", true, false, false));
                break;

            case CsoCompressionProfile.Smallest:
                trials.Add(new(CsoCodecKind.ManagedDeflateFastest, "managed-deflate-fastest", false, false, false));
                trials.Add(new(CsoCodecKind.ManagedDeflateOptimal, "managed-deflate-optimal", false, false, false));
                trials.Add(new(CsoCodecKind.ManagedDeflateSmallest, "managed-deflate-smallest", false, false, false));
                trials.Add(new(CsoCodecKind.NativeZlibDefault, "native-zlib-default", true, false, false));
                trials.Add(new(CsoCodecKind.NativeZlibFiltered, "native-zlib-filtered", true, false, false));
                trials.Add(new(CsoCodecKind.NativeZlibHuffmanOnly, "native-zlib-huffman-only", true, false, false));
                trials.Add(new(CsoCodecKind.NativeZlibRle, "native-zlib-rle", true, false, false));
                trials.Add(new(CsoCodecKind.NativeLibDeflate6, "native-libdeflate-6", true, false, false));
                trials.Add(new(CsoCodecKind.NativeLibDeflate9, "native-libdeflate-9", true, false, false));
                trials.Add(new(CsoCodecKind.NativeLibDeflate12, "native-libdeflate-12", true, false, false));
                break;

            case CsoCompressionProfile.ArchiveSmallest:
                trials.Add(new(CsoCodecKind.ManagedDeflateFastest, "managed-deflate-fastest", false, false, false));
                trials.Add(new(CsoCodecKind.ManagedDeflateOptimal, "managed-deflate-optimal", false, false, false));
                trials.Add(new(CsoCodecKind.ManagedDeflateSmallest, "managed-deflate-smallest", false, false, false));
                trials.Add(new(CsoCodecKind.NativeLibDeflate9, "native-libdeflate-9", true, false, false));
                trials.Add(new(CsoCodecKind.NativeLibDeflate12, "native-libdeflate-12", true, false, false));
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(profile), profile, "Unsupported CSO compression profile.");
        }

        if (useZopfli)
        {
            trials.Add(new(CsoCodecKind.NativeZopfli5, "native-zopfli-5", true, true, false));
            trials.Add(new(CsoCodecKind.NativeZopfli15, "native-zopfli-15", true, true, false));

            if (useExperimental)
            {
                trials.Add(new(CsoCodecKind.NativeZopfli25, "native-zopfli-25", true, true, true));
            }
        }

        if (useExperimental)
        {
            trials.Add(new(CsoCodecKind.NativeSevenZipDeflate, "native-7z-deflate", true, true, true));
        }

        return new CsoTrialPlan(profile, useZopfli, useExperimental, trials);
    }

    public static IReadOnlyList<ICsoCodecTrial> CreateTrials(CsoTrialPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        List<ICsoCodecTrial> trials = new(plan.Trials.Count);

        foreach (CsoCodecTrial trial in plan.Trials)
        {
            trials.Add(CreateTrial(trial));
        }

        return trials;
    }

    private static ICsoCodecTrial CreateTrial(CsoCodecTrial trial)
    {
        return trial.Kind switch
        {
            CsoCodecKind.ManagedDeflateFastest => ManagedDeflateTrial.Fastest,
            CsoCodecKind.ManagedDeflateOptimal => ManagedDeflateTrial.Optimal,
            CsoCodecKind.ManagedDeflateSmallest => ManagedDeflateTrial.Smallest,
            CsoCodecKind.NativeZopfli5 => new NativeZopfliTrial(5),
            CsoCodecKind.NativeZopfli15 => new NativeZopfliTrial(15),
            CsoCodecKind.NativeZopfli25 => new NativeZopfliTrial(25),
            CsoCodecKind.NativeZlibDefault or
                CsoCodecKind.NativeZlibFiltered or
                CsoCodecKind.NativeZlibHuffmanOnly or
                CsoCodecKind.NativeZlibRle => CreateZlibTrial(trial),
            CsoCodecKind.NativeLibDeflate1 => new NativeLibDeflateTrial(trial.Kind, trial.Name, 1),
            CsoCodecKind.NativeLibDeflate6 => new NativeLibDeflateTrial(trial.Kind, trial.Name, 6),
            CsoCodecKind.NativeLibDeflate9 => new NativeLibDeflateTrial(trial.Kind, trial.Name, 9),
            CsoCodecKind.NativeLibDeflate12 => new NativeLibDeflateTrial(trial.Kind, trial.Name, 12),
            CsoCodecKind.NativeSevenZipDeflate => new NativeUnavailableTrial(trial.Kind, trial.Name, "NativeCodecUnavailable", "Native 7z deflate is experimental and not linked in this build."),
            _ => throw new ArgumentOutOfRangeException(nameof(trial), trial.Kind, "Unsupported CSO codec trial."),
        };
    }

    private static NativeZlibTrial CreateZlibTrial(CsoCodecTrial trial)
    {
        NativeCsoRawCodec codec = trial.Kind switch
        {
            CsoCodecKind.NativeZlibDefault => NativeCsoRawCodec.ZlibDefault,
            CsoCodecKind.NativeZlibFiltered => NativeCsoRawCodec.ZlibFiltered,
            CsoCodecKind.NativeZlibHuffmanOnly => NativeCsoRawCodec.ZlibHuffmanOnly,
            CsoCodecKind.NativeZlibRle => NativeCsoRawCodec.ZlibRle,
            _ => throw new ArgumentOutOfRangeException(nameof(trial), trial.Kind, "Unsupported zlib trial."),
        };

        return new NativeZlibTrial(trial.Kind, trial.Name, codec);
    }
}
