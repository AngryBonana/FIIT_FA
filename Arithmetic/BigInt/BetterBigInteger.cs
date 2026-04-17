using System.ComponentModel.DataAnnotations;
using Arithmetic.BigInt.Interfaces;
using Arithmetic.BigInt.MultiplyStrategy;

namespace Arithmetic.BigInt;

public sealed class BetterBigInteger : IBigInteger
{
    private int _signBit;
    
    private uint _smallValue; // Если число маленькое, храним его прямо в этом поле, а _data == null.
    private uint[]? _data;
    
    public bool IsNegative => _signBit == 1;
    
    #region Constructors
    
    /// От массива цифр (little endian)
    public BetterBigInteger(uint[] digits, bool isNegative = false)
    {
        InitializeFromDigits(digits, isNegative);

    }
    
    public BetterBigInteger(IEnumerable<uint> digits, bool isNegative = false)
    {
        if (digits is null) throw new ArgumentNullException(nameof(digits));

        var digitsList = new List<uint>();
        foreach (var d in digits) digitsList.Add(d);

        InitializeFromDigits(digitsList.ToArray(), isNegative);
    }
    
    public BetterBigInteger(string value, int radix)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("String can't be null or WhiteSpase", nameof(value));
        if (radix < 2 || radix > 36) throw new ArgumentException("radix must be between 2 and 36", nameof(radix));

        value = value.ToUpper();
        int firstIndex = 0;
        if (value[0] == '-') 
        {
            _signBit = 1;
            firstIndex = 1;
        }
        else if (value[0] == '+')
        {
            _signBit = 0;
            firstIndex = 1;
        }
        else
        {
            _signBit = 0;
        }
        if (firstIndex == value.Length) throw new ArgumentException("No digits to parse in string", nameof(value));


        var digits = new List<uint>();
        
        for (int i = firstIndex; i < value.Length; ++i)
        {
            uint newValue = CharToValue(value[i], radix);
            MultiplyAndAddInPlace(digits, (uint)radix, newValue);
        }

        if (digits.Count == 0 || (digits.Count == 1 && digits[0] == 0))
        {
            InitializeFromDigits(new uint[] { 0 }, false);
            return;
        }
    
        InitializeFromDigits(digits.ToArray(), _signBit == 1);


    }

    private BetterBigInteger(uint[]? data, uint smallValue, int signBit)
    {
        _data = data;
        _smallValue = smallValue;
        _signBit = signBit;
    }


    private void InitializeFromDigits(uint[] digits, bool isNegative)
    {
        if (digits is null) throw new ArgumentNullException(nameof(digits));

        int nonZeroLength = digits.Length;
        while (nonZeroLength > 0 && digits[nonZeroLength - 1] == 0) 
            nonZeroLength--;

        if (nonZeroLength == 0)
        {
            _signBit = 0;
            _smallValue = 0;
            _data = null;
        }
        else if (nonZeroLength == 1)
        {
            _smallValue = digits[0];
            _signBit = (isNegative && _smallValue != 0) ? 1 : 0;
            _data = null;
        }
        else
        {
            _data = new uint[nonZeroLength];
            Array.Copy(digits, 0, _data, 0, nonZeroLength);
            _smallValue = 0;
            _signBit = isNegative ? 1 : 0;
        }
    }


    #endregion
    
    
    public ReadOnlySpan<uint> GetDigits()
    {
        return _data ?? [_smallValue];
    }
    
    public int CompareTo(IBigInteger? other)
    {
        if (other is null) return 1;

        if (IsNegative && !other.IsNegative) return -1;

        if (!IsNegative && other.IsNegative) return 1;

        var thisDigits = GetDigits();
        var otherDigits = other.GetDigits();

        if (thisDigits.Length > otherDigits.Length) return IsNegative ? -1 : 1;
        if (thisDigits.Length < otherDigits.Length) return IsNegative ? 1 : -1;

        for (int i = thisDigits.Length - 1; i >= 0; --i)
        {
            if (thisDigits[i] < otherDigits[i]) return IsNegative ? 1 : -1;
            if (thisDigits[i] > otherDigits[i]) return IsNegative ? -1 : 1;
        }

        return 0;
    }

    public bool Equals(IBigInteger? other)
    {
        if (other is null) return false;
        if (this.IsNegative != other.IsNegative) return false;
        var thisDigits = this.GetDigits();
        var otherDigits = other.GetDigits();
        if (thisDigits.Length != otherDigits.Length) return false;
        for (int i = 0; i < thisDigits.Length; ++i)
        {
            if (thisDigits[i] != otherDigits[i]) return false;
        }
        return true;
    }
    public override bool Equals(object? obj) => obj is IBigInteger other && Equals(other);
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(IsNegative);
        
        foreach (uint digit in GetDigits())
        {
            hash.Add(digit);
        }
        
        return hash.ToHashCode();
    }
    

    #region Arithmetic
    
    public static BetterBigInteger operator +(BetterBigInteger a, BetterBigInteger b)
    {
        if (a is null) throw new ArgumentNullException(nameof(a));
        if (b is null) throw new ArgumentNullException(nameof(b));

        if (a._data is null && a._smallValue == 0) return b;
        if (b._data is null && b._smallValue == 0) return a;

        if (a.IsNegative && !b.IsNegative)
        {
            if ((-a).CompareTo(b) > 0) return new(SubtractArrays(a.GetDigits(), b.GetDigits()), true);
            else return new(SubtractArrays(b.GetDigits(), a.GetDigits()), false);
        }
        else if (!a.IsNegative && b.IsNegative)
        {
            if (a.CompareTo(-b) > 0) return new(SubtractArrays(a.GetDigits(), b.GetDigits()), false);
            else return new(SubtractArrays(b.GetDigits(), a.GetDigits()), true);
        }
        else
        {
            return new(AddArrays(a.GetDigits(), b.GetDigits()), a.IsNegative);
        }
    }

    public static BetterBigInteger operator -(BetterBigInteger a, BetterBigInteger b) => a + (-b);
    
    public static BetterBigInteger operator -(BetterBigInteger a)
    {
        if (a is null) throw new ArgumentNullException(nameof(a));
        if (a._data is null && a._smallValue == 0) return a;
        return new(a._data, a._smallValue, a.IsNegative ? 0 : 1);
    }

    public static BetterBigInteger operator /(BetterBigInteger a, BetterBigInteger b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        if (b._data is null && b._smallValue == 0) throw new DivideByZeroException();

        var aDigits = a.GetDigits();
        var bDigits = b.GetDigits();
        if (AbsCompare(aDigits, bDigits) < 0) return new([0u], false);

        bool isNeg = a.IsNegative != b.IsNegative;

        
        int shift = 0;
        uint last = bDigits[bDigits.Length - 1];
        while (last < 0x80000000)
        {
            last <<= 1;
            ++shift;
        }

        var newA = (a.IsNegative ? -a : a) << shift;
        var newB = (b.IsNegative ? -b : b) << shift;

        var rawADigits = newA.GetDigits().ToArray();
        var newADigits = new uint[rawADigits.Length + 1];
        rawADigits.CopyTo(newADigits, 0);
        var newBDigits = newB.GetDigits().ToArray();

        int divisorLen = newBDigits.Length;
        int quotientLen = newADigits.Length - divisorLen; 
        uint[] quotient = new uint[quotientLen];

        for (int i = quotientLen - 1; i >= 0; --i)
        {
            int highIdx = i + divisorLen;
            uint u2 = newADigits[highIdx] >> 16;
            uint u1 = newADigits[highIdx - 1] >> 16;
            uint numerator = (u2 << 16) | u1;

            uint v1 = newBDigits[divisorLen - 1] >> 16;
            uint qHat = numerator / v1;

            int corrections = 0;
            while (true)
            {
                var prod = MultiplyByScalar(newBDigits, qHat);
                if (CompareWindows(newADigits, i, prod) <= 0) break;
                qHat--;
                if (qHat == 0 || ++corrections > 2) break;
            }

            var product = MultiplyByScalar(newBDigits, qHat);
            SubtractInPlace(newADigits, i, product);

            quotient[i] = qHat;
        }

        return new(quotient, isNeg);

    }


    public static BetterBigInteger operator %(BetterBigInteger a, BetterBigInteger b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        if (b._data is null && b._smallValue == 0) throw new DivideByZeroException();

        var aDigits = a.GetDigits();
        var bDigits = b.GetDigits();
        if (AbsCompare(aDigits, bDigits) < 0) return new([0u], false);

        bool isNeg = a.IsNegative != b.IsNegative;

        
        int shift = 0;
        uint last = bDigits[bDigits.Length - 1];
        while (last < 0x80000000)
        {
            last <<= 1;
            ++shift;
        }

        var newA = (a.IsNegative ? -a : a) << shift;
        var newB = (b.IsNegative ? -b : b) << shift;

        var rawADigits = newA.GetDigits().ToArray();
        var newADigits = new uint[rawADigits.Length + 1];
        rawADigits.CopyTo(newADigits, 0);
        var newBDigits = newB.GetDigits().ToArray();

        int divisorLen = newBDigits.Length;
        int quotientLen = newADigits.Length - divisorLen; 
        uint[] quotient = new uint[quotientLen];

        for (int i = quotientLen - 1; i >= 0; --i)
        {
            int highIdx = i + divisorLen;
            uint u2 = newADigits[highIdx] >> 16;
            uint u1 = newADigits[highIdx - 1] >> 16;
            uint numerator = (u2 << 16) | u1;

            uint v1 = newBDigits[divisorLen - 1] >> 16;
            uint qHat = numerator / v1;

            int corrections = 0;
            while (true)
            {
                var prod = MultiplyByScalar(newBDigits, qHat);
                if (CompareWindows(newADigits, i, prod) <= 0) break;
                qHat--;
                if (qHat == 0 || ++corrections > 2) break;
            }

            var product = MultiplyByScalar(newBDigits, qHat);
            SubtractInPlace(newADigits, i, product);

            quotient[i] = qHat;
        }

        var remainder = new BetterBigInteger(newADigits, a.IsNegative) >> shift;
        return remainder;

    }
    
    
    public static BetterBigInteger operator *(BetterBigInteger a, BetterBigInteger b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        return new SimpleMultiplier().Multiply(a, b);
    }
    
    #endregion

    #region Bitwise operations

    public static BetterBigInteger operator ~(BetterBigInteger a)
    {
        ArgumentNullException.ThrowIfNull(a);
        return -(a + new BetterBigInteger([1u], false));
    }

    public static BetterBigInteger operator &(BetterBigInteger a, BetterBigInteger b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        bool aNeg = a.IsNegative, bNeg = b.IsNegative;
        var aDig = a.GetDigits();
        var bDig = b.GetDigits();
        
        int len = Math.Max(aDig.Length, bDig.Length) + 1;
        
        uint[] aBits = ToTwosComplement(aDig, aNeg, len);
        uint[] bBits = ToTwosComplement(bDig, bNeg, len);

        uint[] res = new uint[len];
        for (int i = 0; i < len; i++) res[i] = aBits[i] & bBits[i];

        bool resNeg = aNeg && bNeg;

        if (resNeg)
        {
            for (int i = 0; i < len; i++) res[i] = ~res[i];
            uint carry = 1;
            for (int i = 0; i < len && carry != 0; i++)
            {
                uint sum = res[i] + carry;
                carry = (sum < res[i]) ? 1u : 0u;
                res[i] = sum;
            }
        }

        return new BetterBigInteger(res, resNeg);
    }

    public static BetterBigInteger operator |(BetterBigInteger a, BetterBigInteger b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        bool aNeg = a.IsNegative, bNeg = b.IsNegative;
        var aDigits = a.GetDigits();
        var bDigits = b.GetDigits();
        
        int len = Math.Max(aDigits.Length, bDigits.Length) + 1;
        uint[] res = new uint[len];

        uint[] aBits = ToTwosComplement(aDigits, aNeg, len);
        uint[] bBits = ToTwosComplement(bDigits, bNeg, len);

        for (int i = 0; i < len; i++) res[i] = aBits[i] | bBits[i];

        bool resNeg = aNeg || bNeg;

        if (resNeg)
        {
            for (int i = 0; i < len; i++) res[i] = ~res[i];
            uint carry = 1;
            for (int i = 0; i < len && carry != 0; i++)
            {
                uint sum = res[i] + carry;
                carry = (sum < res[i]) ? 1u : 0u;
                res[i] = sum;
            }
        }

        return new BetterBigInteger(res, resNeg);
    }

    public static BetterBigInteger operator ^(BetterBigInteger a, BetterBigInteger b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        bool aNeg = a.IsNegative, bNeg = b.IsNegative;
        var aDigits = a.GetDigits();
        var bDigits = b.GetDigits();
        
        int len = Math.Max(aDigits.Length, bDigits.Length) + 1;
        uint[] res = new uint[len];

        uint[] aBits = ToTwosComplement(aDigits, aNeg, len);
        uint[] bBits = ToTwosComplement(bDigits, bNeg, len);

        for (int i = 0; i < len; i++) res[i] = aBits[i] ^ bBits[i];

        bool resNeg = aNeg != bNeg;

        if (resNeg)
        {
            for (int i = 0; i < len; i++) res[i] = ~res[i];
            uint carry = 1;
            for (int i = 0; i < len && carry != 0; i++)
            {
                uint sum = res[i] + carry;
                carry = (sum < res[i]) ? 1u : 0u;
                res[i] = sum;
            }
        }

        return new BetterBigInteger(res, resNeg);
    }
    
    public static BetterBigInteger operator <<(BetterBigInteger a, int shift)
    {
        ArgumentNullException.ThrowIfNull(a);
        if (shift < 0) throw new ArgumentException("Shift can't be less than zero", nameof(shift));
        
        if (shift == 0) return a;
        if (a._data is null && a._smallValue == 0) return a;
        
        int digitShift = shift / 32;
        int bitShift = shift % 32;
        
        var data = a.GetDigits();
        uint[] newData = new uint[data.Length + digitShift + 1];
        
        uint carry = 0;
        for (int i = 0; i < data.Length; ++i)
        {
            uint tmpCarry = data[i] >> (32 - bitShift);
            uint newValue = data[i] << bitShift;
            newValue |= carry;
            carry = (tmpCarry == data[i]) ? 0 : tmpCarry;
            newData[i + digitShift] = newValue;
        }
        if (carry != 0) newData[newData.Length - 1] = carry;

        return new(newData, a.IsNegative);
    }

    public static BetterBigInteger operator >> (BetterBigInteger a, int shift)
    {
        ArgumentNullException.ThrowIfNull(a);
        if (shift < 0) throw new ArgumentException("Shift can't be less than zero", nameof(shift));
        if (shift == 0) return a;
        if (a._data is null && a._smallValue == 0) return a;

        int digitShift = shift / 32;
        int bitShift = shift % 32;
        var data = a.GetDigits();

        if (digitShift >= data.Length)
            return a.IsNegative ? new BetterBigInteger([1u], true) : new BetterBigInteger([0u], false);

        if (a.IsNegative)
        {
            var absMinusOne = (-a) - new BetterBigInteger([1u], false);
            return -(absMinusOne >> shift) - new BetterBigInteger([1u], false);
        }

        uint[] newData = new uint[data.Length - digitShift];
        uint carry = 0;

        if (bitShift == 0)
        {
            for (int i = 0; i < newData.Length; ++i)
                newData[i] = data[i + digitShift];
        }
        else
        {
            int invShift = 32 - bitShift;
            for (int i = data.Length - 1; i >= 0; --i)
            {
                uint current = (data[i] >> bitShift) | carry;
                carry = data[i] << invShift;
                if (i >= digitShift)
                    newData[i - digitShift] = current;
            }
        }

        return new BetterBigInteger(newData, false);
    }

    #endregion

    #region Compartion
    
    public static bool operator ==(BetterBigInteger a, BetterBigInteger b) => Equals(a, b);
    public static bool operator !=(BetterBigInteger a, BetterBigInteger b) => !Equals(a, b);
    public static bool operator <(BetterBigInteger a, BetterBigInteger b) => a.CompareTo(b) < 0;
    public static bool operator >(BetterBigInteger a, BetterBigInteger b) => a.CompareTo(b) > 0;
    public static bool operator <=(BetterBigInteger a, BetterBigInteger b) => a.CompareTo(b) <= 0;
    public static bool operator >=(BetterBigInteger a, BetterBigInteger b) => a.CompareTo(b) >= 0;

    #endregion
    
    #region Helpers

    public override string ToString() => ToString(10);

    public string ToString(int radix)
    {
        if (radix < 2 || radix > 36) throw new ArgumentOutOfRangeException(nameof(radix), "Radix must be between 2 and 36.");
        
        if ((_data == null && _smallValue == 0) || (_data != null && _data.All(d => d == 0)))
        {
            return "0";
        }
        
        bool isNegative = IsNegative;
        var digits = GetDigits();
        
        var resultChars = new List<char>(digits.Length * 10 + 2);
        
        var currentDigits = digits.ToArray();
        
        while (currentDigits.Length > 0 && !(currentDigits.Length == 1 && currentDigits[0] == 0))
        {
            var (quotient, remainder) = DivideBySmall(currentDigits, (uint)radix);
            
            resultChars.Add(ValueToChar(remainder, radix));
            
            currentDigits = quotient.ToArray();
        }
        
        resultChars.Reverse();
        
        if (isNegative)
            resultChars.Insert(0, '-');
        
        return new string(resultChars.ToArray());
    }

    private static (List<uint> Quotient, uint Remainder) DivideBySmall(uint[] digits, uint divisor)
    {
        if (digits == null || digits.Length == 0)
            return (new List<uint> { 0 }, 0);
        
        ulong remainder = 0;
        var quotient = new List<uint>(digits.Length);
        
        for (int i = digits.Length - 1; i >= 0; i--)
        {
            ulong current = (remainder << 32) | digits[i];
            
            quotient.Add((uint)(current / divisor));
            remainder = current % divisor;
        }
        
        quotient.Reverse();
        
        while (quotient.Count > 1 && quotient[^1] == 0)
            quotient.RemoveAt(quotient.Count - 1);
        
        return (quotient, (uint)remainder);
    }

    private static char ValueToChar(uint value, int radix)
    {
        if (value >= radix)
            throw new ArgumentException($"Digit value {value} out of range for radix {radix}");
        
        return value < 10 ? (char)('0' + value) : (char)('A' + value - 10);
    }
    
    private static uint CharToValue(char c, int radix)
    {
        uint value;
        if (c >= '0' && c <= '9')
            value = (uint)(c - '0');
        else if (c >= 'A' && c <= 'Z')
            value = (uint)(c - 'A' + 10);
        else
            throw new FormatException($"Invalid character '{c}' for radix {radix}");
        
        if (value >= radix)
            throw new FormatException($"Digit '{c}' out of range for radix {radix}");
        
        return value;
    }


    private static void MultiplyAndAddInPlace(List<uint> digits, uint multiplier, uint addend)
    {
        ulong carry = addend;
        ulong mult = multiplier;
        
        for (int i = 0; i < digits.Count; i++)
        {
            carry += mult * digits[i];
            digits[i] = (uint)carry;
            carry >>= 32;
        }
        
        while (carry != 0)
        {
            digits.Add((uint)carry);
            carry >>= 32;
        }
        
    }

    private static uint[] SubtractArrays (ReadOnlySpan<uint> more, ReadOnlySpan<uint> less)
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

    private static uint[] AddArrays (ReadOnlySpan<uint> first, ReadOnlySpan<uint> second)
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

    private static uint[] ToTwosComplement(ReadOnlySpan<uint> mag, bool isNeg, int len)
    {
        uint[] bits = new uint[len];
        uint carry = isNeg ? 1u : 0u; 

        for (int i = 0; i < len; i++)
        {
            uint m = i < mag.Length ? mag[i] : 0;
            if (isNeg)
            {
                uint inv = ~m;
                uint sum = inv + carry;
                carry = (sum < inv) ? 1u : 0u; 
                bits[i] = sum;
            }
            else
            {
                bits[i] = m;
            }
        }
        return bits;
    }

    private static int AbsCompare(ReadOnlySpan<uint> a, ReadOnlySpan<uint> b)
    {
        if (a.Length > b.Length) return 1;
        if (a.Length < b.Length) return -1;

        for (int i = a.Length - 1; i >= 0; --i)
        {
            if (a[i] < b[i]) return -1;
            if (a[i] > b[i]) return 1;
        }

        return 0;
    }

    private static uint[] MultiplyByScalar(uint[] digits, uint scalar)
    {
        if (scalar == 0) return new uint[] { 0 };
        if (scalar == 1) return (uint[])digits.Clone();
        
        uint[] res = new uint[digits.Length + 1];
        ulong carry = 0;
        for (int i = 0; i < digits.Length; i++)
        {
            carry += (ulong)digits[i] * scalar;
            res[i] = (uint)carry;
            carry >>= 32;
        }
        res[digits.Length] = (uint)carry;
        
        int len = res.Length;
        while (len > 1 && res[len - 1] == 0) len--;
        if (len < res.Length) Array.Resize(ref res, len);
        return res;
    }

    private static int CompareWindows(uint[] dividend, int offset, uint[] product)
    {
        // Сравниваем от старших разрядов
        for (int i = product.Length - 1; i >= 0; i--)
        {
            uint d = (offset + i < dividend.Length) ? dividend[offset + i] : 0;
            uint p = product[i];
            if (d < p) return -1;
            if (d > p) return 1;
        }
        return 0;
    }

    private static void SubtractInPlace(uint[] dividend, int offset, uint[] product)
    {
        uint borrow = 0;
        for (int j = 0; j < product.Length; j++)
        {
            int idx = offset + j;
            
            if (idx >= dividend.Length) continue;

            uint d = dividend[idx];
            uint sub = product[j] + borrow;
            uint result = d - sub;
            borrow = (sub > d) ? 1u : 0u;
            dividend[idx] = result;
        }
    }

    #endregion
}