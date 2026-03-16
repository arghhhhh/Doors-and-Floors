# ZedGames Unity Project

## unity-cli — Unity Editor Automation

This project uses `unity-cli`, a **direct CLI tool** (NOT an MCP server). Do NOT use `npx mcporter` for unity-cli commands. Run `unity-cli` directly via Bash.

### How to Use

1. **Always verify connection first**: `unity-cli system ping`
2. **Prefer typed subcommands** (`system`, `scene`, `instances`) when available
3. **Fall back to `raw`** for everything else: `unity-cli raw <tool_name> --json '{...}'`
4. **Use `--output json`** when chaining results

### Quick Reference

```bash
# Connection
unity-cli system ping
unity-cli instances list

# Scene inspection
unity-cli raw get_hierarchy --json '{"nameOnly":true}'
unity-cli raw get_scene_info --json '{}'
unity-cli raw list_scenes --json '{}'
unity-cli raw find_gameobject --json '{"name":"Player"}'
unity-cli raw find_by_component --json '{"componentType":"Camera"}'
unity-cli raw get_gameobject_details --json '{"gameObjectName":"Player"}'
unity-cli raw get_component_values --json '{"gameObjectName":"Player","componentType":"Transform"}'
unity-cli raw analyze_scene_contents --json '{"includeInactive":true}'

# Scene creation
unity-cli scene create SceneName --path Assets/Scenes/

# GameObject operations
unity-cli raw create_gameobject --json '{"name":"NewObject"}'
unity-cli raw create_gameobject --json '{"name":"Child","parentPath":"/Parent"}'
unity-cli raw add_component --json '{"gameObjectName":"Player","componentType":"Rigidbody"}'
unity-cli raw set_component_values --json '{"gameObjectName":"Player","componentType":"Transform","values":{"position":{"x":0,"y":1,"z":0}}}'
unity-cli raw delete_gameobject --json '{"gameObjectName":"OldObject"}'

# Animator
unity-cli raw get_animator_state --json '{"gameObjectName":"Player"}'
```

### Skills

When a unity task comes up, read the matching skill from `~/.claude/skills/`:

| Task | Skill |
|------|-------|
| Inspect scene hierarchy, find objects | `unity-scene-inspect` |
| Create new scenes | `unity-scene-create` |
| Create/edit GameObjects & components | `unity-gameobject-edit` |
| Prefab workflows | `unity-prefab-workflow` |
| C# script editing | `unity-csharp-edit` |
| Navigate/search C# code | `unity-csharp-navigate` |
| Asset import/management | `unity-asset-management` |
| Addressables | `unity-addressables` |
| Input System | `unity-input-system` |
| PlayMode/EditMode testing | `unity-playmode-testing` |
| Editor tools & windows | `unity-editor-tools` |
| UI automation | `unity-ui-automation` |
| CLI setup & troubleshooting | `unity-cli-usage` |
