// Program.cs - Command line front end.
// Language level: C# 5.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace IbanTool
{
    internal static class Program
    {
        public const string Version = "1.0.0";

        private const int ExitAllValid = 0;
        private const int ExitInvalidFound = 1;
        private const int ExitUsageError = 2;

        private static bool _quiet;
        private static bool _json;
        private static bool _color = true;

        public static int Main(string[] args)
        {
            try
            {
                return Run(args);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("error: " + ex.Message);
                return ExitUsageError;
            }
        }

        private static int Run(string[] args)
        {
            List<string> inputs = new List<string>();
            List<string> generate = new List<string>();
            bool wantGenerate = false;

            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];

                if (a == "-h" || a == "--help" || a == "/?")
                {
                    PrintHelp();
                    return ExitAllValid;
                }
                if (a == "-v" || a == "--version")
                {
                    Console.WriteLine("iban " + Version + "  (" + Registry.Source + ", " + Registry.Count + " countries)");
                    return ExitAllValid;
                }
                if (a == "-q" || a == "--quiet") { _quiet = true; continue; }
                if (a == "--json") { _json = true; continue; }
                if (a == "--no-color" || a == "--no-colour") { _color = false; continue; }
                if (a == "--list")
                {
                    PrintCountryList();
                    return ExitAllValid;
                }
                if (a == "--selftest" || a == "--self-test")
                {
                    return SelfTest.Run();
                }
                if (a == "-g" || a == "--generate")
                {
                    wantGenerate = true;
                    continue;
                }
                if (a == "-f" || a == "--file")
                {
                    if (i + 1 >= args.Length)
                    {
                        Console.Error.WriteLine("error: --file needs a path");
                        return ExitUsageError;
                    }
                    i++;
                    string path = args[i];
                    if (!File.Exists(path))
                    {
                        Console.Error.WriteLine("error: no such file: " + path);
                        return ExitUsageError;
                    }
                    foreach (string line in File.ReadAllLines(path))
                    {
                        string t = line.Trim();
                        if (t.Length == 0 || t.StartsWith("#")) continue;
                        inputs.Add(t);
                    }
                    continue;
                }
                if (a.Length > 1 && a[0] == '-' && a != "-")
                {
                    Console.Error.WriteLine("error: unknown option " + a);
                    Console.Error.WriteLine("try: iban --help");
                    return ExitUsageError;
                }

                if (wantGenerate) generate.Add(a);
                else inputs.Add(a);
            }

            if (_color && Console.IsOutputRedirected) _color = false;
            if (_json) _color = false;

            if (wantGenerate)
            {
                return RunGenerate(generate);
            }

            // Piped input, for example: type ibans.txt | iban.exe
            if (inputs.Count == 0 && Console.IsInputRedirected)
            {
                string line;
                while ((line = Console.In.ReadLine()) != null)
                {
                    string t = line.Trim();
                    if (t.Length == 0 || t.StartsWith("#")) continue;
                    inputs.Add(t);
                }
            }

            // Nothing on the command line and a real console attached: prompt.
            if (inputs.Count == 0)
            {
                return RunInteractive();
            }

            return ValidateAll(JoinFragments(inputs));
        }

        /// <summary>
        /// A shell splits "iban GB82 WEST 1234 5698 7654 32" into six arguments. No real
        /// IBAN is under 15 characters, so when every argument is shorter than that they
        /// are the groups of one IBAN rather than several IBANs.
        /// </summary>
        private static List<string> JoinFragments(List<string> inputs)
        {
            if (inputs.Count < 2) return inputs;
            for (int i = 0; i < inputs.Count; i++)
            {
                if (Iban.Normalize(inputs[i]).Length >= Iban.MinLength) return inputs;
            }
            List<string> joined = new List<string>(1);
            joined.Add(string.Join(" ", inputs.ToArray()));
            return joined;
        }

        private static int RunGenerate(List<string> parts)
        {
            if (parts.Count < 2)
            {
                Console.Error.WriteLine("error: --generate needs a country code and an account number");
                Console.Error.WriteLine("example: iban --generate GB WEST12345698765432");
                return ExitUsageError;
            }

            string cc = Iban.Normalize(parts[0]);
            StringBuilder bban = new StringBuilder();
            for (int i = 1; i < parts.Count; i++) bban.Append(Iban.Normalize(parts[i]));

            string iban = Iban.Generate(cc, bban.ToString());
            IbanResult r = Iban.Validate(iban);

            if (_json)
            {
                Console.WriteLine(ToJson(r));
            }
            else if (_quiet)
            {
                Console.WriteLine(r.Electronic);
            }
            else
            {
                Console.WriteLine("electronic : " + r.Electronic);
                Console.WriteLine("printed    : " + r.Printed);
                Console.WriteLine("status     : " + (r.IsValid ? "valid" : "INVALID - " + r.Message));
            }
            return r.IsValid ? ExitAllValid : ExitInvalidFound;
        }

        private static int RunInteractive()
        {
            Console.WriteLine("iban " + Version + " - type an IBAN and press Enter. Blank line or 'quit' exits.");
            Console.WriteLine();
            int worst = ExitAllValid;
            while (true)
            {
                Console.Write("> ");
                string line = Console.ReadLine();
                if (line == null) break;
                string t = line.Trim();
                if (t.Length == 0) break;
                if (string.Equals(t, "quit", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(t, "exit", StringComparison.OrdinalIgnoreCase)) break;

                IbanResult r = Iban.Validate(t);
                PrintResult(r);
                Console.WriteLine();
                if (!r.IsValid) worst = ExitInvalidFound;
            }
            return worst;
        }

        private static int ValidateAll(List<string> inputs)
        {
            List<IbanResult> results = new List<IbanResult>(inputs.Count);
            int invalid = 0;

            for (int i = 0; i < inputs.Count; i++)
            {
                IbanResult r = Iban.Validate(inputs[i]);
                results.Add(r);
                if (!r.IsValid) invalid++;
            }

            if (_json)
            {
                Console.WriteLine("[");
                for (int i = 0; i < results.Count; i++)
                {
                    Console.Write("  " + ToJson(results[i]));
                    Console.WriteLine(i == results.Count - 1 ? "" : ",");
                }
                Console.WriteLine("]");
            }
            else if (!_quiet)
            {
                for (int i = 0; i < results.Count; i++)
                {
                    PrintResult(results[i]);
                }
                if (results.Count > 1)
                {
                    Console.WriteLine();
                    Console.WriteLine(string.Format("{0} checked, {1} valid, {2} invalid",
                        results.Count, results.Count - invalid, invalid));
                }
            }

            return invalid == 0 ? ExitAllValid : ExitInvalidFound;
        }

        private static void PrintResult(IbanResult r)
        {
            string label = r.IsValid ? "VALID  " : "INVALID";
            WriteColored(label, r.IsValid ? ConsoleColor.Green : ConsoleColor.Red);
            Console.WriteLine("  " + (r.Printed.Length > 0 ? r.Printed : "(empty)"));

            if (r.IsValid)
            {
                Console.WriteLine(string.Format("         {0} ({1})  |  {2} chars  |  check digits {3}",
                    r.CountryName, r.CountryCode, r.Length, r.CheckDigits));
                Console.WriteLine("         BBAN " + r.Bban);
                if (r.Message != "valid")
                {
                    Console.WriteLine("         note: " + r.Message);
                }
            }
            else
            {
                Console.WriteLine("         " + r.Message);
                Console.WriteLine("         reason code: " + r.ErrorCode);
            }
        }

        private static void WriteColored(string text, ConsoleColor color)
        {
            if (!_color)
            {
                Console.Write(text);
                return;
            }
            ConsoleColor previous = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.Write(text);
            Console.ForegroundColor = previous;
        }

        private static string ToJson(IbanResult r)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("{");
            sb.Append("\"input\":").Append(Quote(r.Input)).Append(",");
            sb.Append("\"valid\":").Append(r.IsValid ? "true" : "false").Append(",");
            sb.Append("\"iban\":").Append(Quote(r.Electronic)).Append(",");
            sb.Append("\"printed\":").Append(Quote(r.Printed)).Append(",");
            sb.Append("\"country_code\":").Append(Quote(r.CountryCode)).Append(",");
            sb.Append("\"country\":").Append(Quote(r.CountryName)).Append(",");
            sb.Append("\"check_digits\":").Append(Quote(r.CheckDigits)).Append(",");
            sb.Append("\"bban\":").Append(Quote(r.Bban)).Append(",");
            sb.Append("\"length\":").Append(r.Length).Append(",");
            sb.Append("\"reason_code\":").Append(Quote(r.ErrorCode)).Append(",");
            sb.Append("\"message\":").Append(Quote(r.Message));
            sb.Append("}");
            return sb.ToString();
        }

        private static string Quote(string s)
        {
            if (s == null) return "null";
            StringBuilder sb = new StringBuilder(s.Length + 2);
            sb.Append('"');
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }

        private static void PrintCountryList()
        {
            List<CountrySpec> all = Registry.All();
            Console.WriteLine(Registry.Source);
            Console.WriteLine(all.Count + " countries and territories");
            Console.WriteLine();
            Console.WriteLine("code  len  BBAN structure  country");
            Console.WriteLine("----  ---  --------------  -------");
            for (int i = 0; i < all.Count; i++)
            {
                CountrySpec s = all[i];
                Console.WriteLine(string.Format("{0}    {1,3}  {2,-14}  {3}",
                    s.Code, s.Length, s.Pattern, s.Name));
            }
            Console.WriteLine();
            Console.WriteLine("structure: n = digits, a = letters A-Z, c = letters or digits");
        }

        private static void PrintHelp()
        {
            Console.WriteLine("iban " + Version + " - International Bank Account Number validator");
            Console.WriteLine();
            Console.WriteLine("USAGE");
            Console.WriteLine("  iban <IBAN> [IBAN ...]        validate one or more IBANs");
            Console.WriteLine("  iban --file <path>            validate one IBAN per line from a file");
            Console.WriteLine("  type list.txt | iban          validate piped input");
            Console.WriteLine("  iban                          interactive prompt");
            Console.WriteLine("  iban --generate <CC> <BBAN>   build a valid IBAN by computing check digits");
            Console.WriteLine();
            Console.WriteLine("OPTIONS");
            Console.WriteLine("  -q, --quiet       print nothing, report through the exit code");
            Console.WriteLine("      --json        machine readable output");
            Console.WriteLine("      --no-color    plain text, no ANSI colours");
            Console.WriteLine("      --list        list every country in the registry");
            Console.WriteLine("      --selftest    run the built in test suite");
            Console.WriteLine("  -v, --version     print the version");
            Console.WriteLine("  -h, --help        this text");
            Console.WriteLine();
            Console.WriteLine("EXIT CODES");
            Console.WriteLine("  0  every IBAN checked was valid");
            Console.WriteLine("  1  at least one IBAN was invalid");
            Console.WriteLine("  2  usage or file error");
            Console.WriteLine();
            Console.WriteLine("WHAT IT CHECKS");
            Console.WriteLine("  1. character set is A-Z and 0-9 only");
            Console.WriteLine("  2. length is 15 to 34, and matches the country's fixed length");
            Console.WriteLine("  3. first two characters are a country code, next two are digits");
            Console.WriteLine("  4. the account number matches the country's BBAN structure");
            Console.WriteLine("  5. ISO 7064 MOD 97-10 checksum over the rearranged IBAN equals 1");
            Console.WriteLine();
            Console.WriteLine("EXAMPLES");
            Console.WriteLine("  iban GB82 WEST 1234 5698 7654 32");
            Console.WriteLine("  iban DE89370400440532013000 NL91ABNA0417164300");
            Console.WriteLine("  iban --generate GB WEST12345698765432");
            Console.WriteLine("  iban --json DE89370400440532013000");
            Console.WriteLine();
            Console.WriteLine("Data: " + Registry.Source);
        }
    }
}
