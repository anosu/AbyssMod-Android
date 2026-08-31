# 构建依赖

此目录包含在干净环境中构建和打包 AbyssMod 所需的最小追踪输入：

```text
dependencies/
├── font/
│   └── ttcuyuanj
├── interop/
│   └── assemblies/    # 仅保留 Mod 项目明确引用的 DLL
├── managed/
│   └── Utility.dll
└── melonloader/
    └── net6/          # 仅保留 Mod 项目明确引用的 DLL
```

Interop 必须由目标 Android APK 生成，不能混用 PC 代理或其他游戏版本。生成缓存和 `interop-manifest.json` 不参与编译，因此不在这里追踪。

游戏更新并重新生成 Interop 后，使用同一目录构建 Utility，再刷新仓库中的最小引用集：

```powershell
dotnet build ../Utility/Utility/Utility.csproj -c Release `
    -p:UnityProxyDir=<Il2CppAssemblies-directory>

pwsh -NoProfile -File scripts/sync-dependencies.ps1 `
    -InteropDirectory <Il2CppAssemblies-directory> `
    -MelonLoaderDirectory <LemonLoader-net6-directory> `
    -UtilityAssemblyPath ../Utility/Utility/bin/Release/net6.0/Utility.dll
```

`sync-dependencies.ps1` 从 Mod 项目的显式引用读取文件名，先验证所有输入，再复制所需文件，并删除两个托管引用目录中的旧文件。字体 AssetBundle 是 Release 资源，单独维护。
