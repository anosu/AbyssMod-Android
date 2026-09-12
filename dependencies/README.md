# 构建依赖

此目录包含在干净环境中构建和打包 AbyssMod 所需的最小追踪输入：

```text
dependencies/
├── font/
│   └── ttcuyuanj
├── interop/
│   └── assemblies/    # 仅保留 Mod 项目明确引用的 DLL
└── melonloader/
    └── net6/          # 仅保留 Mod 项目明确引用的 DLL
```

Interop 必须由目标 Android APK 生成，不能混用 PC 代理或其他游戏版本。生成缓存和 `interop-manifest.json` 不参与编译，因此不在这里追踪。

游戏更新并重新生成 Interop 后，刷新仓库中的最小引用集：

```powershell
pwsh -NoProfile -File scripts/sync-dependencies.ps1 `
    -InteropDirectory <Il2CppAssemblies-directory> `
    -MelonLoaderDirectory <LemonLoader-net6-directory>
```

`sync-dependencies.ps1` 从 Mod 项目的显式引用读取文件名，先验证所有输入，再复制所需文件，并删除两个托管引用目录中的旧文件。字体 AssetBundle 是 Release 资源，单独维护。

## 共享库

Utility 通过 `shared/Utility` 源码子模块和 `ProjectReference` 构建。更新共享库时更新源码和子模块指针，或使用本地源码覆盖配置，具体见 [构建说明](../docs/BUILDING.md)。

构建会自动将共享库 DLL 复制到 Mod 输出目录，发布脚本从该目录打包。`dependencies/managed/` 已废弃，不参与编译或打包，整目录忽略 Git；旧克隆中的该目录可直接删除，无需重新复制 DLL。

共享库使用自身固定的编译依赖，不依赖完整 Interop 备份。清理旧 DLL 副本时应保留游戏／加载器的必要引用、字体资源和完整 Interop 本地备份。
