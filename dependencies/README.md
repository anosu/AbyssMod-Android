# 本地编译依赖

此目录只收纳 AbyssMod 的本地编译引用，二进制文件由 `.gitignore` 排除：

```text
dependencies/
├── melonloader/
│   └── net6/          # MelonLoader net6 托管 DLL
└── interop/
    └── assemblies/    # 当前游戏版本的 Il2CppInterop DLL
```

将 MelonLoader 解压目录中 `net6/*.dll` 放入 `melonloader/net6/`，将 Patcher 生成的 `Il2CppAssemblies/*.dll` 放入 `interop/assemblies/`。`interop-manifest.json` 可以与 Interop DLL 一起保留；不要复制 `MethodAddressToToken.db`、`MethodXrefScanCache.db` 等生成缓存。

也可以在构建时覆盖默认目录，不需要复制本地文件：

```powershell
dotnet build AbyssMod-Android.slnx -c Release `
    -p:MelonLoaderReferenceDirectory=<MelonLoader-net6目录> `
    -p:GameInteropReferenceDirectory=<Il2CppAssemblies目录>
```
