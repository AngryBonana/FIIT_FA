using Arithmetic.BigInt.Interfaces;

namespace Arithmetic.BigInt.MultiplyStrategy;

internal class FftMultiplier : IMultiplier
{
    public BetterBigInteger Multiply(BetterBigInteger a, BetterBigInteger b)
    {
        bool isNeg = a.IsNegative != b.IsNegative;
        uint[] aBytes = SplitToBytes(a.GetDigits());
        uint[] bBytes = SplitToBytes(b.GetDigits());

        int resLen = aBytes.Length + bBytes.Length - 1;
        int n = 1;
        int logN = 0;
        while (n < resLen)
        {
            n <<= 1;
            logN++;
        }

        Array.Resize(ref aBytes, n);
        Array.Resize(ref bBytes, n);

        uint[] mods = { 167772161u, 469762049u, 754974721u };
        uint[] generators = { 3u, 3u, 11u };
        
        uint[][] results = new uint[3][];

        for (int m = 0; m < 3; m++)
        {
            uint mod = mods[m];
            uint gen = generators[m];
            
            uint exp = (mod - 1) / (uint)n;
            uint w = PowMod(gen, exp, mod);
            
            uint[] A = new uint[n];
            uint[] B = new uint[n];
            Array.Copy(aBytes, A, n);
            Array.Copy(bBytes, B, n);
            
            NTT(A, mod, w);
            NTT(B, mod, w);
            
            for (int i = 0; i < n; i++)
                A[i] = MultiplyMod(A[i], B[i], mod);
            
            InverseNTT(A, mod, w);
            
            results[m] = A;
        }

        uint[] chunks = CRTReconstruct(results, resLen);

        return ConvertToBigInteger(chunks, resLen, isNeg);
    }


    private static uint[] SplitToBytes(ReadOnlySpan<uint> digits)
    {
        uint[] bytes = new uint[digits.Length * 4];
        for (int i = 0; i < digits.Length; i++)
        {
            bytes[4 * i]     = digits[i] & 0xFF;
            bytes[4 * i + 1] = (digits[i] >> 8) & 0xFF;
            bytes[4 * i + 2] = (digits[i] >> 16) & 0xFF;
            bytes[4 * i + 3] = digits[i] >> 24;
        }
        return bytes;
    }

    private static uint AddMod(uint a, uint b, uint mod)
    {
        uint sum = a + b;
        if (sum < a || sum >= mod) sum -= mod;
        return sum;
    }
    private static uint SubtractMod(uint a, uint b, uint mod)
    {
        if (a >= b) return a - b;
        return a + (mod - b);
    }
    private static uint MultiplyMod(uint a, uint b, uint mod)
    {
        uint aHigh = a >> 16;
        uint aLow = a & 0xFFFF;
        uint bHigh = b >> 16;
        uint bLow = b & 0xFFFF;

        uint p00 = bLow * aLow;
        uint p11 = bHigh * aHigh;
        uint p10 = aHigh * bLow;
        uint p01 = aLow * bHigh;

        uint low = p00 + ((p01 & 0xFFFF) << 16) + ((p10 & 0xFFFF) << 16);
        uint carry = (p00 >> 16) + (p01 & 0xFFFF) + (p10 & 0xFFFF);
        uint high = p11 + (p01 >> 16) + (p10 >> 16) + (carry >> 16);
        low = (low & 0xFFFF) | ((carry & 0xFFFF) << 16);

        return Reduce64(high, low, mod);
    }

    private static uint Reduce64(uint high, uint low, uint mod)
    {
        if (mod == 1) return 0;
        while (high != 0 || low >= mod)
        {
            if (high == 0)
            {
                low %= mod;
                return low;
            }
            
            uint q = high % mod;
            high = high / mod;
            
            uint shifted = q;
            for (int i = 0; i < 32; i++)
            {
                shifted = AddMod(shifted, shifted, mod);
            }
            low = AddMod(low, shifted, mod);
        }
        
        return low;
    }

    private static uint PowMod(uint baseVal, uint exp, uint mod)
    {
        if (mod == 1) return 0;

        uint result = 1;
        baseVal %= mod;

        while (exp > 0)
        {
            if ((exp & 1) == 1)
                result = MultiplyMod(result, baseVal, mod);
            
            baseVal = MultiplyMod(baseVal, baseVal, mod);
            exp >>= 1;
        }

        return result;
    }

    private static uint InverseMod(uint a, uint mod)
    {
        if (mod == 0) throw new DivideByZeroException();
        if (mod == 1) return 0;

        a %= mod;

        uint m0 = mod;
        uint x0 = 0;
        uint x1 = 1;
        uint a0 = a;

        while (a0 > 1)
        {
            uint q = a0 / m0;
            
            uint t = m0;
            m0 = a0 % m0;
            a0 = t;
            
            uint qx0 = MultiplyMod(q, x0, mod);
            uint xNew;
            if (x1 >= qx0)
                xNew = x1 - qx0;
            else
                xNew = x1 + (mod - qx0);
            
            t = x0;
            x0 = xNew;
            x1 = t;
        }

        return x1;
    }


    private static void NTT(uint[] data, uint mod, uint root)
    {
        int n = data.Length;

        if (n <= 1) return;

        uint[] even = new uint[n / 2];
        uint[] odd = new uint[n / 2];

        for (int i = 0; i < n/2; ++i)
        {
            even[i] = data[2 * i];
            odd[i] = data[2 * i + 1];
        }

        uint root2 = MultiplyMod(root, root, mod);

        NTT(even, mod, root2);
        NTT(odd, mod, root2);

        uint w = 1;
        for (int i = 0; i < n / 2; ++i)
        {
            uint t = MultiplyMod(w, odd[i], mod);
            data[i] = AddMod(even[i], t, mod);
            data[n / 2 + i] = SubtractMod(even[i], t, mod);
            w = MultiplyMod(w, root, mod);
        }
    }

    private static void InverseNTT(uint[] data, uint mod, uint root)
    {
        uint invRoot = InverseMod(root, mod);
        NTT(data, mod, invRoot);

        uint invN = InverseMod((uint)data.Length, mod);
        for (int i = 0; i < data.Length; ++i)
        {
            data[i] = MultiplyMod(data[i], invN, mod);
        }
    }
    private static uint[] CRTReconstruct(uint[][] results, int resultLen)
    {
        uint p1 = 167772161u;
        uint p2 = 469762049u;
        uint p3 = 754974721u;
        
        uint p1p2High, p1p2Low;
        (p1p2Low, p1p2High) = MultiplyFull(p1, p2);
        
        uint invP1ModP2 = InverseMod(p1 % p2, p2);
        
        uint p1p2ModP3 = Reduce64(p1p2High, p1p2Low, p3);
        uint invP1P2ModP3 = InverseMod(p1p2ModP3, p3);
        
        uint[] result = new uint[resultLen * 3];
        
        for (int i = 0; i < resultLen; i++)
        {
            uint r1 = results[0][i];
            uint r2 = results[1][i];
            uint r3 = results[2][i];
            
            uint diff = SubtractMod(r2, r1 % p2, p2);
            uint k = MultiplyMod(diff, invP1ModP2, p2);
            
            uint kp1High, kp1Low;
            (kp1Low, kp1High) = MultiplyFull(k, p1);
            uint carry = AddWithCarry(ref kp1Low, r1);
            uint x12High = kp1High + carry;
            uint x12Low = kp1Low;
            
            uint x12ModP3 = Reduce64(x12High, x12Low, p3);
            diff = SubtractMod(r3, x12ModP3, p3);
            uint t = MultiplyMod(diff, invP1P2ModP3, p3);
            
            uint tp1p2High, tp1p2Low;
            (tp1p2Low, tp1p2High) = MultiplyFull(t, p1p2Low);

            uint tp1p2Mid, tp1p2High2;
            (tp1p2Mid, tp1p2High2) = MultiplyFull(t, p1p2High);

            carry = AddWithCarry(ref tp1p2Low, 0);
            carry = AddWithCarry(ref tp1p2Mid, tp1p2High + carry);
            uint totalHigh = tp1p2High2 + carry;

            carry = AddWithCarry(ref tp1p2Low, x12Low);
            carry = AddWithCarry(ref tp1p2Mid, x12High + carry);
            totalHigh += carry;

            result[3 * i] = tp1p2Low;
            result[3 * i + 1] = tp1p2Mid;
            result[3 * i + 2] = totalHigh;
        }
        return result;
    }

    private static uint AddWithCarry(ref uint a, uint b)
    {
        uint sum = a + b;
        uint carry = sum < a ? 1u : 0u;
        a = sum;
        return carry;
    }

    private static (uint, uint) MultiplyFull(uint a, uint b)
    {
        uint aLow = a & 0xFFFF;
        uint aHigh = a >> 16;
        uint bLow = b & 0xFFFF;
        uint bHigh = b >> 16;

        uint lowLow = aLow * bLow;
        uint lowHigh = aLow * bHigh;
        uint highLow = aHigh * bLow;
        uint highHigh = aHigh * bHigh;

        uint low = lowLow + ((lowHigh & 0xFFFF) << 16) + ((highLow & 0xFFFF) << 16);
        uint carry = (lowLow >> 16) + (lowHigh & 0xFFFF) + (highLow & 0xFFFF);
        uint high = highHigh + (lowHigh >> 16) + (highLow >> 16) + (carry >> 16);
        low = (low & 0xFFFF) | ((carry & 0xFFFF) << 16);

        return (low, high);
    }

    private static BetterBigInteger ConvertToBigInteger(uint[] chunks, int resultLen, bool isNeg)
    {
        List<uint> bytes = new List<uint>();
        uint carry = 0;
        
        for (int i = 0; i < resultLen; i++)
        {
            uint low = chunks[3 * i];
            uint mid = chunks[3 * i + 1];
            uint high = chunks[3 * i + 2];
            
            low += carry;
            if (low < carry)
            {
                mid++;
                if (mid == 0)
                    high++;
            }
            
            bytes.Add(low & 0xFF);
            
            carry = (low >> 8) | (mid << 24);
            mid = (mid >> 8) | (high << 24);
            high >>= 8;
            
            while (mid != 0 || high != 0)
            {
                low = carry;
                
                low += 0;
                bytes.Add(low & 0xFF);
                
                carry = (low >> 8) | (mid << 24);
                mid = (mid >> 8) | (high << 24);
                high >>= 8;
            }
        }
        
        while (carry != 0)
        {
            bytes.Add(carry & 0xFF);
            carry >>= 8;
        }
        
        uint[] digits = new uint[(bytes.Count + 3) / 4];
        for (int i = 0; i < bytes.Count; i++)
        {
            int idx = i / 4;
            int shift = (i % 4) * 8;
            digits[idx] |= bytes[i] << shift;
        }
        
        int len = digits.Length;
        while (len > 1 && digits[len - 1] == 0)
            len--;
        
        uint[] result = new uint[len];
        Array.Copy(digits, result, len);
        
        return new BetterBigInteger(result, isNeg);
    }
}