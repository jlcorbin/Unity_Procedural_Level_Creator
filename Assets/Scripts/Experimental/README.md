# Experimental

Dormant code preserved for future revival. Nothing in this folder is part
of the active V2 pipeline. Files here are wrapped in `#if FALSE` so they
do not compile by default.

To revive an experimental file:
1. Read CLAUDE.md to understand why it was retired
2. Strip the `#if FALSE` / `#endif` guards
3. Reintroduce the methods to their original class (or repath as needed)
4. Add tests before re-enabling in V2 code

Do not reference anything in this folder from active V2 code.
