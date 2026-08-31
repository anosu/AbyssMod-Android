# AbyssMod Android

AbyssMod 的 LemonLoader Android 移植版，为 Unity IL2CPP 客户端提供中文本地化与触屏体验优化。

## 功能

- 剧情、界面、角色名称和 MasterData 翻译
- 翻译下载、本地缓存和离线回退
- 自定义中文 TextMeshPro 字体
- 动态马赛克、音量提醒和语音播放优化
- 标题动画和 Live2D 显示比例控制
- Mod 状态与配置变更 Toast 通知

## 环境

- Android ARM64
- Unity IL2CPP
- LemonLoader 或兼容的 MelonLoader Android 环境

## 安装

从 Releases 下载 `AbyssMod-Android.zip`，解压到游戏的 `MelonLoader` base 目录并保留归档中的 `Mods` 和 `UserData` 路径。配置文件首次启动后生成在 `MelonLoader/UserData/AbyssMod.cfg`。

## 构建

仓库跟踪字体、Utility 和项目实际引用的最小 MelonLoader/Interop DLL，可以在干净环境中直接构建：

```powershell
pwsh -NoProfile -File scripts/build-release.ps1
```

输出位于 `artifacts/release/v<version>/`。游戏或 LemonLoader 更新后，按照 [dependencies/README.md](dependencies/README.md) 使用 `scripts/sync-dependencies.ps1` 刷新最小引用集。

## 自动发布

推送任意分支或创建 Pull Request 会执行 Release 构建验证，但不会上传占用 Actions 存储配额的 artifact。推送与项目版本一致的 `v*` 标签时，工作流会把 ZIP 和校验文件直接发布到对应 GitHub Release；标签和项目版本不一致会直接失败。
