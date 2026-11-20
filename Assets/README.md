# SpiderProceduralAnimation

## Setup
- Unzip or clone the repository in the Assets folder of an unity project. It has been created on unity 2019.4.14f1 with URP but other than the materials, it should work on any RP.
- Make sure to have the animation rigging package installed. You can install it via package manager (be careful it is still a preview package)

## Code readability and documentation

- Prefer clear names that describe intent over short or generic names. A reader should be able to understand a method, field, or local variable without tracing all of its usages.
- Add meaningful inline commentary throughout gameplay code. Explain what is happening, why an approach is used, and which details are important for movement, physics, camera behavior, or animation.
- Document serialized fields with concise tooltips or comments. Describe:
  - what the field controls;
  - the expected units or valid range;
  - important interactions with other fields;
  - what a larger or smaller value changes in practice.
- Document non-obvious private state as well, especially values shared between `Update`, `LateUpdate`, and `FixedUpdate`. Comment ownership and timing so that ordering dependencies remain visible.
- Give complex methods a short summary before the implementation. Break large methods into focused helpers when each part has a distinct purpose, and comment the important transitions between those parts.
- Comment reasoning rather than restating syntax. For example, explain why a trajectory uses a collision sphere, why a camera rotation is compensated, or why a value is smoothed over time.
- Keep comments current when behavior changes. A misleading comment is worse than no comment.
- Use headings and Inspector groups (`Header`, `Tooltip`, and `Range`) to make serialized settings understandable without opening the source file.
- Keep the update frequency explicit. Document code that depends on physics time, frame time, smoothing, delayed execution, or Unity's input and camera update order.
- Preserve this documentation style when extending the project: add field documentation and inline guidance as part of the same change, not as a later cleanup task.

## Source Control commit messages

When using the commit-message button in the Source Control side panel, formulate the message as a short explanation of the user-visible or gameplay-visible change.
Repository-specific AI instructions are stored in `.copilot-commit-message-instructions.md` and wired into VS Code through `.vscode/settings.json`, so the generated message should describe the actual diff instead of defaulting to a generic refactoring summary.

1. Write a concise imperative summary on the first line, ideally no longer than 72 characters.
2. Start with a useful verb such as `Add`, `Fix`, `Improve`, `Refine`, or `Remove`.
3. Name the affected behavior or system when it is not obvious.
4. Add a blank line and short body only when the reason, tradeoff, or testing details are not clear from the summary.
5. Keep unrelated changes in separate commits.
6. Avoid vague messages such as `update`, `changes`, `fix stuff`, or `wip`.
7. Mention notable scene, prefab, or input-setting dependencies in the body when the code change requires them.

Good examples:

```text
Add vertical control to jump aiming
```

```text
Fix landing marker drifting from camera center

Compensate for the Cinemachine follow offset so the projected
impact point remains the camera's focus during aiming.
```

```text
Slow spider leg animation while airborne
```

## Project and web-build considerations

- Treat this game as a web-first project that remains self-hostable. The build must run from an ordinary static web host without requiring a game server or backend.
- Prefer itch.io and generic static hosting for distribution. Unity WebGL uses generated JavaScript, WebAssembly, data files, and runtime assets, which these targets can serve directly.
- GitHub Pages is acceptable only when the WebGL compression configuration is compatible with its static hosting headers. GitHub Pages may not provide the `Content-Encoding` headers required by Unity's Brotli or Gzip output, so avoid assumptions: test the production build and either use uncompressed output or Unity's supported decompression fallback when deploying there. If that cannot produce a reliable build, skip GitHub Pages and deploy to itch.io or another static host instead.
- Performance and loading speed are primary requirements, not optional polish. Keep the initial download small, avoid unnecessary packages and runtime allocations, and measure both scene startup time and frame rate on representative low-end hardware.
- Prefer small textures, shared materials, simple geometry, limited overdraw, pooled reusable objects, and restrained post-processing. Compress assets appropriately, but confirm that the chosen web compression remains compatible with every intended host.
- Preserve responsive input and stable frame pacing while jumping, aiming, rendering trajectories, and animating the spider. Expensive visual work must not weaken controller responsiveness.
- Target a late-1990s to early-2000s console aesthetic, with a PlayStation/PSX-era visual direction rather than modern photorealism. Favor clear silhouettes, chunky low-poly forms, low-resolution textures, limited palettes, fog, hard lighting, and deliberate texture or shading artifacts.
- Retro styling should come from intentional art direction, not accidental technical problems. The result should still have a polished composition, readable surfaces, clear interaction feedback, and consistent visual rules.
- Avoid large content or effects that contradict the retro target or noticeably increase the WebGL payload. Reuse modular level pieces and effects where possible.

## WebGL release builds

The checked-in player settings use Brotli compression with decompression fallback for itch.io and tested static hosts. To build with the same settings used by CI:

```bash
./scripts/build-webgl.sh Builds/WebGL
```

Set `SPIDERBRO_BUILD_VERSION` to stamp the product version. Set `SPIDERBRO_DEVELOPMENT_BUILD=1` for a development player with debugging enabled. Set `SPIDERBRO_WEBGL_COMPRESSION=Disabled` for a host that cannot serve or decompress Unity's compressed WebGL output, or use `Gzip` when the host requires it:

```bash
SPIDERBRO_WEBGL_COMPRESSION=Disabled \
  ./scripts/build-webgl.sh Builds/WebGL-Uncompressed
```

The builder writes `build-manifest.json` beside the WebGL output. It is the release record for the Unity version, product version, target, compression format, commit, and build time.

The GitHub Actions workflow builds pull requests, `main`, manual runs, and `v*` tags. It requires a Linux self-hosted runner labeled `unity-6000-6-10` with:

- Unity 6000.6.10f1 and the WebGL Build Support module installed;
- the repository's Unity license activated for unattended use;
- Git LFS, `zip`, and GitHub CLI available;
- enough disk space for the Unity Library and a production WebGL build.

Tagged builds publish the ZIP as a GitHub release. Publishing from there to itch.io is deliberately a separate upload step until deployment credentials and the target itch project are finalized.

## Explaination video
https://www.youtube.com/watch?v=AhywDyu0EGw
