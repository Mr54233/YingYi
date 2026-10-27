# 第三方组件声明 (Third-Party Notices)

本插件（影医 / YingYi）在构建时将以下第三方程序集合并进最终产物
`artifacts/YingYi.dll`（通过 ILRepack）。特此声明以符合各组件许可证要求。

## TinyPinyin 1.1.0 （MIT License）

- 来源包：`TinyPinyin` 1.1.0（NuGet，目标 `netstandard2.1`）
- 用途：P1-1 拼音搜索 / P1-2 拼音排序名 — 汉字转拼音（`PinyinHelper.GetPinyin`）
- 分发方式：ILRepack 合并进 `YingYi.dll`（PostBuild 目标）
- 许可证全文如下。

```
MIT License

Copyright (c) 2019 TinyPinyin contributors

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## CHTCHSConv 1.0.0

- 简繁转换组件，历史上已通过 ILRepack 合并（见 csproj PostBuild 目标），沿用原有声明。

## SQLitePCL.pretty.core 1.2.2 （编译期引用，不随插件分发）

- 仅在编译期作为类型引用（`ExcludeAssets="runtime" PrivateAssets="all"`），
  **不打包**进 `YingYi.dll`，运行时使用 Emby 服务器自带的
  `SQLitePCL.pretty.dll`。
