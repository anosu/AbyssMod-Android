# 源码依赖与构建

## 干净克隆与 CI

```sh
git clone --recurse-submodules https://github.com/anosu/AbyssMod-Android.git
```

已有克隆运行 `git submodule update --init --recursive`。仓库固定每个公共库的提交，CI 不追踪依赖的最新分支。

```powershell
pwsh -NoProfile -File scripts/build-release.ps1
```

ProjectReference 自动构建公共库并复制 DLL 到 `AbyssMod/bin/Release/`。打包脚本从这里取文件。

## 本地联调

将 `SharedDependencies.local.props.example` 复制为 `SharedDependencies.local.props`。默认引用上一级目录中的 Utility；也可在文件中改为其他路径。

该文件不进入 Git。启用后，修改公共库源码再构建 Mod 即可，无需复制 DLL。CI 自动忽略本地配置。

需要在本地验证固定的子模块版本时：

```powershell
dotnet build AbyssMod/AbyssMod.csproj -c Release -p:UsePinnedSharedDependencies=true
```

临时设置环境变量 `CI=true` 后运行打包脚本，可以检查与 CI 相同的源码选择。

公共库的输出和中间目录位于当前 Mod 的 `artifacts/shared/local/` 或 `artifacts/shared/pinned/`，不同 Mod 不会覆盖同一份 Utility/Extension 的构建缓存。Utility 使用自身仓库中固定的 7 个 Unity/Interop 编译引用；Mod 继续使用本游戏的 Interop。Extension 使用自身固定的 MelonLoader 编译引用。

## 更新公共库

先在公共库仓库提交并推送源码，再在本 Mod 中更新子模块：

```sh
git -C shared/Utility fetch origin
git -C shared/Utility checkout <tested-commit>
git add shared/Utility
```

完成固定版本的构建验证后，将子模块指针与 Mod 改动一并提交。普通分支推送只构建验证，现有 `v*` 标签发布规则保持不变。

## Visual Studio

标准解决方案已包含固定版本的 Utility 项目。在 VS 中打开标准解决方案时，不应用 `SharedDependencies.local.props` 的本地源码覆盖。

如果需要在 VS 中同时修改同级 Utility，先配置 `SharedDependencies.local.props`，然后在本仓库运行：

```powershell
pwsh -NoProfile -File shared/Utility/scripts/New-ModSolution.ps1 -Project AbyssMod/AbyssMod.csproj
```

打开生成的 `AbyssMod-Android.local.slnx`（需要 VS 2022 17.14 或更新版本）。它包含实际引用的共享项目，忽略 Git 追踪；修改共享项目路径后重新运行该命令。不要只向标准解决方案添加本机路径后提交。

VS 使用共享项目自身的 `bin` / `obj` 输出，以保证解决方案构建和项目引用查找一致；命令行项目构建继续使用本仓库 `artifacts/shared` 下的隔离目录。
