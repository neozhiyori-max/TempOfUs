# tempMOD — Notices and Source Availability

`tempMOD` is distributed under the **GNU General Public License, version 3.0 or later**. The full license text is included in [`LICENSE`](./LICENSE).

## SuperNewRoles notice

This project contains a derived **minimal SNR foundation** from [SuperNewRoles/SuperNewRoles](https://github.com/SuperNewRoles/SuperNewRoles), fixed at commit `713c98779e14000479f7578a28705264645f07e5`. SuperNewRoles is licensed under GPL-3.0, and the copied source remains available in `src/TempMod.SnrBase/` together with its upstream copyright and license terms.

The derived SNR project is currently compiled as `TempMod.SnrBase.dll` and is intentionally a **no-feature foundation**. Its entry point has been changed to use the tempMOD identifier and has had the following SNR startup calls disabled: role registration, role options, custom RPC registration, Harmony patch-all, external API and analytics initialization, custom servers and region changes, announcements, custom cosmetics, trophies, in-game requests, CPU affinity modification, and SNR update checks. No SNR role is active in this initial foundation.

The fixed source reference, current-game build audit, and adoption architecture are published in [`docs/SNR_BASELINE_ADOPTION_AUDIT_JA.md`](./docs/SNR_BASELINE_ADOPTION_AUDIT_JA.md) and [`docs/SNR_ADOPTION_ARCHITECTURE_JA.md`](./docs/SNR_ADOPTION_ARCHITECTURE_JA.md).

## Corresponding source

When distributing a compiled `TempMod.SnrBase.dll`, distribute or provide a direct link to the complete corresponding source code for the exact build, together with this notice and the full GPL-3.0 license text. Preserve upstream copyright and license notices for all files copied or modified from SuperNewRoles.

## Game notice

Among Us and associated game assets are property of Innersloth LLC. tempMOD is an independent community modification and is not affiliated with or endorsed by Innersloth LLC.
