# NuGet documentation and payload preservation

Shared helpers extracted unchanged from four maintained repositories, except that the preparation command receives its product root explicitly. Existing local tools remain thin wrappers; copies record immutable source hashes in `build/nuget-artifacts.json`.

The validator requires README and release notes with usable absolute links. The documentation-patch command validates the reviewed base package digest, changes only the declared metadata and README, and checks that every other payload entry is byte-identical. It prepares files locally; it never uploads, publishes, signs or changes an existing published package. Publication remains in repository workflows under their existing gates.

Run `Test-NuGetArtifacts.ps1` for synthetic, network-free regressions.
