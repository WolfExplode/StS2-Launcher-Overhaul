using System;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Settings;

namespace STS2Mobile.Patches;

// Scales combat room backgrounds and creature positions for non-standard aspect ratios.
// On taller screens the background container is scaled up so no black bars appear
// behind the combat scene.
internal static class CombatBackgroundPatches
{
    private const string AdjustCreatureScaleForAspectRatioMethod =
        "AdjustCreatureScaleForAspectRatio";
    private const float BackgroundRatio = 2764.8f / 1296f;
    private const string BgContainerNode = "%BgContainer";
    private const string ReadyMethod = "_Ready";
    private const string RoomTypeName = "MegaCrit.Sts2.Core.Nodes.Rooms.NCombatRoom";
    private const string SetUpBackgroundMethod = "SetUpBackground";

    internal static void Apply(Harmony harmony)
    {
        var sts2Asm = typeof(MegaCrit.Sts2.Core.Nodes.NGame).Assembly;
        var combatRoomType = sts2Asm.GetType(RoomTypeName);
        if (combatRoomType == null)
            return;

        PatchHelper.Patch(
            harmony,
            combatRoomType,
            SetUpBackgroundMethod,
            postfix: PatchHelper.Method(
                typeof(CombatBackgroundPatches),
                nameof(SetUpBackgroundPostfix)
            )
        );

        PatchHelper.Patch(
            harmony,
            combatRoomType,
            ReadyMethod,
            postfix: PatchHelper.Method(
                typeof(CombatBackgroundPatches),
                nameof(CombatRoomReadyPostfix)
            )
        );
    }

    private static void SetUpBackgroundPostfix(object __instance)
    {
        try
        {
            var room = (Control)__instance;
            ScaleBackground(room);
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"[CombatBg] SetUpBackgroundPostfix failed: {ex.Message}");
        }
    }

    private static void CombatRoomReadyPostfix(object __instance)
    {
        try
        {
            var room = (Control)__instance;
            DeferAdjust(room, AccessTools.Method(room.GetType(), AdjustCreatureScaleForAspectRatioMethod));
        }
        catch (Exception ex)
        {
            PatchHelper.Log($"[CombatBg] CombatRoomReadyPostfix failed: {ex.Message}");
        }
    }

    private static void ScaleBackground(Control room)
    {
        if (SaveManager.Instance.SettingsSave.AspectRatioSetting != AspectRatioSetting.Auto)
            return;

        var window = room.GetTree().Root;
        var vpSize = window.GetVisibleRect().Size;
        float vpRatio = vpSize.X / vpSize.Y;

        var bgContainer = room.GetNodeOrNull<Control>(BgContainerNode);
        if (bgContainer == null)
            return;

        var scaleNeeded = vpRatio >= BackgroundRatio ? 1f : BackgroundRatio / vpRatio;
        if (scaleNeeded <= 1f)
        {
            bgContainer.Scale = Vector2.One;
            return;
        }

        bgContainer.Scale = new Vector2(scaleNeeded, scaleNeeded);
        PatchHelper.Log(
            $"[CombatBg] Scaled BgContainer by {scaleNeeded:F3} for viewport ratio {vpRatio:F2}"
        );
    }

    // Deferred so layout dimensions are finalized before adjusting.
    private static void DeferAdjust(Control room, MethodInfo adjustMethod)
    {
        if (adjustMethod == null)
            return;

        Callable
            .From(() =>
            {
                try
                {
                    if (GodotObject.IsInstanceValid(room) && room.IsInsideTree())
                        adjustMethod.Invoke(room, null);
                }
                catch (Exception ex)
                {
                    PatchHelper.Log($"[CombatBg] Deferred adjust failed: {ex.Message}");
                }
            })
            .CallDeferred();
    }
}

