# 影医 · YingYi

![logo](StrmAssistant/Properties/thumb.png "logo")

## [[English]](README.en.md)

Emby 影音库增强插件，基于 [StrmAssistant](https://github.com/sjtuross/StrmAssistant)（GPL-3.0）继续开发。

## 与上游的差异

1. **兼容 Emby 4.8 / 4.9 / 4.10**：`GetChapters` 在 4.9 改了签名（`long, MarkerType[]` → 加 `CancellationToken`），本项目用 `ChapterQueryCompatibility` 三档反射回退兜住，4.8 走内存过滤分支
2. **中文搜索增强（P1-1）**：往 Emby 的 FTS5 索引注入拼音 token，中文标题可用全拼 / 首字母 / 中段搜到
3. **拼音排序名（P1-2）**：中文标题生成拼音排序名写入 `SortName`，侧栏首字母分组正确；带 `PinyinSortNameBackup` 备份表，可一键回滚
4. **社区片头源（P1-3）**：接入 [IntroDB](https://introdb.app) 补本地指纹测不出的片头（默认关闭）
5. **自更新**：指向 `Mr54233/YingYi` 的 release

## 功能

1. 提高首次播放的起播速度
2. 视频截图预览缩略图增强
3. 片头片尾探测增强
4. 自动合并同目录视频为多版本
5. 独占模式提取媒体信息
6. 独立的外挂字幕扫描
7. 自定义刮削备选语言
8. 使用替代 `TMDB` 配置
9. 演职人员增强 `TMDB`
10. 获取原语言海报
11. 中文搜索增强（拼音）
12. 拼音首字母排序
13. 媒体信息持久化
14. 支持代理服务器
15. 支持 `TMDB` 剧集组刮削
16. 社区片头源（IntroDB）

## 安装

1. 从 [Releases](https://github.com/Mr54233/YingYi/releases) 下载 `YingYi.dll`
2. 放进 Emby 的 `programdata/plugins/` 目录
3. 重启 Emby
4. 在「插件 → 影医 → Settings」里配置

**支持版本**：Emby 4.8.11 / 4.9.5 / 4.10.2（均在沙盒实测，见项目文档）

## 构建

需要 .NET 8 SDK：

```bash
dotnet build StrmAssistant/StrmAssistant.csproj -c Release
# 产物：artifacts/YingYi.dll
```

## 声明

本项目为开源项目，与 Emby LLC 没有任何关联，也未获得 Emby LLC 的授权或认可。本项目的目的是为合法购买并安装了 Emby 软件的用户提供额外的功能增强和使用便利。

### 使用须知

1. **合法使用**
   本项目仅适用于合法安装和使用 Emby 软件的用户。使用本项目时，用户需自行确保遵守 Emby 软件的服务条款和使用许可协议。

2. **非商业用途**
   本项目完全免费，仅限个人学习、研究和非商业用途。严禁将本项目或其衍生版本用于任何商业用途。

3. **不包含 Emby 专有组件**
   本项目未包含 Emby 软件的任何专有组件（例如：DLL 文件、代码、图标或其他版权资源）。使用本项目不会直接修改或分发 Emby 软件本身。

4. **功能限制**
   本项目不会绕过 Emby 的授权机制、数字版权保护 (DRM)，或以任何方式解锁其付费功能。

5. **用户责任**
   用户在使用本项目时，需自行承担遵守相关法律法规的责任。

### 免责声明

1. 本项目开发者不对因使用本项目而可能导致的任何直接或间接后果（包括但不限于数据丢失、软件故障或法律纠纷）负责。
2. 如果认为本项目可能侵犯相关方的合法权益，请与开发者取得联系。

## 许可证

[GPL-3.0](LICENSE)。本项目是 [StrmAssistant](https://github.com/sjtuross/StrmAssistant) 的衍生作品，同样以 GPL-3.0 发布，原始版权归其作者所有。
