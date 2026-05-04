# FFmpeg 目录说明

这个目录是历史遗留目录。

当前版本的 OpenSteamAnalyzer 不再通过 FFmpeg 预处理动态背景，也不会在构建时把 `ThirdParty/ffmpeg` 复制到输出目录。动态背景现在直接缓存 Steam 返回的视频文件，并交给 WPF 媒体播放组件播放。

如果后续重新引入 FFmpeg，需要同时恢复项目文件中的内容复制配置，并在代码中明确说明 FFmpeg 的调用路径、编码器要求和失败回退策略。
