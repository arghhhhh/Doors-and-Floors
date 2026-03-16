# unity-cli Setup for Claude Code

This guide sets up `unity-cli` so Claude Code can automate the Unity Editor (scene inspection, GameObject editing, prefab workflows, testing, etc.) without needing to be told explicitly.

## Prerequisites

- **Rust/Cargo** installed ([rustup.rs](https://rustup.rs))
- **Unity Editor** open with this project loaded
- **Claude Code** installed

## Step 1: Install unity-cli

```bash
cargo install --git https://github.com/akiojin/unity-cli
```

Verify:

```bash
unity-cli --version
```

## Step 2: Install the Unity Bridge Package

The project already includes `com.akiojin.unity-cli-bridge` in its package manifest. If it's missing, add it in Unity via **Window > Package Manager > Add package from git URL**:

```
https://github.com/akiojin/unity-cli-bridge.git
```

## Step 3: Verify the Connection

Open Unity Editor with the project, then run:

```bash
unity-cli system ping
```

You should get `"message": "pong"`. If not, check that Unity is running and the bridge package is installed. The default connection is `localhost:6400`.

## Step 4: Fix PATH for Claude Code (Windows only)

Claude Code uses Git Bash on Windows, which doesn't inherit `~/.cargo/bin` from the Windows PATH. Add it to `~/.bashrc`:

```bash
echo 'export PATH="$HOME/.cargo/bin:$PATH"' >> ~/.bashrc
```

Restart Claude Code after this change. Verify by running `unity-cli system ping` from within Claude Code.

On macOS/Linux this step is usually unnecessary since shell profiles already source cargo's env.

## Step 5: Add the Marketplace Plugin

From within Claude Code, run:

```
/plugin marketplace add akiojin/unity-cli
```

Then reload:

```
/reload-plugins
```

## Step 6: Fix Broken Symlinks (Windows only)

Git on Windows stores symlinks as plain text files. The plugin's skill directories won't load unless you replace them with real copies.

Run this in bash:

```bash
src="$HOME/.claude/plugins/marketplaces/unity-cli/.claude-plugin/plugins/unity-cli/skills"
dest="$HOME/.claude/skills"

for skill in \
  unity-addressables \
  unity-asset-management \
  unity-cli-usage \
  unity-csharp-edit \
  unity-csharp-navigate \
  unity-editor-tools \
  unity-gameobject-edit \
  unity-input-system \
  unity-playmode-testing \
  unity-prefab-workflow \
  unity-scene-create \
  unity-scene-inspect \
  unity-ui-automation; do
  cp -r "$src/$skill" "$dest/$skill"
done
```

Restart Claude Code. Run `/skills` to confirm all 14 `unity-*` skills appear.

## Step 7: Verify Everything

From Claude Code, run these commands to confirm the full setup:

```bash
unity-cli system ping
unity-cli raw get_hierarchy --json '{"nameOnly":true}'
```

Then try a natural language request like "show me the scene hierarchy" — Claude should use `unity-cli` automatically without being told.

## Troubleshooting

| Problem | Fix |
|---------|-----|
| `unity-cli: command not found` in Claude Code | Add `~/.cargo/bin` to `~/.bashrc` (Step 4) and restart |
| `unity-cli system ping` times out | Make sure Unity Editor is open and the bridge package is installed |
| Skills don't appear in `/skills` | Re-run Step 6 to copy skill directories, then restart Claude Code |
| `warning: ... may shadow the managed binary` | Safe to ignore, or run `cargo uninstall unity-cli` and use the managed binary instead |
| Multiple Unity instances | Use `unity-cli instances list` and `unity-cli instances set-active localhost:<port>` |

## Environment Variables (optional)

| Variable | Default | Description |
|----------|---------|-------------|
| `UNITY_CLI_HOST` | `localhost` | Unity Editor host |
| `UNITY_CLI_PORT` | `6400` | Unity Editor port |
| `UNITY_CLI_TIMEOUT_MS` | `30000` | Command timeout |
