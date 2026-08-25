# iban.exe

Final changes 12/9/2023

## How this program works

Five rules are enforced:

1. **Character set** — `A-Z` and `0-9` only, after spaces and hyphens are stripped.
2. **Length** — 15 to 34 overall, and exactly the fixed length that country uses.
3. **Shape** — two letters of country code, then two digits of check digits.
4. **BBAN structure** — the account number matches the country's layout. A UK IBAN
   must have four *letters* of bank code; a German one must be eighteen digits.
5. **Checksum** — ISO 7064 MOD 97-10 over the rearranged IBAN must equal 1.

It also rejects check digits `00`, `01` and `99`, which MOD 97-10 can never produce.

The registry covers **87 countries and territories**-  country outside the registry still gets the checksum and generic length checks,
and the result is calculated rather than smooth passing.

## Usage

```
iban <IBAN> [IBAN ...]        validate one or more IBANs
iban --file <path>            validate one IBAN per line from a file
type list.txt | iban          validate piped input
iban                          interactive prompt
iban --generate <CC> <BBAN>   build a valid IBAN by computing check digits
```

| Option | Effect |
| --- | --- |
| `-q`, `--quiet` | print nothing, report through the exit code |
| `--json` | machine readable output |
| `--no-color` | plain text, no colours |
| `--list` | list every country, its length and its BBAN structure |
| `--selftest` | run the built in test suite |
| `-v`, `--version` | version and registry release |
| `-h`, `--help` | help text |

Exit codes: `0` all valid, `1` at least one invalid, `2` usage or file error. That makes
it usable in a script:

```bat
iban --quiet %ACCOUNT% || echo Bad account number & exit /b 1