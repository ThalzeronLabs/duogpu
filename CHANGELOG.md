# Changelog

## 0.2.0 (2026-11-12)

- The guard now keeps the **other monitors** as you saved them: their size, refresh rate and place. A monitor dropped
  to 60 Hz or moved is put back by itself; a monitor that is off turns the icon amber.
- The setup saves the other monitors; *Remember the other monitors as they are* (tray menu) and `DuoGPU.exe remember`
  save them again after a change you made on purpose.
- `DuoGPU.exe fix` runs the guard's safe repairs once, for scripts.

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
