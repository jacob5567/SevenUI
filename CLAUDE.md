<!-- cope:profile -->
## Build and test notes (maintained by COPE; edit freely)
- Check: `dotnet build -c Release -p:CopyToProfile=false`
- Test signal: compile-only: a pass proves the plugin compiles against the installed RoR2 assemblies with no warnings; behavior is checked only by in-game ModTestRunner scenarios in `tests/` (run as host with `mod_test --mod SevenUI`, needs the game and DebugToolkit), which the check does not run.
- Oracle: allowed: `tests/` holds an existing ModTestRunner harness (scenarios and checks), so acceptance scenarios can be written for typical changes, but they can only be run in game, by hand.
- Hazards: a plain build copies the DLL and PDB into the r2modman profile (`CopyToProfile` target); the check passes `-p:CopyToProfile=false`. Never run `build.sh` (packages dist/) without `CopyToProfile=false`, and nothing here publishes (the Thunderstore publish is a CI workflow).
- Worktree needs: none (optional `Directory.Build.props.user` is absent; defaults point at the local Steam install and r2modman Default profile, which must exist for references to resolve)
- Baseline: pass: 0 warnings, 0 errors (2026-11-04, da99ac0)
<!-- /cope:profile -->
