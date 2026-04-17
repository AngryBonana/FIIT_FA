using System.Numerics;
using Arithmetic.BigInt.Interfaces;

namespace Arithmetic.BigInt.MultiplyStrategy;

internal class FftMultiplier : IMultiplier
{
    public BetterBigInteger Multiply(BetterBigInteger a, BetterBigInteger b)
    {
        var aDigits = a.GetDigits().ToArray();
        var bDigits = b.GetDigits().ToArray();
        bool isNeg = a.IsNegative != b.IsNegative;
        // B = 2 ** 16

        double[] chunksA = CreateChunks(aDigits);
        double[] chunksB = CreateChunks(bDigits);
        
        int len = chunksA.Length + chunksB.Length - 1;
        int k = 0;
        while (Math.Pow(2, k) < len) k++;
        int n = (int)Math.Pow(2, k);
        Complex [] aComp = new Complex[n];
        Complex [] bComp = new Complex[n];
        for (int i = 0; i < n; ++i)
        {
            aComp[i] = i < chunksA.Length ? chunksA[i] : 0;
            bComp[i] = i < chunksB.Length ? chunksB[i] : 0;
        }

        Complex[] A = FFT(aComp, false);
        Complex[] B = FFT(bComp, false);

        Complex[] C = new Complex[Math.Max(A.Length, B.Length)];
        for (int i = 0; i < C.Length; ++i)
        {
            var val1 = i < A.Length ? A[i] : 0;
            var val2 = i < B.Length ? B[i] : 0;
            C[i] = val1 * val2;
        }

        Complex[] resultComplex = FFT(C, true);
        double[] coefs = resultComplex.Select(x => x.Real).ToArray();
        PropagateCarries(coefs);
        var res = FromChunks(coefs);

        return new(res, isNeg);
    }

    private static double[] CreateChunks(uint[] data)
    {
        double[] chunks = new double[data.Length * 2];
        for (int i = 0; i < data.Length; ++i)
        {
            uint value = data[i];
            chunks[2 * i] = value & 0xFFFF;
            chunks[2 * i + 1] = value >> 16;
        }

        return chunks;
    }

    private static uint[] FromChunks (double[] chunks)
    {
        uint[] res = new uint[(chunks.Length + 1) / 2];
        
        for (int i = 0; i < chunks.Length; i += 2)
        {
            uint low = (uint)Math.Round(chunks[i]);
            uint high = i + 1 < chunks.Length ? (uint)Math.Round(chunks[i + 1]) : 0u;
            res[i / 2] = (high << 16) | low;
        }

        return res;
    }

    private static void PropagateCarries(double[] chunks)
    {
        double carry = 0;
        double b = 65536;

        for (int i = 0; i < chunks.Length; ++i)
        {
            double total = Math.Round(chunks[i]) + carry;
            carry = Math.Floor(total / b);
            chunks[i] = total - carry * b;

            if (chunks[i] < 0)
            {
                chunks[i] += b;
                carry -= 1.0;
            }
        }

        if (carry > 0)
        {
            int oldLen = chunks.Length;
            int extra = 2; 
            if (carry > 65536.0) extra = (int)Math.Ceiling(Math.Log(carry, 2.0) / 16.0) + 2;
            
            Array.Resize(ref chunks, oldLen + extra);
            for (int i = oldLen; carry > 0 && i < chunks.Length; i++)
            {
                chunks[i] = carry % b;
                carry = Math.Floor(carry / b);
            }
        }
    }

    private static Complex [] FFT(Complex[] data, bool invert=false)
    {
        
    }
}