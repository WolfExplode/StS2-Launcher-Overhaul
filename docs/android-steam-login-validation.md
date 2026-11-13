# Android Steam login validation

## Implementation boundary

Steam sign-in is required to verify ownership, download the game, and synchronize saves. Offline launch depends on saved credentials, a matching ownership record, and a downloaded game that is ready to launch. Missing saved credentials lead to the login screen; a failed sign-in asks the user to sign in again.

Once the game is starting or running, a save-sync authentication or connection failure does not prevent local play or local saves. Automatic uploads stay paused when the startup save check cannot complete, and local changes remain queued for a later sync. This game-process behavior does not bypass the launcher's login requirements.

The native Android credential panel must:

- Accept username and password input.
- Support Steam Guard challenges.
- Clear password fields after submit, cancel, or expiry.
- Pass an entered password to Steam for sign-in, then clear it. Never persist passwords or put them in scripts or logs.
- Keep refresh tokens encrypted through the existing Android Keystore path.
- Keep logs free of credentials and tokens.

## Checks available without a device

- Managed compilation.
- Basic launcher navigation and action wiring on desktop.

The focused save tests do not validate authentication. Their fake remote proves neither Steam nor Android transport.

## Checks requiring a device

- Keyboard and focus behavior.
- Password-manager suggestions.
- Steam Guard interaction.
- Real authentication, ownership, and download.
- Process death and resume behavior.

For diagnostics, prefer **Help → Diagnostics → Create support report** and a focused logcat window. Review both before sharing. Never publish passwords, Steam Guard codes, refresh/session tokens, QR/login payloads, account identifiers, private save data, or unreviewed full logs.
