# Contributing to Antigravity Pet

Thank you for your interest in contributing to **Antigravity Pet**! Whether you are fixing a bug, adding a translation, designing a community pixel pet, or proposing a new feature, your contributions are warmly welcome.

> 欢迎为 **Antigravity Pet** 贡献代码、翻译、Bug 反馈或自制宠物包！提交前请先阅读下方指南。

---

## Ways to Contribute / 参与方式

1. **Report Bugs & Request Features / 反馈问题与建议**
   - Open an issue using our [Issue Templates](https://github.com/Wlaeg/antigravity-pet/issues/new/choose).
   - Please search existing issues first to avoid duplicates.

2. **Submit a Community Pet Pack / 投稿社区宠物包**
   - Read the [Custom Pet Specification (`CUSTOM_PET_SPEC.md`)](CUSTOM_PET_SPEC.md) first.
   - Both **1-image fallback pets** (`sprite.png` + `pet.json`) and **full `1536×2288` (8×11) sprite sheets** are supported.
   - Place your pet pack zip under `community/packs/<pet-id>.zip` and register it in `community-pets.json`, or use the **Pet Submission** issue template.

3. **Improve Translations (i18n) / 完善多语言翻译**
   - Antigravity Pet supports **11 locales** (`en`, `zh`, `zh-tw`, `fr`, `de`, `ja`, `ko`, `hi`, `th`, `es`, `es-mx`).
   - Translation improvements for the native desktop pet, settings window, sidecar panel, or documentation are always appreciated.

4. **Code & Documentation / 代码与文档**
   - Review the architecture and local HTTP API documentation in [Development Guide (`docs/DEVELOPMENT.md`)](docs/DEVELOPMENT.md).

---

## Development Setup / 本地开发与构建

### Prerequisites
- **Windows**: PowerShell & `.NET Framework` (`csc.exe`, built into Windows 10/11) — zero external SDK required.
- **macOS**: Xcode Command Line Tools (`swiftc`).
- **Node.js**: Only needed if you run helper scripts or edit plugin tooling.

### Local Build

```powershell
# Windows (builds AntigravityPet.exe)
.\build.ps1
```

```bash
# macOS (builds universal AntigravityPet binary)
./build.sh
```

> Please keep runtime dependencies at **zero** for the native pet binaries (`AntigravityPet.exe` / `AntigravityPet`).

---

## Pull Request Process / PR 提交规范

1. Fork the repository and create a new branch from `main` (e.g., `feat/my-feature`, `fix/bubble-layout`, `pet/my-pet`).
2. Keep changes focused and well-scoped.
3. Verify your build succeeds locally (`./build.ps1` on Windows or `./build.sh` on macOS) when modifying native source files under `app/`.
4. Update relevant documentation (`README.md`, `docs/DEVELOPMENT.md`, or `CUSTOM_PET_SPEC.md`) if your change affects user-facing behavior or specs.
5. Fill out the [Pull Request Template](.github/PULL_REQUEST_TEMPLATE.md) clearly when opening your PR.

---

## Code of Conduct / 行为准则

This project adheres to the [Contributor Covenant Code of Conduct](CODE_OF_CONDUCT.md). By participating, you are expected to uphold this code.
