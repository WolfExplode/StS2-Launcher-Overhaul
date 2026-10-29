# Copy Vanilla to Modded

In the launcher, open **Saves → Copy Vanilla to Modded** and confirm the copy.
The action copies each populated vanilla profile (1–3) on this device into
the matching modded profile for the game's current account. Pull saves from
Steam first if the vanilla profiles are not yet on the device.
Steam-imported profiles directly under the app data directory are supported;
otherwise the action uses Android's `default/0` account directory.

The copy includes progress, profile data, and history. Active single-player
and multiplayer run saves and their backups are excluded. Replacing a modded
profile also clears its active runs. Start a new modded run after copying.
Vanilla profiles and modded profiles without a matching populated vanilla
profile are untouched. The two save namespaces remain separate afterward.

The launcher stages all source profiles before replacing destinations and
retains the old modded profiles under
`user://.sts2-launcher/profile-copy/<operation-id>/backup/`. These backups are
excluded from Steam Cloud synchronization. If replacement fails, completed
replacements are rolled back. The operation shares the save synchronization
lock and records local changes for the next sync; copying itself needs no
network connection.
