# DisplayPilot 1.1.0

- Replaced tabs and data grids with sidebar navigation, rounded status cards, and individual device assignment rows.
- Added device search and expandable details so hardware identifiers stay out of the main workflow.
- Added a persistent Save changes action, Ctrl+S, visible unsaved state, and inline feedback.
- Profile renames update device assignments and quick-switch shortcuts automatically; profiles in use are protected from removal.
- Grouped DisplayMagician connection, quick profiles, cooldown, and launch settings.
- Preserved the raw-input pipeline, assumed-PC startup, cooldown, and tray controls. Personal settings remain local.
- Added isolated interface checks that use synthetic data and never invoke DisplayMagician.

Controller input activation is still deferred. Installers remain unsigned.
