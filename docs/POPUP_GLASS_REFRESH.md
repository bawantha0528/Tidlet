# Blue glass popup refresh

Follow-up: user clarified that the desktop should be visible through the popups. Lower only the background brush opacity, including the inset charts, while keeping text and data accents opaque. Use the existing per-pixel transparent popup window. The first glass trial ended after 249 samples (popup closed at 278.17 seconds; 85.06 MB peak observed), so it was incomplete. Keep that result and validate the revised build separately.

Use the attached image as visual inspiration for the existing monitor detail popups. Keep the bar, Dashboard, popup bounds, live metrics and supported modules. Use frozen WPF brushes for navy glass, module accents, inset cards and charts. Keep the existing renderer; no wallpaper capture, blur effects, new assets, packages or settings changes. Verify focused rendering tests, live previews and a fresh CPU popup resource trial before replacing the normal build.

Gradient and frozen brush references: [WPF gradients](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/painting-with-solid-colors-and-gradients-overview), [Freezable objects](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/freezable-objects-overview).

## Visual verification

The isolated Release build `bin/popup-glass` completed without warnings or errors. Four existing popup drawing/lifetime tests passed with the x86 test host. Seven short owned fixtures opened CPU, RAM, GPU, Network, Battery, Storage and Sensors, presented readings or an explicit capability message, exported 100% and 150% render images, and exited with workers drained and tray removed. The render exports were inspected under `artifacts/popup-glass-preview`. They are WPF render exports, not desktop captures or physical mixed-DPI tests. The existing popup layout and sampling remain in place. The CPU five-minute budget gate is pending.

## Translucency verification

The revised `bin/popup-translucent` Release build passed without warnings/errors. Four focused x86 WPF popup drawing/lifecycle tests passed. CPU and RAM ran eight-second owned previews, rendered at 100%/150%, and exited naturally with all workers drained and tray removed. A background-only pixel was alpha 182/255 at both scales for both modules (about 29% background transmission). Foreground data accents and text brushes are opaque. See `artifacts/popup-translucent-preview`. This measures exported WPF alpha, not a desktop screenshot. The final five-minute CPU popup trial is separate from the prior incomplete trial.
