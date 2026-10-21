using WeaselStudio.Models;

namespace WeaselStudio.Services;

public static class DictionaryCatalog
{
    public static IReadOnlyList<DictionaryPackage> Packages { get; } = new[]
    {
        new DictionaryPackage(
            "rime-ice",
            "雾凇拼音",
            "常用拼音增强方案，包含词库、英文、emoji、符号和多种双拼。适合作为主力拼音方案。",
            "https://codeload.github.com/iDvel/rime-ice/zip/da1fbe602e38f26db846fa10120ee64c2b0324c0",
            "ad71f7d8a63f8ae2d29a745d4be87be8827598dec5a18fc23009869fb72329c0",
            16942440,
            "GPL-3.0",
            "https://github.com/iDvel/rime-ice",
            new[] { "rime_ice" }),
        new DictionaryPackage(
            "wubi86",
            "五笔 86",
            "Rime 官方五笔 86 方案，含五笔词典和拼音反查。",
            "https://codeload.github.com/rime/rime-wubi/zip/152a0d3f3efe40cae216d1e3b338242446848d07",
            "016e03c66fc5ad91f18a460db152de140ccee10ec72cc0dc63472bc2ecab9968",
            1095691,
            "LGPL-3.0",
            "https://github.com/rime/rime-wubi",
            new[] { "wubi86" }),
        new DictionaryPackage(
            "cantonese",
            "粤拼",
            "Rime 官方粤拼方案，含粤语词库、OpenCC 转换和 IPA 方案。",
            "https://codeload.github.com/rime/rime-cantonese/zip/259f0e48bba840c3a2e0d117539e96937f3d89bc",
            "9ef08e9529e2afbd9b0f84917e5194d8e7e00808cddcb4769fd91e5df13ef96f",
            4819857,
            "CC-BY-4.0",
            "https://github.com/rime/rime-cantonese",
            new[] { "jyut6ping3" })
    };
}
