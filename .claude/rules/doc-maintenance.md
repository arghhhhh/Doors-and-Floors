# Documentation Maintenance

When you make changes to this project, keep the docs in sync:

## When to Update Docs
- **Adding/removing/renaming a script**: Update `docs/SCRIPTS-API.md` with the new/changed API
- **Changing game mechanics** (physics, movement, teleportation, win conditions): Update `docs/ARCHITECTURE.md`
- **Modifying scene hierarchy** (adding/removing GameObjects, changing structure): Update `docs/SCENE-SETUP.md`
- **Changing UI structure** (new UXML elements, USS changes, new UI flows): Update relevant sections in `docs/ARCHITECTURE.md` and `docs/SCENE-SETUP.md`

## How to Update
- Keep the same style and level of detail as the existing docs
- Update the specific section that changed — don't rewrite entire files
- If a doc section is now inaccurate, fix it immediately as part of the same change

## What NOT to Update
- `docs/APP-UI-MIGRATION-PLAN.md` — historical reference only, do not modify
- `docs/CUSTOM-PACKAGES-SETUP.md` — only update if package setup process changes
