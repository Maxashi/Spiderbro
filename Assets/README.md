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

The checked-in player settings use Brotli compression with decompression fallback for itch.io and tested static hosts.

### Build the first prototype in Unity

1. Save the project and stop Play Mode.
2. Open **File > Build Profiles**.
3. Select **WebGL**. If Unity asks you to switch platforms, select **Switch Platform** and wait for the reimport to finish.
4. In the scene list, confirm that `Assets/Scenes/Test.unity` is the only enabled scene. Keep prototype and demo scenes disabled.
5. Enable **Development Build** for a prototype so warnings and debugging are available. Disable it for a public release candidate.
6. Select **Build And Run** to build and open the game automatically, or **Build** to choose an output folder manually. Use an empty folder such as `Builds/Prototype`.
7. Do not interrupt the build. A successful build contains `index.html` and a `Build` directory at the output root.

For this machine, building through the open Unity Editor is the reliable prototype workflow because an unattended Unity license is required to use the command-line builder.

### Test the build locally

Do not open `index.html` directly from the file browser. Serve the WebGL output over HTTP instead:

```bash
cd Builds/Prototype
python3 -m http.server 8080
```

Open <http://localhost:8080> in a private or fresh browser window. Check:

- the loading indicator completes without missing-file or decompression errors;
- keyboard, mouse, and any controller input work as expected;
- audio starts after the first interaction;
- pausing, refocusing, and resizing the browser remain stable;
- the browser console contains no unexpected runtime errors.

Repeat the test with the browser cache disabled before treating the build as ready to upload.

### Deploy to itch.io

1. Sign in to itch.io and create a new project, or open the existing Spiderbro project.
2. Set the project classification and kind of project as appropriate, then enable **HTML** in the playable-in-browser options.
3. Package the *contents* of the Unity WebGL output, not the parent build directory. The ZIP must contain this structure:

```text
spiderbro-webgl.zip
|- index.html
|- Build/
|- TemplateData/
`- build-manifest.json        # present when produced by the command-line builder
```

`StreamingAssets/` may also be present and must be included when it is generated. `index.html` must be at the root of the ZIP, not inside an extra folder.

On Linux, package the output with:

```bash
cd Builds/Prototype
zip -r ../Spiderbro-WebGL-Prototype.zip .
```

4. Upload the ZIP as a build on the itch.io project page.
5. Mark the uploaded file as **This file will be played in the browser**.
6. Set the itch.io embed width and height to match the desired play area. Start with the Unity WebGL values `960 x 600`, then adjust the page layout after testing the game at real browser sizes.
7. Save the project as **Draft** or **Restricted** first.
8. Open the itch.io game page in a private browser window, test the complete loading flow again, and only then make the project Public.

If itch.io reports a decompression problem, test an uncompressed build:

```bash
SPIDERBRO_WEBGL_COMPRESSION=Disabled \
  ./scripts/build-webgl.sh Builds/Prototype-Uncompressed
```

For new uploads, use a new itch build version or replace the previous file deliberately. Avoid leaving multiple browser-playable ZIPs enabled at once because itch.io may launch the wrong build.

### Command-line and CI builds

To build with the same settings used by CI:

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

### Release checklist

- Build from a clean or freshly saved project.
- Confirm the correct scene is enabled and starts at the intended point.
- Confirm the product name and version in the Unity player settings.
- Test the ZIP itself, not only the original build folder.
- Test with a cleared cache and in a private browser window.
- Check first-load time, download size, frame rate, input, audio, and browser resizing.
- Keep a copy of the exact uploaded ZIP and its build manifest for rollback.
- Publish the itch.io page only after the uploaded browser build passes those checks.

## Explaination video
https://www.youtube.com/watch?v=AhywDyu0EGw
