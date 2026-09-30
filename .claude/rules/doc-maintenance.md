# Documentation Maintenance

When you make changes to this project, keep the docs in sync:

## When to Update Docs
- **Adding/removing/renaming a script**: Update the table in `.claude/rules/scripts-map.md`
- **Changing game mechanics** (physics, movement, teleportation, win conditions): Update `Docs/ARCHITECTURE.md`
- **Modifying scene hierarchy** (adding/removing GameObjects, changing structure): Update `Docs/SCENE-SETUP.md`
- **Changing UI structure** (new UXML elements, USS changes, new UI flows): Update relevant sections in `Docs/ARCHITECTURE.md` and `Docs/SCENE-SETUP.md`

## How to Update
- Keep the same style and level of detail as the existing docs
- Update the specific section that changed — don't rewrite entire files
- If a doc section is now inaccurate, fix it immediately as part of the same change
