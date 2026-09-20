# The Red Door — Unity Git 工作流

本仓库只保存可以跨电脑同步的 Unity 工程内容：

- `Assets/`
- `Packages/`
- `ProjectSettings/`
- `Documentation/`

`Library/`、`Temp/`、`Logs/`、`UserSettings/`、IDE 工程文件和构建输出均被忽略。它们会在每台电脑上由 Unity 自动重新生成。

## Mac 完成工作后

```bash
git status
git add Assets Packages ProjectSettings Documentation
git commit -m "Describe the Unity change"
git push
```

## Windows 开始工作前

```bash
git pull --ff-only
```

首次在 Windows 获取工程时，使用 Git 克隆仓库，然后通过 Unity Hub 使用 **Unity 6000.3.19f1** 打开工程。不要从 Mac 复制 `Library/`。

## 重要规则

- 同一时间尽量只在一台电脑上编辑同一个 Unity 场景。
- `.meta` 文件必须与对应资源一起提交，不要手动删除。
- 提交前关闭 Unity 或等待资源导入完成。
- Blender 母版和大体积原始素材继续保存在 Magic Drive 的项目目录中。
- 如果以后加入大型音频、视频或高分辨率贴图，再启用 Git LFS。
