# AbyssMod Android

本版本针对 [LemonLoader v0.7.4-android.3](https://github.com/LemonLoaderX/LemonLoader/releases/tag/v0.7.4-android.3) 构建，Mod 目标为 `net10.0`，需要 .NET 10 SDK；加载器运行时为 .NET 11，历史 `net6` 目录仍作为引用和安装路径。Loader 编译 DLL 来自正式发行包，不随 Mod ZIP 部署。使用 Patcher 安装该加载器时需 2.1.0 或更新版；从原始游戏 APK 准备新 payload，保留包名、签名及用户数据，勿混用旧运行时。游戏 Interop 保持各项目现有导出，运行验证需使用对应游戏版本。

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

翻译会话在卸载时取消 manifest、静态资源和剧情请求，取消不弹失败通知。补丁安装部分失败后会回滚；回滚或卸载失败时保留清理句柄与运行依赖。字体可能被游戏文本持有，按进程生命周期保留，不支持热替换或强制销毁。

源码位于 `src/`，测试位于 `tests/`。项目配置由 `.csproj` 管理，依赖版本由 Git 子模块记录。构建、VS 联调和发布命令见[公共工程说明](https://github.com/anosu/ModEngineering/blob/main/docs/CONVENTIONS.md)。

Android 保留 MelonPreferences 和现有 UserData 路径，配置修改后重启生效。资源地址和路径在赋值或重载时去除首尾空格，无效 HTTP/HTTPS 地址及空路径回退默认值；连续数值编辑只写日志，布尔开关保留 Toast。`General.HideNovelGlasses` 默认 false，可独立隐藏寝室 CG 眼镜。字体加载由入口持有并在卸载时停止协程、解除注册和配置订阅；翻译解密使用显式参数及有限大小的流式读取，界面翻译支持通配路径与占位符规则。

PC 的 ConfigurationManager 标签、快捷键和滚轮缩放属于桌面交互；图片替换与后处理控制依赖尚未验证的 Android 纹理/渲染契约，本版保持现有功能范围。已有游戏互操作引用不能用 PC 导出替代。回归测试覆盖资源配置、解密流、界面规则和缓存；字体、Live2D 和 ARM64 补丁需游戏内验证。
