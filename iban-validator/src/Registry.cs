// Registry.cs - IBAN country registry.
// Data snapshot: SWIFT IBAN Registry, Release 93 (2023).
// Language level: C# 5.0 (compiles with the in-box .NET Framework 4.x csc.exe).

using System;
using System.Collections.Generic;

namespace IbanTool
{
    /// <summary>One country's IBAN rules: total length and BBAN structure.</summary>
    internal sealed class CountrySpec
    {
        private readonly string _code;
        private readonly string _name;
        private readonly int _length;
        private readonly string _pattern;

        public CountrySpec(string code, string name, int length, string pattern)
        {
            _code = code;
            _name = name;
            _length = length;
            _pattern = pattern;
        }

        /// <summary>ISO 3166-1 alpha-2 country code.</summary>
        public string Code { get { return _code; } }

        /// <summary>Human readable country name.</summary>
        public string Name { get { return _name; } }

        /// <summary>Total IBAN length including the country code and check digits.</summary>
        public int Length { get { return _length; } }

        /// <summary>
        /// BBAN structure in compacted registry notation: a run of digits followed by
        /// a type character. n = digits, a = upper case letters, c = letters or digits.
        /// Adjacent runs of the same type are merged, which is equivalent for validation.
        /// </summary>
        public string Pattern { get { return _pattern; } }
    }

    internal static class Registry
    {
        public const string Source = "SWIFT IBAN Registry, Release 93 (2023)";

        private static readonly Dictionary<string, CountrySpec> Map = Build();
        private static readonly List<CountrySpec> Ordered = BuildOrdered();

        public static bool TryGet(string countryCode, out CountrySpec spec)
        {
            spec = null;
            if (countryCode == null) return false;
            return Map.TryGetValue(countryCode, out spec);
        }

        public static List<CountrySpec> All()
        {
            return Ordered;
        }

        public static int Count
        {
            get { return Ordered.Count; }
        }

        private static List<CountrySpec> BuildOrdered()
        {
            List<CountrySpec> list = new List<CountrySpec>(Map.Values);
            list.Sort(delegate(CountrySpec a, CountrySpec b)
            {
                return string.CompareOrdinal(a.Code, b.Code);
            });
            return list;
        }

        private static Dictionary<string, CountrySpec> Build()
        {
            Dictionary<string, CountrySpec> m = new Dictionary<string, CountrySpec>(StringComparer.Ordinal);
            Add(m, "AD", "Andorra", 24, "8n12c");
            Add(m, "AE", "United Arab Emirates", 23, "19n");
            Add(m, "AL", "Albania", 28, "8n16c");
            Add(m, "AT", "Austria", 20, "16n");
            Add(m, "AZ", "Azerbaijan", 28, "4a20c");
            Add(m, "BA", "Bosnia and Herzegovina", 20, "16n");
            Add(m, "BE", "Belgium", 16, "12n");
            Add(m, "BG", "Bulgaria", 22, "4a6n8c");
            Add(m, "BH", "Bahrain", 22, "4a14c");
            Add(m, "BI", "Burundi", 27, "23n");
            Add(m, "BR", "Brazil", 29, "23n1a1c");
            Add(m, "BY", "Belarus", 28, "4c4n16c");
            Add(m, "CH", "Switzerland", 21, "5n12c");
            Add(m, "CR", "Costa Rica", 22, "18n");
            Add(m, "CY", "Cyprus", 28, "8n16c");
            Add(m, "CZ", "Czechia", 24, "20n");
            Add(m, "DE", "Germany", 22, "18n");
            Add(m, "DJ", "Djibouti", 27, "23n");
            Add(m, "DK", "Denmark", 18, "14n");
            Add(m, "DO", "Dominican Republic", 28, "4c20n");
            Add(m, "EE", "Estonia", 20, "16n");
            Add(m, "EG", "Egypt", 29, "25n");
            Add(m, "ES", "Spain", 24, "20n");
            Add(m, "FI", "Finland", 18, "14n");
            Add(m, "FK", "Falkland Islands", 18, "2a12n");
            Add(m, "FO", "Faroe Islands", 18, "14n");
            Add(m, "FR", "France", 27, "10n11c2n");
            Add(m, "GB", "United Kingdom", 22, "4a14n");
            Add(m, "GE", "Georgia", 22, "2a16n");
            Add(m, "GI", "Gibraltar", 23, "4a15c");
            Add(m, "GL", "Greenland", 18, "14n");
            Add(m, "GR", "Greece", 27, "7n16c");
            Add(m, "GT", "Guatemala", 28, "24c");
            Add(m, "HR", "Croatia", 21, "17n");
            Add(m, "HU", "Hungary", 28, "24n");
            Add(m, "IE", "Ireland", 22, "4a14n");
            Add(m, "IL", "Israel", 23, "19n");
            Add(m, "IQ", "Iraq", 23, "4a15n");
            Add(m, "IS", "Iceland", 26, "22n");
            Add(m, "IT", "Italy", 27, "1a10n12c");
            Add(m, "JO", "Jordan", 30, "4a4n18c");
            Add(m, "KW", "Kuwait", 30, "4a22c");
            Add(m, "KZ", "Kazakhstan", 20, "3n13c");
            Add(m, "LB", "Lebanon", 28, "4n20c");
            Add(m, "LC", "Saint Lucia", 32, "4a24c");
            Add(m, "LI", "Liechtenstein", 21, "5n12c");
            Add(m, "LT", "Lithuania", 20, "16n");
            Add(m, "LU", "Luxembourg", 20, "3n13c");
            Add(m, "LV", "Latvia", 21, "4a13c");
            Add(m, "LY", "Libya", 25, "21n");
            Add(m, "MC", "Monaco", 27, "10n11c2n");
            Add(m, "MD", "Moldova", 24, "20c");
            Add(m, "ME", "Montenegro", 22, "18n");
            Add(m, "MK", "North Macedonia", 19, "3n10c2n");
            Add(m, "MN", "Mongolia", 20, "16n");
            Add(m, "MR", "Mauritania", 27, "23n");
            Add(m, "MT", "Malta", 31, "4a5n18c");
            Add(m, "MU", "Mauritius", 30, "4a19n3a");
            Add(m, "NI", "Nicaragua", 28, "4a20n");
            Add(m, "NL", "Netherlands", 18, "4a10n");
            Add(m, "NO", "Norway", 15, "11n");
            Add(m, "OM", "Oman", 23, "3n16c");
            Add(m, "PK", "Pakistan", 24, "4a16c");
            Add(m, "PL", "Poland", 28, "24n");
            Add(m, "PS", "Palestine, State of", 29, "4a21c");
            Add(m, "PT", "Portugal", 25, "21n");
            Add(m, "QA", "Qatar", 29, "4a21c");
            Add(m, "RO", "Romania", 24, "4a16c");
            Add(m, "RS", "Serbia", 22, "18n");
            Add(m, "RU", "Russia", 33, "14n15c");
            Add(m, "SA", "Saudi Arabia", 24, "2n18c");
            Add(m, "SC", "Seychelles", 31, "4a20n3a");
            Add(m, "SD", "Sudan", 18, "14n");
            Add(m, "SE", "Sweden", 24, "20n");
            Add(m, "SI", "Slovenia", 19, "15n");
            Add(m, "SK", "Slovakia", 24, "20n");
            Add(m, "SM", "San Marino", 27, "1a10n12c");
            Add(m, "SO", "Somalia", 23, "19n");
            Add(m, "ST", "Sao Tome and Principe", 25, "21n");
            Add(m, "SV", "El Salvador", 28, "4a20n");
            Add(m, "TL", "Timor-Leste", 23, "19n");
            Add(m, "TN", "Tunisia", 24, "20n");
            Add(m, "TR", "Turkiye", 26, "6n16c");
            Add(m, "UA", "Ukraine", 29, "6n19c");
            Add(m, "VA", "Vatican City State", 22, "18n");
            Add(m, "VG", "Virgin Islands, British", 24, "4a16n");
            Add(m, "XK", "Kosovo", 20, "16n");
            return m;
        }

        private static void Add(Dictionary<string, CountrySpec> m, string code, string name, int length, string pattern)
        {
            m.Add(code, new CountrySpec(code, name, length, pattern));
        }
    }
}
