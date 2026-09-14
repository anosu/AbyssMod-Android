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

Utility 通过固定提交的 Git submodule 与 `ProjectReference` 从源码构建，不再维护公共库 DLL 副本。

```powershell
git submodule update --init --recursive
python shared/ModEngineering/scripts/project.py package
```

输出位于 `artifacts/release/v<version>/`。本地共享开发目录、依赖升级和 CI 配置见 [shared/ModEngineering/docs/CONVENTIONS.md](https://github.com/anosu/ModEngineering/blob/main/docs/CONVENTIONS.md)。游戏和加载器编译引用见 [dependencies/README.md](dependencies/README.md)。

## 自动发布

推送任意分支或创建 Pull Request 会执行 Release 构建验证，但不会上传占用 Actions 存储配额的 artifact。推送与项目版本一致的 `v*` 标签时，工作流会把 ZIP 和校验文件直接发布到对应 GitHub Release；标签和项目版本不一致会直接失败。

## 开发

源码位于 `src/`，测试位于 `tests/`。项目配置由 `.csproj` 管理，依赖版本由 Git 子模块记录。构建、VS 联调和发布命令见[公共工程说明](https://github.com/anosu/ModEngineering/blob/main/docs/CONVENTIONS.md)。
