// SelfTest.cs - Built in test suite, run with: iban.exe --selftest
// Language level: C# 5.0.

using System;
using System.Collections.Generic;
using System.Text;

namespace IbanTool
{
    internal static class SelfTest
    {
        // Registry example IBANs. Every one of these must validate.
        private static readonly string[] Valid = new string[]
        {
            "AD1200012030200359100100",
            "AE070331234567890123456",
            "AL47212110090000000235698741",
            "AT611904300234573201",
            "AZ21NABZ00000000137010001944",
            "BA391290079401028494",
            "BE68539007547034",
            "BG80BNBG96611020345678",
            "BH67BMAG00001299123456",
            "BR1500000000000010932840814P2",
            "BY13NBRB3600900000002Z00AB00",
            "CH9300762011623852957",
            "CR05015202001026284066",
            "CY17002001280000001200527600",
            "CZ6508000000192000145399",
            "DE89370400440532013000",
            "DK5000400440116243",
            "DO28BAGR00000001212453611324",
            "EE382200221020145685",
            "EG380019000500000000263180002",
            "ES9121000418450200051332",
            "FI2112345600000785",
            "FO6264600001631634",
            "FR1420041010050500013M02606",
            "GB82WEST12345698765432",
            "GE29NB0000000101904917",
            "GI75NWBK000000007099453",
            "GL8964710001000206",
            "GR1601101250000000012300695",
            "GT82TRAJ01020000001210029690",
            "HR1210010051863000160",
            "HU42117730161111101800000000",
            "IE29AIBK93115212345678",
            "IL620108000000099999999",
            "IQ98NBIQ850123456789012",
            "IS140159260076545510730339",
            "IT60X0542811101000000123456",
            "JO94CBJO0010000000000131000302",
            "KW81CBKU0000000000001234560101",
            "KZ86125KZT5004100100",
            "LB62099900000001001901229114",
            "LC55HEMM000100010012001200023015",
            "LI21088100002324013AA",
            "LT121000011101001000",
            "LU280019400644750000",
            "LV80BANK0000435195001",
            "LY83002048000020100120361",
            "MC5811222000010123456789030",
            "MD24AG000225100013104168",
            "ME25505000012345678951",
            "MK07250120000058984",
            "MR1300020001010000123456753",
            "MT84MALT011000012345MTLCAST001S",
            "MU17BOMM0101101030300200000MUR",
            "NL91ABNA0417164300",
            "NO9386011117947",
            "PK36SCBL0000001123456702",
            "PL61109010140000071219812874",
            "PS92PALS000000000400123456702",
            "PT50000201231234567890154",
            "QA58DOHB00001234567890ABCDEFG",
            "RO49AAAA1B31007593840000",
            "RS35260005601001611379",
            "SA0380000000608010167519",
            "SC18SSCB11010000000000001497USD",
            "SD2129010501234001",
            "SE4550000000058398257466",
            "SI56263300012039086",
            "SK3112000000198742637541",
            "SM86U0322509800000000270100",
            "ST23000200000289355710148",
            "SV62CENR00000000000000700025",
            "TL380080012345678910157",
            "TN5910006035183598478831",
            "TR330006100519786457841326",
            "UA213223130000026007233566001",
            "VA59001123000012345678",
            "VG96VPVG0000012345678901",
            "XK051212012345678906"
        };

        // Each entry is an IBAN that must be rejected, plus the reason it exists.
        private static readonly string[][] Invalid = new string[][]
        {
            new string[] { "", "empty string" },
            new string[] { "GB82WEST1234569876543", "one character short for GB" },
            new string[] { "GB82WEST123456987654321", "one character too long for GB" },
            new string[] { "GB82TEST12345698765432", "single letter changed, checksum must catch it" },
            new string[] { "GB82WEST12345698765433", "single digit changed, checksum must catch it" },
            new string[] { "GB82WEST12345698765423", "two digits transposed, checksum must catch it" },
            new string[] { "GB82WEST1234569876543!", "illegal character" },
            new string[] { "1282WEST12345698765432", "digits where the country code belongs" },
            new string[] { "GBX2WEST12345698765432", "letter inside the country code position two" },
            new string[] { "GB8AWEST12345698765432", "letter where a check digit belongs" },
            new string[] { "GB00WEST12345698765432", "reserved check digits 00" },
            new string[] { "GB01WEST12345698765432", "reserved check digits 01" },
            new string[] { "GB99WEST12345698765432", "reserved check digits 99" },
            new string[] { "GB12W3ST12345698765432", "digit inside the four letter GB bank code" },
            new string[] { "DE8937040044053201300", "wrong length for DE" },
            new string[] { "NO93860111179471", "wrong length for NO" },
            new string[] { "NL91ABNA041716430A", "letter in the all numeric part of a NL BBAN" },
            new string[] { "XX82WEST12345698765432", "unknown country code, and checksum fails too" },
            new string[] { "AA", "far too short to be anything" }
        };

        public static int Run()
        {
            int passed = 0;
            int failed = 0;

            Console.WriteLine("IBAN validator self test");
            Console.WriteLine("Registry: " + Registry.Source + " (" + Registry.Count + " countries)");
            Console.WriteLine();

            // 1. Known good IBANs.
            Console.WriteLine("[1] registry example IBANs (" + Valid.Length + ")");
            for (int i = 0; i < Valid.Length; i++)
            {
                IbanResult r = Iban.Validate(Valid[i]);
                if (r.IsValid) passed++;
                else
                {
                    failed++;
                    Console.WriteLine("    FAIL  " + Valid[i] + "  ->  " + r.ErrorCode + ": " + r.Message);
                }
            }
            Console.WriteLine("    " + (Valid.Length - failed) + "/" + Valid.Length + " accepted");
            Console.WriteLine();

            // 2. Known bad IBANs.
            int badFailed = 0;
            Console.WriteLine("[2] IBANs that must be rejected (" + Invalid.Length + ")");
            for (int i = 0; i < Invalid.Length; i++)
            {
                IbanResult r = Iban.Validate(Invalid[i][0]);
                if (!r.IsValid) passed++;
                else
                {
                    failed++;
                    badFailed++;
                    Console.WriteLine("    FAIL  accepted '" + Invalid[i][0] + "' (" + Invalid[i][1] + ")");
                }
            }
            Console.WriteLine("    " + (Invalid.Length - badFailed) + "/" + Invalid.Length + " rejected");
            Console.WriteLine();

            // 3. Round trip every country in the registry: build a synthetic account
            //    number that matches the structure, generate check digits, validate,
            //    then corrupt one character and confirm the checksum notices.
            Console.WriteLine("[3] generate and validate one IBAN per country (" + Registry.Count + ")");
            Random rng = new Random(20230101);
            List<CountrySpec> all = Registry.All();
            int roundTripFailed = 0;
            for (int i = 0; i < all.Count; i++)
            {
                CountrySpec spec = all[i];
                string bban = SynthesizeBban(spec.Pattern, rng);
                string iban = Iban.Generate(spec.Code, bban);

                IbanResult r = Iban.Validate(iban);
                if (r.IsValid && iban.Length == spec.Length) passed++;
                else
                {
                    failed++;
                    roundTripFailed++;
                    Console.WriteLine("    FAIL  " + spec.Code + " generated " + iban +
                        " (" + iban.Length + " chars, want " + spec.Length + ")  ->  " + r.Message);
                    continue;
                }

                string corrupted = Corrupt(iban, rng);
                IbanResult c = Iban.Validate(corrupted);
                if (!c.IsValid) passed++;
                else
                {
                    failed++;
                    roundTripFailed++;
                    Console.WriteLine("    FAIL  " + spec.Code + " accepted corrupted " + corrupted);
                }
            }
            Console.WriteLine("    " + (all.Count - roundTripFailed) + "/" + all.Count + " countries round tripped");
            Console.WriteLine();

            // 4. A few unit level checks on the pieces.
            Console.WriteLine("[4] unit checks");
            failed += Check("MOD 97-10 of the rearranged GB example is 1",
                Iban.Mod97("WEST12345698765432GB82") == 1, ref passed);
            failed += Check("check digits for GB + WEST12345698765432 are 82",
                Iban.ComputeCheckDigits("GB", "WEST12345698765432") == "82", ref passed);
            failed += Check("lower case and spaces normalise",
                Iban.Normalize("gb82 west 1234 5698 7654 32") == "GB82WEST12345698765432", ref passed);
            failed += Check("printed format groups in fours",
                Iban.Format("GB82WEST12345698765432") == "GB82 WEST 1234 5698 7654 32", ref passed);
            failed += Check("spaced input validates the same as unspaced",
                Iban.Validate("GB82 WEST 1234 5698 7654 32").IsValid, ref passed);
            failed += Check("hyphenated input validates",
                Iban.Validate("GB82-WEST-1234-5698-7654-32").IsValid, ref passed);
            failed += Check("null input is handled",
                !Iban.Validate(null).IsValid, ref passed);
            Console.WriteLine();

            Console.WriteLine("---------------------------------------------");
            Console.WriteLine("passed: " + passed + "   failed: " + failed);
            Console.WriteLine(failed == 0 ? "ALL TESTS PASSED" : "TESTS FAILED");
            return failed == 0 ? 0 : 1;
        }

        private static int Check(string label, bool condition, ref int passed)
        {
            if (condition)
            {
                passed++;
                Console.WriteLine("    ok    " + label);
                return 0;
            }
            Console.WriteLine("    FAIL  " + label);
            return 1;
        }

        /// <summary>Builds an account number that fits a compacted registry pattern.</summary>
        private static string SynthesizeBban(string pattern, Random rng)
        {
            const string Digits = "0123456789";
            const string Letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            const string Both = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

            StringBuilder sb = new StringBuilder();
            int p = 0;
            while (p < pattern.Length)
            {
                int count = 0;
                while (p < pattern.Length && pattern[p] >= '0' && pattern[p] <= '9')
                {
                    count = count * 10 + (pattern[p] - '0');
                    p++;
                }
                char type = pattern[p];
                p++;
                string alphabet = type == 'n' ? Digits : (type == 'a' ? Letters : Both);
                for (int k = 0; k < count; k++)
                {
                    sb.Append(alphabet[rng.Next(alphabet.Length)]);
                }
            }
            return sb.ToString();
        }

        /// <summary>Changes one BBAN character to a different one of the same class.</summary>
        private static string Corrupt(string iban, Random rng)
        {
            char[] chars = iban.ToCharArray();
            int index = 4 + rng.Next(chars.Length - 4);
            char c = chars[index];
            char replacement;
            if (c >= '0' && c <= '9')
            {
                replacement = (char)('0' + ((c - '0' + 1 + rng.Next(9)) % 10));
            }
            else
            {
                replacement = (char)('A' + ((c - 'A' + 1 + rng.Next(25)) % 26));
            }
            chars[index] = replacement;
            return new string(chars);
        }
    }
}
