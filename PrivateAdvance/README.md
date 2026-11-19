# Private Advance

Tab 7 shell for the planned Private Advance module.

## Current state
- Tab 7 is registered in `MainForm.cs`.
- UI is implemented in `MainForm.Tab7.PrivateAdvance.cs`.
- Process-session status follows `MainForm`'s existing `selectedProcess`, `memory`, and `features` fields.
- This module does not enumerate or search for a game process independently.

## Not yet ported
The KENTUYUK archive is a decompiled .NET Framework 4.8 application, while Muiz Tool targets .NET 8 Windows Forms. The legacy UI, resources, third-party dependencies, and process-memory operations require a compatibility and security review before migration. No legacy executable, credential, endpoint, or process-manipulation implementation has been copied into this module.

## Next integration work
1. Build and run this shell on Windows.
2. Inventory the legacy UI controls and feature handlers.
3. Review resource/dependency use and ownership of external services.
4. Port reviewed, authorized functionality in isolated files, with explicit testing.
