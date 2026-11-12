# Changelog

## 0.1.0 (2026-11-12)

The first public version.

- Detects the GPUs, the outputs and the monitor wired to both GPUs, with no configuration.
- AI mode and Game mode: switches the monitor input over DDC/CI, turns each GPU output on or off, keeps the layout,
  the primary monitor and the scale.
- "Can you see this window?" after every switch, with an automatic undo after 15 seconds.
- A tested refresh limit per path, found with you watching, so a cable that cannot carry a mode never blacks out.
- The tray icon (purple AI, green gamepad, amber warning), the panel with the free VRAM of the AI GPU, and a guard
  that checks every 5 seconds and puts back the scale and the refresh by itself.
- The command line: `detect`, `status`, `ai`, `game`, `toggle`.
- English and Brazilian Portuguese.
