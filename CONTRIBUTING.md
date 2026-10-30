# Contributing

Thanks for your interest in improving Network Toggle.

## Getting started

You only need Windows 10 or later. The C# compiler comes with Windows, so there's nothing to install.

```bat
test.cmd
build.cmd
```

`test.cmd` runs the tests, and `build.cmd` creates `Network Toggle.exe`.

## Project layout

| Path | Purpose |
|---|---|
| `source/Network.cs` | Finds adapters and changes their priorities |
| `source/MainWindow.cs` | Window behaviour |
| `source/MainWindow.xaml` | Window layout and styling |
| `source/Glass.cs` | Blur and rounded corners |
| `tests/NetworkTests.cs` | Tests for adapter detection and generated commands |

## Language version

The Windows compiler supports C# 5 only, so features such as string interpolation (`$"..."`) and `?.` won't build.

## Pull requests

1. Keep each pull request focused on one change.
2. Add a test if you change adapter detection or the generated commands.
3. Make sure `test.cmd` and `build.cmd` both pass.
4. Add your change to `CHANGELOG.md` under **Unreleased**.
5. Test on a real PC with at least two connections if you change switching.

To support a new tethering adapter, add part of its description to `PhoneAdapters` in `source/Network.cs`, along with a test.

## Reporting bugs

Please use the bug report template and include your adapter details.
