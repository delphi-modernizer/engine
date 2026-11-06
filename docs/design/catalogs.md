# Catalogs: frozen dictionaries for names, ordered rules for families

## Problem

Every token and component class is looked up against what the scanner knows: Delphi's own units, database access
technologies and third-party vendors. The catalogs are built once and read millions of times.

## Decision

Exact names — database units and classes, connection classes, standard VCL classes, runtime units — live in
`FrozenDictionary` and `FrozenSet` with `OrdinalIgnoreCase`: lookups are faster than `Dictionary` because the
structures are optimised once for reading, and they cannot be mutated. Vendor families are recognised by ordered
`GeneratedRegex` rules on unit and class prefixes (`^Tcx[A-Z]`, `^Id[A-Z]`), compiled at build time with a match
timeout. The first matching rule wins.

## Consequences

A match is evidence, not proof: a project's own unit named `IdHelper` would count as Indy. The inventory lists the
evidence behind every technology so a person can tell.
