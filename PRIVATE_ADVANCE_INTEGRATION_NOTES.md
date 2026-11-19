# Private Advance integration notes

This change adds the initial Tab 7 shell only. It is not a full migration of KENTUYUK features.

## Files changed
- `MainForm.cs`: registers Tab 7 after Tab 6 and refreshes its session status after attach/detach.
- `MainForm.Tab7.PrivateAdvance.cs`: contains the Tab 7 UI and status binding.
- `PrivateAdvance/README.md`: documents migration state and next steps.

## Process ownership
The new module does not call `Process.GetProcessesByName` and does not perform independent process discovery. It displays the process already selected and attached by Muiz Tool. Existing process selection behavior in Muiz Tool is unchanged.

## Compatibility/security findings from the supplied KENTUYUK ZIP
- KENTUYUK targets .NET Framework 4.8 x64; Muiz Tool targets .NET 8 Windows Forms.
- The legacy archive contains decompiled source, resource files, and third-party dependencies whose exact runtime compatibility is not established.
- Its source audit identifies hard-coded HTTP service endpoints, token-like constants, embedded RSA key material, and an external shortened URL. These were deliberately not copied into Muiz Tool.
- Process-memory read/write and process manipulation code require separate review and must only be used in software/processes the operator owns or is authorized to test.

## Build verification
The editing environment does not have the .NET SDK installed. A Windows `dotnet build -c Release` via GitHub Actions is still required before treating the change as build-verified.
