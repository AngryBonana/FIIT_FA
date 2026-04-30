using System.ComponentModel.DataAnnotations;
using Arithmetic.BigInt.Interfaces;

namespace Arithmetic.BigInt.MultiplyStrategy;

internal class KaratsubaMultiplier : IMultiplier
{
    public BetterBigInteger Multiply(BetterBigInteger a, BetterBigInteger b)
    {
        bool isNeg = a.IsNegative != b.IsNegative;
        uint[] aDigits = a.GetDigits().ToArray();
        uint[] bDigits = b.GetDigits().ToArray();

        return new(MultiplyArrays(aDigits, bDigits), isNeg);
    }

    private static uint[] MultiplyArrays(uint[] a, uint[] b)
    {
        int maxLen = Math.Max(a.Length, b.Length);
        if (maxLen < 8) return SimpleMultiply(a, b);
        
        int m = maxLen / 2;
        int highLen = maxLen - m;

        uint[] aHigh = a.Length > m? new uint[highLen] : [0u];
        if (a.Length > m) Array.Copy(a, m, aHigh, 0, highLen);
        uint[] bHigh = b.Length > m ? new uint[highLen] : [0u];
        if (b.Length > m) Array.Copy(b, m, bHigh, 0, highLen);
        uint[] aLow = new uint[Math.Min(m, a.Length)];
        Array.Copy(a, 0, aLow, 0, aLow.Length);
        uint[] bLow = new uint[Math.Min(m, b.Length)];
        Array.Copy(b, 0, bLow, 0, bLow.Length);

        var z0 = MultiplyArrays(aLow, bLow);
        var z2 = MultiplyArrays(aHigh, bHigh);
        var z1 = SubtractArrays(SubtractArrays(MultiplyArrays(AddArrays(aLow, aHigh), AddArrays(bLow, bHigh)), z0), z2);

        uint[] z2Normal = new uint[z2.Length + 2 * m];
        Array.Copy(z2, 0, z2Normal, 2 * m, z2.Length);
        uint[] z1Normal = new uint[z1.Length + m];
        Array.Copy(z1, 0, z1Normal, m, z1.Length);

        uint[] res = AddArrays(AddArrays(z2Normal, z1Normal), z0);
        return res;
    }

    private static uint[] SimpleMultiply(uint[] a, uint[] b)
    {
        uint[] res = new uint[a.Length + b.Length];

        for (int i = 0; i < a.Length; i++)
        {
            uint carry = 0;
            uint aVal = a[i];
            uint a0 = aVal & 0xFFFF, a1 = aVal >> 16;

            for (int j = 0; j < b.Length; j++)
            {
                uint bVal = b[j];
                uint b0 = bVal & 0xFFFF, b1 = bVal >> 16;

                uint p00 = b0 * a0;
                uint p11 = a1 * b1;
                uint p01 = a0 * b1;
                uint p10 = a1 * b0;

                uint c0 = p00 & 0xFFFF;
                uint c1 = (p00 >> 16) + (p01 & 0xFFFF) + (p10 & 0xFFFF);
                uint c2 = (p01 >> 16) + (p10 >> 16) + (p11 & 0xFFFF) + (c1 >> 16);
                uint c3 = (p11 >> 16) + (c2 >> 16);

                uint low  = c0 | (c1 << 16);
                uint high = (c2 & 0xFFFF) | (c3 << 16);

                uint cur = res[i + j];
                uint sum = cur + low;
                uint carry1 = (sum < cur) ? 1u : 0u;

                uint total = sum + carry;
                uint carry2 = (total < sum) ? 1u : 0u;

                res[i + j] = total;

                carry = high + carry1 + carry2; 
            }
            res[i + b.Length] = carry;
        }

        return res;
    }

    private static uint[] AddArrays(uint[] first, uint[] second)
    {
        uint[] ans = new uint[((first.Length > second.Length) ? first.Length : second.Length) + 1];

        uint carry = 0;
        for (int i = 0; i < ans.Length - 1; ++i)
        {
            uint f = (i < first.Length) ? first[i] : 0;
            uint s = (i < second.Length) ? second[i] : 0;

            uint sum = f + s;
            bool overflow1 = sum < f;

            sum += carry;
            bool overflow2 = sum < carry;

            ans[i] = sum;
            carry = (overflow1 || overflow2) ? 1u : 0u;
        }
        ans[ans.Length - 1] = carry;
        return ans;
    }

    private static uint[] SubtractArrays (uint[] more, uint[] less)
    {
        uint[] ans = new uint[more.Length];
        uint borrow = 0;

        for (int i = 0; i < more.Length; i++)
        {
            uint l = (i < less.Length) ? less[i] : 0;

            uint val = more[i] - borrow;
            
            bool borrowedFromThis = borrow > more[i];
            
            uint diff = val - l;
            
            bool borrowedForL = val < l;

            ans[i] = diff;

            borrow = (borrowedFromThis || borrowedForL) ? 1u : 0u;
        }
        
        return ans;
    }
}