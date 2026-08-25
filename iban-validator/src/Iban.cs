// Iban.cs - Core IBAN validation (ISO 13616) and check digit generation (ISO 7064 MOD 97-10).
// Language level: C# 5.0.

using System;
using System.Text;

namespace IbanTool
{
    internal enum IbanError
    {
        None = 0,
        Empty,
        InvalidCharacter,
        TooShort,
        TooLong,
        BadCountryFormat,
        UnknownCountry,
        WrongLength,
        BadCheckDigitFormat,
        ReservedCheckDigits,
        BbanStructure,
        ChecksumFailed
    }

    /// <summary>Outcome of validating a single candidate IBAN.</summary>
    internal sealed class IbanResult
    {
        public string Input;
        public string Electronic;      // normalised, no spaces, upper case
        public string Printed;         // grouped in fours for printing
        public string CountryCode;
        public string CountryName;
        public string CheckDigits;
        public string Bban;
        public int Length;
        public bool IsValid;
        public IbanError Error;
        public string Message;

        public string ErrorCode
        {
            get { return Error.ToString(); }
        }
    }

    internal static class Iban
    {
        public const int MinLength = 15;
        public const int MaxLength = 34;

        /// <summary>Strips spaces, tabs and the common separators, and upper cases the rest.</summary>
        public static string Normalize(string input)
        {
            if (input == null) return string.Empty;
            StringBuilder sb = new StringBuilder(input.Length);
            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                if (c == ' ' || c == '\t' || c == '-' || c == '.' || c == ' ') continue;
                sb.Append(char.ToUpperInvariant(c));
            }
            return sb.ToString();
        }

        /// <summary>Formats an IBAN in space separated groups of four, the paper format.</summary>
        public static string Format(string electronic)
        {
            if (string.IsNullOrEmpty(electronic)) return string.Empty;
            StringBuilder sb = new StringBuilder(electronic.Length + electronic.Length / 4);
            for (int i = 0; i < electronic.Length; i++)
            {
                if (i > 0 && i % 4 == 0) sb.Append(' ');
                sb.Append(electronic[i]);
            }
            return sb.ToString();
        }

        /// <summary>
        /// ISO 7064 MOD 97-10 over the rearranged IBAN. Letters expand to two digits
        /// (A=10 .. Z=35), so the running remainder is scaled by 100 for a letter and
        /// by 10 for a digit. Avoids big integers entirely.
        /// </summary>
        public static int Mod97(string rearranged)
        {
            int remainder = 0;
            for (int i = 0; i < rearranged.Length; i++)
            {
                char c = rearranged[i];
                if (c >= '0' && c <= '9')
                {
                    remainder = (remainder * 10 + (c - '0')) % 97;
                }
                else if (c >= 'A' && c <= 'Z')
                {
                    remainder = (remainder * 100 + (c - 'A' + 10)) % 97;
                }
                else
                {
                    return -1;
                }
            }
            return remainder;
        }

        /// <summary>Moves the first four characters to the end, as ISO 13616 requires.</summary>
        private static string Rearrange(string electronic)
        {
            return electronic.Substring(4) + electronic.Substring(0, 4);
        }

        /// <summary>Validates a candidate IBAN and explains the first rule it breaks.</summary>
        public static IbanResult Validate(string input)
        {
            IbanResult r = new IbanResult();
            r.Input = input == null ? string.Empty : input;
            r.Electronic = Normalize(input);
            r.Length = r.Electronic.Length;
            r.Printed = Format(r.Electronic);

            if (r.Length == 0)
                return Fail(r, IbanError.Empty, "empty input");

            // 1. Character set: digits and upper case letters only.
            for (int i = 0; i < r.Electronic.Length; i++)
            {
                char c = r.Electronic[i];
                bool ok = (c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z');
                if (!ok)
                {
                    return Fail(r, IbanError.InvalidCharacter, string.Format(
                        "illegal character '{0}' at position {1}; an IBAN holds only A-Z and 0-9",
                        c, i + 1));
                }
            }

            // 2. Absolute length limits.
            if (r.Length < MinLength)
                return Fail(r, IbanError.TooShort, string.Format(
                    "{0} characters; no IBAN is shorter than {1}", r.Length, MinLength));
            if (r.Length > MaxLength)
                return Fail(r, IbanError.TooLong, string.Format(
                    "{0} characters; no IBAN is longer than {1}", r.Length, MaxLength));

            // 3. Shape of the first four characters: two letters then two digits.
            r.CountryCode = r.Electronic.Substring(0, 2);
            r.CheckDigits = r.Electronic.Substring(2, 2);
            r.Bban = r.Electronic.Substring(4);

            if (!IsUpperAlpha(r.CountryCode[0]) || !IsUpperAlpha(r.CountryCode[1]))
                return Fail(r, IbanError.BadCountryFormat, string.Format(
                    "'{0}' is not a two letter country code", r.CountryCode));

            // Resolve the country now so the name is reported even when a later rule fails.
            CountrySpec spec;
            bool known = Registry.TryGet(r.CountryCode, out spec);
            r.CountryName = known ? spec.Name : "not in registry";

            if (!IsDigit(r.CheckDigits[0]) || !IsDigit(r.CheckDigits[1]))
                return Fail(r, IbanError.BadCheckDigitFormat, string.Format(
                    "check digits '{0}' are not two digits", r.CheckDigits));

            // 00, 01 and 99 can never come out of the MOD 97-10 algorithm.
            if (r.CheckDigits == "00" || r.CheckDigits == "01" || r.CheckDigits == "99")
                return Fail(r, IbanError.ReservedCheckDigits, string.Format(
                    "check digits '{0}' are never produced by MOD 97-10 (valid range is 02-98)",
                    r.CheckDigits));

            // 4. Country specific length and BBAN structure.
            if (known)
            {
                if (r.Length != spec.Length)
                    return Fail(r, IbanError.WrongLength, string.Format(
                        "{0} requires {1} characters, got {2}", spec.Code, spec.Length, r.Length));

                string structureProblem = CheckStructure(r.Bban, spec.Pattern);
                if (structureProblem != null)
                    return Fail(r, IbanError.BbanStructure, structureProblem);
            }

            // 5. The checksum itself.
            int remainder = Mod97(Rearrange(r.Electronic));
            if (remainder != 1)
            {
                string expected = ComputeCheckDigits(r.CountryCode, r.Bban);
                return Fail(r, IbanError.ChecksumFailed, string.Format(
                    "MOD 97-10 checksum failed (remainder {0}, expected 1); correct check digits for this account number are '{1}'",
                    remainder, expected));
            }

            r.IsValid = true;
            r.Error = IbanError.None;
            r.Message = known
                ? "valid"
                : string.Format("checksum valid, but '{0}' is not in the {1}", r.CountryCode, Registry.Source);
            return r;
        }

        /// <summary>Builds a complete IBAN from a country code and a BBAN.</summary>
        public static string Generate(string countryCode, string bban)
        {
            string cc = Normalize(countryCode);
            string b = Normalize(bban);
            string check = ComputeCheckDigits(cc, b);
            return cc + check + b;
        }

        /// <summary>The two check digits that make a country code plus BBAN valid.</summary>
        public static string ComputeCheckDigits(string countryCode, string bban)
        {
            int remainder = Mod97(bban + countryCode + "00");
            if (remainder < 0) return "??";
            int check = 98 - remainder;
            return check.ToString("00");
        }

        /// <summary>
        /// Walks the BBAN against the compacted registry pattern, for example "4a14n".
        /// Returns null when it matches, or a message naming the offending position.
        /// </summary>
        private static string CheckStructure(string bban, string pattern)
        {
            int pos = 0;   // index into bban
            int p = 0;     // index into pattern

            while (p < pattern.Length)
            {
                int count = 0;
                while (p < pattern.Length && pattern[p] >= '0' && pattern[p] <= '9')
                {
                    count = count * 10 + (pattern[p] - '0');
                    p++;
                }
                if (p >= pattern.Length) return "malformed structure pattern in registry";
                char type = pattern[p];
                p++;

                for (int k = 0; k < count; k++)
                {
                    if (pos >= bban.Length)
                        return "account number is shorter than the country's structure requires";

                    char c = bban[pos];
                    bool ok;
                    string want;
                    if (type == 'n') { ok = IsDigit(c); want = "a digit"; }
                    else if (type == 'a') { ok = IsUpperAlpha(c); want = "a letter"; }
                    else { ok = IsDigit(c) || IsUpperAlpha(c); want = "a letter or digit"; }

                    if (!ok)
                        return string.Format(
                            "structure mismatch at position {0} of the IBAN: '{1}' found where the country requires {2}",
                            pos + 5, c, want);

                    pos++;
                }
            }

            if (pos != bban.Length)
                return "account number is longer than the country's structure allows";

            return null;
        }

        private static IbanResult Fail(IbanResult r, IbanError error, string message)
        {
            r.IsValid = false;
            r.Error = error;
            r.Message = message;
            return r;
        }

        private static bool IsDigit(char c)
        {
            return c >= '0' && c <= '9';
        }

        private static bool IsUpperAlpha(char c)
        {
            return c >= 'A' && c <= 'Z';
        }
    }
}
