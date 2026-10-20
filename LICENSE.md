# License

Fury Parameter Compressor (`net.fosa.fury-parameter-compressor`)
Copyright (c) 2026 FOSA

**This package is a derivative work of VRCFury. Read section 1 before using or redistributing it.**

This package is assembled from three upstream sources, each under its own terms. Because the terms combine,
the **most restrictive** ones govern the package as a whole:

> The package as a whole may be used, copied, modified and distributed **for personal purposes only**,
> and this file must be included in full with any derivative work or distribution.
> It may **not** be used for any commercial purpose.

The individual terms follow.

---

## 1. Portions derived from VRCFury

The following files were ported from VRCFury (`com.vrcfury.vrcfury`) and remain subject to VRCFury's license.
Each file names its upstream source and the changes made in a comment at the top.

```
Editor/Editor-Avatars/FPCPackageUtils.cs
Editor/Editor-Avatars/Service/Compressor/CompressorLayerUtilsService.cs
Editor/Editor-Avatars/Service/Compressor/OptimizationDecision.cs
Editor/Editor-Avatars/Service/Compressor/ParameterCompressorLayerService.cs
Editor/Editor-Avatars/Service/Compressor/ParameterCompressorService.cs
Editor/Editor-Avatars/Service/Compressor/ParameterCompressorSolverOutput.cs
Editor/Editor-Avatars/Service/Compressor/ParameterCompressorSolverService.cs
Editor/Editor-Avatars/Service/Compressor/ParameterPlatformAlignmentService.cs
Editor/Editor-Avatars/Utils/VFStateAvatarExtensions.cs
Editor/Editor-Avatars/Utils/VRCExpressionParameterExtensions.cs
Editor/Editor-Avatars/Utils/VRCExpressionParametersExtensions.cs
Editor/Editor-Avatars/Utils/VRCExpressionsMenuExtensions.cs
Editor/Editor-Common/Utils/AnimatorConditionLogic.cs
Editor/Editor-Common/Utils/Controller/VFABool.cs
Editor/Editor-Common/Utils/Controller/VFAParam.cs
Editor/Editor-Common/Utils/Controller/VFCondition.cs
Editor/Editor-Common/Utils/EnumerableExtensions.cs
Editor/Localization/en-US.json      (the `compressor.warn.*` strings)
Editor/Localization/zh-Hans.json    (translations of the `compressor.warn.*` strings)
```

VRCFury's license offers a choice of terms.
This package is a **modified / derivative version** of VRCFury,
so the Commercial License is not available to it: that license requires VRCFury to be left unmodified and forbids derivative works.
Only VRCFury's **Personal License** applies, which is why the package as a whole is restricted to personal purposes.

VRCFury's full copyright and license notice, reproduced as required:

> # License Options
>
> VRCFury (c) 2022 Senky
>
> For the purposes of this document, a "commercial purpose" is one primarily intended for commercial advantage or monetary compensation (including, but not limited to, one-time payments, subscription payments, and donations). A "personal purpose" is any purpose aside from those defined as a "commercial purpose."
>
> THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
>
> You may choose to use any of the following licenses:
>
> ### Personal License
>
> VRCFury may be used, copied, modified, merged, published, distributed, or sublicensed for personal purposes if all of the following restrictions are met:
> * This full copyright and license notice is included in any derivitive works or distributions.
>
> ### Commercial License
>
> VRCFury is licensed, not sold.
>
> VRCFury may be used for personal or commercial purposes if all of the following restrictions are met:
> * VRCFury must be downloaded directly by the end user from an official distribution channel designated by Senky, including https://vcc.vrcfury.com
> * VRCFury must not be redistributed with your product.
> * The source files must be left unmodified.
> * You may not modify, patch, adapt, transform, inject into, or create derivative works of VRCFury, whether directly or through any third party, tool, plugin, script, build step, or service.
> * You may not use, distribute, or rely on any modified or derivative version of VRCFury for any commercial purpose.
> * You may not request, instruct, require, or assist an end user or third party to modify, patch, or use a modified or derivative version of VRCFury in connection with your product, service, installation process, compatibility, support, or advertised features.
> * You may not distribute, bundle, provide, link to, or direct users to any patch, plugin, script, fork, service, or instructions intended to modify VRCFury for use with your product or service.
>
> Any use in violation of this section automatically terminates this Commercial License.
>
> ### VRCA / VRCW License
>
> VRCFury may distributed for personal or commercial purposes if all of the following restrictions are met:
> * The distribution is made as part of an "uploaded VRChat avatar asset bundle," or an "uploaded VRChat world asset bundle" hosted on VRChat asset servers.
> * This license is only here to permit the use of the SPS shader, which is linked and packaged into avatars / worlds uploaded using SPS. This means the SPS shader CANNOT be used for commercial purposes at runtime outside of VRChat avatar / world packages.

---

## 2. Portions derived from Modular Avatar

The following files were ported from Modular Avatar (`nadena.dev.modular-avatar`), which is MIT licensed.

Each file names its upstream source and the changes made in a comment at the top.

```
Editor/ErrorReporting/ErrorLog.cs
Editor/Inspector/FPCEditorBase.cs
Editor/Inspector/FPCStyles.uss
Editor/Inspector/FuryParameterCompressorEditor.cs
Editor/Inspector/FuryParameterCompressorEditor.uxml
Editor/Inspector/ParameterPickerWindow.cs
Editor/Inspector/ParameterPickerWindow.uxml
Editor/Inspector/UIElementsUtil.cs
Editor/Localization/Localization.cs
Editor/Localization/en-US.json      (the `error.syncparamsequence.*` and `error.singleton` strings)
Editor/Localization/zh-Hans.json    (ditto)
Editor/PluginDefinition/PluginDefinition.cs
Editor/SyncParameterSequence/AvatarRecord.cs
Editor/SyncParameterSequence/MaSyncParameterSequenceDetector.cs
Editor/SyncParameterSequence/ParameterDefinition.cs
Editor/SyncParameterSequence/ParameterInfoRecord.cs
Editor/SyncParameterSequence/ParameterInfoStore.cs
Editor/SyncParameterSequence/ValueType.cs
Runtime/FuryParameterCompressor.cs
```

Additionally, `Editor/Editor-Avatars/Service/Compressor/CompressorLayerUtilsService.cs` and
`Editor/Editor-Avatars/Service/Compressor/ParameterPlatformAlignmentService.cs`
contain code ported from Modular Avatar alongside their VRCFury-derived parts; see the comments in those files.

No Modular Avatar image assets are included, so the image restriction in Modular Avatar's `COPYING.md` does not apply here.

```
MIT License

Copyright (c) 2022 bd_

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
```

---

## 3. NDMF

No NDMF (`nadena.dev.ndmf`) source code is included in this package; it is used only through its public API and is installed separately as a dependency.

NDMF is MIT licensed, Copyright (c) 2023-2024 bd_ (and other contributors).

---

## 4. Original portions

Everything not covered by sections 1-3 is dedicated to the public domain under
**CC0 1.0 Universal (CC0 1.0) Public Domain Dedication**:

> To the extent possible under law, FOSA has waived all copyright and related or neighboring rights to the original portions of Fury Parameter Compressor.
>
> Full legal code: https://creativecommons.org/publicdomain/zero/1.0/legalcode

Note that CC0 applies only to those original portions. It does **not** lift the restriction in section 1:
because the package as a whole is a derivative work of VRCFury, the package as a whole remains limited to personal purposes.
