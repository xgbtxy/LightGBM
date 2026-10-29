using System.Runtime.InteropServices;

internal static class Program
{
    private const string Library = "lib_lightgbm";
    private const int Float32 = 0;
    private const int PredictNormal = 0;
    private const int Rows = 8;
    private const int Columns = 2;

    private static readonly float[] Features =
    [
        0f, 0f,
        0f, 1f,
        1f, 0f,
        1f, 1f,
        10f, 10f,
        10f, 11f,
        11f, 10f,
        11f, 11f
    ];

    private static readonly float[] Labels = [0f, 0f, 0f, 0f, 1f, 1f, 1f, 1f];

    private static int Main(string[] args)
    {
        try
        {
            CheckArchitecture(args.Contains("--expect-arm64", StringComparer.Ordinal));
            TrainAndPredict();
            Console.WriteLine($"Native LightGBM NuGet smoke passed on {RuntimeInformation.RuntimeIdentifier} ({RuntimeInformation.ProcessArchitecture}).");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void CheckArchitecture(bool expectArm64)
    {
        Architecture expected = expectArm64 ? Architecture.Arm64 : Architecture.X64;
        if (RuntimeInformation.ProcessArchitecture != expected)
        {
            throw new InvalidOperationException(
                $"Expected an {expected} process, got {RuntimeInformation.ProcessArchitecture}."
            );
        }
    }

    private static void TrainAndPredict()
    {
        IntPtr dataset = IntPtr.Zero;
        IntPtr booster = IntPtr.Zero;
        try
        {
            Check(
                Native.LGBM_DatasetCreateFromMat(
                    Features,
                    Float32,
                    Rows,
                    Columns,
                    1,
                    "max_bin=15 min_data_in_bin=1 min_data_in_leaf=1 num_threads=1",
                    IntPtr.Zero,
                    out dataset
                )
            );

            Check(Native.LGBM_DatasetSetField(dataset, "label", Labels, Rows, Float32));
            Check(
                Native.LGBM_BoosterCreate(
                    dataset,
                    "objective=binary min_data_in_leaf=1 num_leaves=4 verbosity=-1 num_threads=1",
                    out booster
                )
            );

            for (int iteration = 0; iteration < 8; iteration++)
            {
                Check(Native.LGBM_BoosterUpdateOneIter(booster, out _));
            }

            double[] predictions = new double[Rows];
            Check(
                Native.LGBM_BoosterPredictForMat(
                    booster,
                    Features,
                    Float32,
                    Rows,
                    Columns,
                    1,
                    PredictNormal,
                    0,
                    -1,
                    string.Empty,
                    out long predictionCount,
                    predictions
                )
            );

            if (predictionCount != Rows || predictions.Any(value => !double.IsFinite(value)))
            {
                throw new InvalidOperationException("Prediction output has an invalid length or non-finite value.");
            }

            double negativeMean = predictions.Take(4).Average();
            double positiveMean = predictions.Skip(4).Average();
            if (positiveMean <= negativeMean)
            {
                throw new InvalidOperationException(
                    $"Native training smoke failed: positive mean {positiveMean} <= negative mean {negativeMean}."
                );
            }
        }
        finally
        {
            if (booster != IntPtr.Zero)
            {
                Check(Native.LGBM_BoosterFree(booster));
            }

            if (dataset != IntPtr.Zero)
            {
                Check(Native.LGBM_DatasetFree(dataset));
            }
        }
    }

    private static void Check(int status)
    {
        if (status != 0)
        {
            string message = Marshal.PtrToStringUTF8(Native.LGBM_GetLastError()) ?? "Unknown LightGBM error.";
            throw new InvalidOperationException(message);
        }
    }

    private static class Native
    {
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr LGBM_GetLastError();

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int LGBM_DatasetCreateFromMat(
            [In] float[] data,
            int dataType,
            int rowCount,
            int columnCount,
            int rowMajor,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string parameters,
            IntPtr reference,
            out IntPtr dataset
        );

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int LGBM_DatasetSetField(
            IntPtr dataset,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string fieldName,
            [In] float[] fieldData,
            int elementCount,
            int dataType
        );

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int LGBM_BoosterCreate(
            IntPtr dataset,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string parameters,
            out IntPtr booster
        );

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int LGBM_BoosterUpdateOneIter(IntPtr booster, out int producedEmptyTree);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int LGBM_BoosterPredictForMat(
            IntPtr booster,
            [In] float[] data,
            int dataType,
            int rowCount,
            int columnCount,
            int rowMajor,
            int predictType,
            int startIteration,
            int iterationCount,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string parameters,
            out long resultCount,
            [Out] double[] result
        );

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int LGBM_BoosterFree(IntPtr booster);

        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int LGBM_DatasetFree(IntPtr dataset);
    }
}
