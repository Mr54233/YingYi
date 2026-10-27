# YingYi (影医)

![logo](StrmAssistant/Properties/thumb.png "logo")

## [[中文]](README.md)

An Emby library enhancement plugin, forked from [StrmAssistant](https://github.com/sjtuross/StrmAssistant) (GPL-3.0).

## Differences from upstream

1. **Emby 4.8 / 4.9 / 4.10 compatible**: 4.9 changed the `GetChapters` signature (`long, MarkerType[]` → adds `CancellationToken`). We added `ChapterQueryCompatibility`, a three-tier reflection fallback; 4.8 takes the in-memory filter branch.
2. **Chinese search (P1-1)**: injects Pinyin tokens into Emby's FTS5 index so Chinese titles are searchable by full pinyin, initials, or middle segments.
3. **Pinyin sort names (P1-2)**: writes Pinyin sort names into `SortName` so the alphabet sidebar groups correctly; ships a `PinyinSortNameBackup` table for one-click rollback.
4. **Community intro source (P1-3)**: fills in intros that local fingerprinting cannot detect, via [IntroDB](https://introdb.app) (off by default).
5. **Self-update**: points at `Mr54233/YingYi` releases.

## Features

1. Improve initial playback start speed
2. Image capture and thumbnail preview enhanced
3. Playback behavior-based intro and credits detection
4. Auto-merge videos in the same folder as multiple versions
5. Exclusive-mode media info extraction
6. Independent external subtitle scan
7. Custom metadata fallback languages
8. Alternative `TMDB` configuration
9. Cast & crew enhancement via `TMDB`
10. Original-language posters
11. Chinese search enhanced (Pinyin)
12. Pinyin initials sorting
13. Media info persistence
14. Proxy server support
15. `TMDB` episode-group scraping
16. Community intro source (IntroDB)

## Install

1. Download `YingYi.dll` from [Releases](https://github.com/Mr54233/YingYi/releases)
2. Copy it into Emby's `programdata/plugins/` folder
3. Restart Emby
4. Configure under **Plugins → YingYi → Settings**

**Supported versions**: Emby 4.8.11 / 4.9.5 / 4.10.2 (all verified in sandboxes).

## Build

Requires .NET 8 SDK:

```bash
dotnet build StrmAssistant/StrmAssistant.csproj -c Release
# Output: artifacts/YingYi.dll
```

## License

[GPL-3.0](LICENSE). This project is a derivative work of [StrmAssistant](https://github.com/sjtuross/StrmAssistant), also released under GPL-3.0; original copyright belongs to its authors.
