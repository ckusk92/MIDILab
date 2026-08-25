# Contributing to MIDILab

Thanks for your interest in improving MIDILab. Contributions of code, documentation, testing, MIDI mappings, UX ideas, and musical-generation improvements are welcome.

## Before you start

For bug fixes and small improvements, opening a pull request directly is fine.

For larger features or changes to generation behavior, please open a Feature Request first. This helps avoid duplicate work and gives maintainers and contributors a place to agree on the intended behavior before implementation begins.

## Development requirements

- Windows
- .NET 10 SDK
- Visual Studio with the **.NET desktop development** workload, or a compatible .NET development environment

## Set up the project

1. Fork the repository on GitHub.
2. Clone your fork.
3. Create a branch from the latest `main`.
4. Restore and build the solution.

```powershell
git clone <your-fork-url>
cd MIDILab
git checkout -b feature/short-description
dotnet restore MIDILab.slnx
dotnet build MIDILab.slnx --configuration Release
```

You can also open `MIDILab.slnx` in Visual Studio.

## Making changes

Please keep changes focused. A pull request that does one thing well is easier to review than one that mixes unrelated refactoring, formatting, and features.

When changing the drum generator:

- Preserve musical behavior outside the intended change where practical.
- Consider all supported time signatures, not only 4/4.
- Respect the currently enabled kit pieces.
- Preserve velocity and timing information when moving or transforming existing hits.
- Keep export mapping separate from musical generation logic.

When changing the UI:

- Keep the normal workflow simple.
- Prefer optional advanced controls over cluttering the primary interface.
- Verify both Light and Dark Mode.
- Check all six accent themes when changing shared brushes or control templates.

## Before opening a pull request

Please verify:

- The solution builds in Release configuration.
- The application starts without XAML/resource errors.
- Existing Generate, New Variation, preview, project Save/Open, and MIDI export still work.
- Undo/Redo behaves correctly for any new editable operation.
- New UI remains usable in both Light and Dark Mode.
- Documentation is updated if behavior visible to users changed.

Run:

```powershell
dotnet restore MIDILab.slnx
dotnet build MIDILab.slnx --configuration Release
```

The GitHub Actions build will also run automatically when you open a pull request.

## Pull requests

In your pull request description:

- Explain what changed and why.
- Link the related issue when one exists.
- Describe how you tested the change.
- Include screenshots for visible UI changes when useful.
- Call out any known limitations or follow-up work.

Pull requests are reviewed before being merged into `main`. A successful automated build does not guarantee acceptance. Maintainers may request changes for architecture, UX, musical behavior, or project direction.

## Reporting bugs

Use the Bug Report issue form and include:

- Windows version
- MIDILab version
- Steps to reproduce
- What you expected
- What actually happened
- Full exception text when available

Please do not publish security vulnerabilities as normal issues. See [SECURITY.md](SECURITY.md).

## Feature requests

Feature requests are welcome, especially when they describe the musical or editing problem being solved rather than only a specific implementation.

Examples include:

- New generation controls
- New genre behavior
- More realistic humanization
- MIDI mappings
- Editing improvements
- Song-building workflows
- Future instrument support

## License

By contributing code or documentation to this repository, you agree that your contribution may be distributed under the project's MIT License.
