Simple IBAN validator generated for demonstration purposes. Code must follow the following ruleset:

1. **Character set** — `A-Z` and `0-9` only, after spaces and hyphens are stripped.
2. **Length** — 15 to 34 overall, and exactly the fixed length that country uses.
3. **Shape** — two letters of country code, then two digits of check digits.
4. **BBAN structure** — the account number matches the country's layout. A UK IBAN must have four *letters* of bank code; a German one must be eighteen digits.
5. **Checksum** — ISO 7064 MOD 97-10 over the rearranged IBAN must equal 1.

'00', '01' and '99' exempt as standard with MOD 97-10
