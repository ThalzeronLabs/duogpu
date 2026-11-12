# Contributing

Thanks for looking. The most useful contribution right now is a report from hardware I don't have:

1. Run `DuoGPU.exe detect` and paste the output in an [issue](https://github.com/ThalzeronLabs/duogpu/issues)
   (it lists GPU names, outputs and monitor ids; no serial numbers, no user names).
2. Say whether a switch worked, and what the monitor did.

For code:

- `dotnet test tests/DuoGPU.Tests` must pass, and the build treats warnings as errors.
- Keep anything that touches the displays behind the confirmation window, and never raise a refresh rate above the
  tested limit of a path.
- Every visible text goes in `src/DuoGPU/UI/Strings.cs`, in English and Brazilian Portuguese.
- Small pull requests with a clear description are easiest to review.
