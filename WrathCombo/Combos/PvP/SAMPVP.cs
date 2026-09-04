using WrathCombo.CustomComboNS;
using WrathCombo.CustomComboNS.Functions;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using System.Linq;
using WrathCombo.Extensions;
using static WrathCombo.Window.Functions.UserConfig;
using static WrathCombo.Combos.PvP.SAMPvP.Config;

namespace WrathCombo.Combos.PvP;

internal static class SAMPvP
{
    #region IDS
    internal class Role : PvPMelee;
    public const uint
        KashaCombo = 58,
        Yukikaze = 29523,
        Gekko = 29524,
        Kasha = 29525,
        Hyosetsu = 29526,
        Mangetsu = 29527,
        Oka = 29528,
        OgiNamikiri = 29530,
        Soten = 29532,
        Chiten = 29533,
        Mineuchi = 29535,
        MeikyoShisui = 29536,
        Midare = 29529,
        Kaeshi = 29531,
        Zantetsuken = 29537,
        TendoSetsugekka = 41454,
        TendoKaeshiSetsugekka = 41455,
        Zanshin = 41577;

    public static class Buffs
    {
        public const ushort
            Chiten = 1240,
            ZanshinReady = 1318,
            MeikyoShisui = 1320,
            Kaiten = 3201,
            TendoSetsugekkaReady = 3203;
    }

    public static class Debuffs
    {
        public const ushort
            Kuzushi = 3202;
    }
    #endregion

    #region Config
    public static class Config
    {
        public static UserInt
            SAMPvP_Soten_Range = new("SAMPvP_Soten_Range", 3),
            SAMPvP_Soten_Charges = new("SAMPvP_Soten_Charges", 1),
            SAMPvP_Chiten_PlayerHP = new("SAMPvP_Chiten_PlayerHP", 70),
            SAMPvP_Mineuchi_TargetHP = new("SAMPvP_Mineuchi_TargetHP", 40),
            SAMPvP_SmiteThreshold = new("SAMPvP_SmiteThreshold", 25),
            SAMPvP_BurstV2_MinKuzushiTargets = new("SAMPvP_BurstV2_MinKuzushiTargets", 1),
            SAMPvP_BurstV2_SotenCharges = new("SAMPvP_BurstV2_SotenCharges", 0),
            SAMPvP_BurstV2_ChitenHP = new("SAMPvP_BurstV2_ChitenHP", 85);

        public static UserBool
            SAMPvP_Soten_SubOption = new("SAMPvP_Soten_SubOption"),
            SAMPvP_Mineuchi_SubOption = new("SAMPvP_Mineuchi_SubOption"),
            SAMPvP_BurstV2_SingleTargetOgi = new("SAMPvP_BurstV2_SingleTargetOgi");

        internal static void Draw(Preset preset)
        {
            switch (preset)
            {
                // Chiten
                case Preset.SAMPvP_Chiten:
                    DrawSliderInt(10, 100, SAMPvP_Chiten_PlayerHP, "Player HP%");
                    break;

                // Mineuchi
                case Preset.SAMPvP_Mineuchi:
                    DrawSliderInt(10, 100, SAMPvP_Mineuchi_TargetHP, "Target HP%");
                    DrawAdditionalBoolChoice(SAMPvP_Mineuchi_SubOption, "Burst Preparation", "Also uses Mineuchi before Tendo Setsugekka.");
                    break;

                // Soten
                case Preset.SAMPvP_Soten:
                    DrawSliderInt(0, 2, SAMPvP_Soten_Charges, "Charges to Keep");
                    DrawSliderInt(1, 10, SAMPvP_Soten_Range, "Maximum Range");
                    DrawAdditionalBoolChoice(SAMPvP_Soten_SubOption, "Yukikaze Only", "Also requires next weaponskill to be Yukikaze.");
                    break;

                // Smite
                case Preset.SAMPvP_Smite:
                    DrawSliderInt(0, 100, SAMPvP_SmiteThreshold,
                        "Target HP% to smite, Max damage below 25%");
                    break;

                // Burst V2
                case Preset.SAMPvP_BurstV2:
                    DrawSliderInt(1, 5, SAMPvP_BurstV2_MinKuzushiTargets,
                        "Minimum Kuzushi targets near the selected target before using Zantetsuken");
                    DrawSliderInt(0, 2, SAMPvP_BurstV2_SotenCharges,
                        "Soten charges to keep");
                    DrawSliderInt(10, 100, SAMPvP_BurstV2_ChitenHP,
                        "Use Chiten when targeted below player HP%");
                    DrawAdditionalBoolChoice(SAMPvP_BurstV2_SingleTargetOgi,
                        "Single-target Ogi",
                        "Only uses Ogi Namikiri when its cone will hit exactly one target for maximum single-target potency. Leave disabled for automatic defensive AoE use and shielding.");
                    break;
            }
        }
    }
    #endregion
       
    internal class SAMPvP_BurstMode : CustomCombo
    {
        protected internal override Preset Preset => Preset.SAMPvP_Burst;

        protected override uint Invoke(uint actionID)
        {
            if (actionID is not (Yukikaze or Gekko or Kasha)) return actionID;

            #region Variables
            float targetCurrentPercentHp = GetTargetHPPercent();
            float playerCurrentPercentHp = PlayerHealthPercentageHp();
            uint chargesSoten = HasCharges(Soten) ? GetCooldown(Soten).RemainingCharges : 0;
            bool isMoving = IsMoving();
            bool inCombat = InCombat();
            bool hasTarget = HasTarget();
            bool hasKaiten = HasStatusEffect(Buffs.Kaiten);
            bool hasZanshin = OriginalHook(Chiten) is Zanshin;
            bool hasBind = HasStatusEffect(PvPCommon.Debuffs.Bind, anyOwner: true);
            bool targetHasImmunity = PvPCommon.TargetImmuneToDamage();
            bool isTargetPrimed = hasTarget && !targetHasImmunity;
            bool targetHasKuzushi = HasStatusEffect(Debuffs.Kuzushi, CurrentTarget);
            bool hasKaeshiNamikiri = OriginalHook(OgiNamikiri) is Kaeshi;
            bool hasTendo = OriginalHook(MeikyoShisui) is TendoSetsugekka;
            bool isYukikazePrimed = ComboTimer == 0 || ComboAction is Kasha;
            bool hasTendoKaeshi = OriginalHook(MeikyoShisui) is TendoKaeshiSetsugekka;
            bool hasPrioWeaponskill = hasTendo || hasTendoKaeshi || hasKaeshiNamikiri;
            bool isMeikyoPrimed = IsOnCooldown(OgiNamikiri) && !hasKaeshiNamikiri && !hasKaiten && !isMoving;
            bool isZantetsukenPrimed = IsLB1Ready && !hasBind && hasTarget && targetHasKuzushi && InActionRange(Zantetsuken);
            bool isSotenPrimed = chargesSoten > SAMPvP_Soten_Charges && !hasKaiten && !hasBind && !hasPrioWeaponskill;
            bool isTargetInvincible = HasStatusEffect(PLDPvP.Buffs.HallowedGround, CurrentTarget, true) || HasStatusEffect(DRKPvP.Buffs.UndeadRedemption, CurrentTarget, true);
            #endregion

            // Zantetsuken
            if (IsEnabled(Preset.SAMPvP_Zantetsuken) && isZantetsukenPrimed && !isTargetInvincible)
                return OriginalHook(Zantetsuken);

            //Smite
            if (IsEnabled(Preset.SAMPvP_Smite) && PvPMelee.CanSmite() && !PvPCommon.TargetImmuneToDamage() && InActionRange(PvPMelee.Smite) && HasTarget() &&
                GetTargetHPPercent() <= SAMPvP_SmiteThreshold)
                return PvPMelee.Smite;

            // Chiten
            if (IsEnabled(Preset.SAMPvP_Chiten) && IsOffCooldown(Chiten) && inCombat && playerCurrentPercentHp < SAMPvP_Chiten_PlayerHP)
                return OriginalHook(Chiten);

            if (isTargetPrimed)
            {
                // Zanshin
                if (hasZanshin && InActionRange(Zanshin))
                    return Zanshin;

                // Soten
                if (IsEnabled(Preset.SAMPvP_Soten) && isSotenPrimed && GetTargetDistance() <= SAMPvP_Soten_Range &&
                    (!SAMPvP_Soten_SubOption || (SAMPvP_Soten_SubOption && isYukikazePrimed)))
                    return OriginalHook(Soten);

                if (InActionRange(Mineuchi))
                {
                    // Meikyo Shisui
                    if (IsEnabled(Preset.SAMPvP_Meikyo) && IsOffCooldown(MeikyoShisui) && isMeikyoPrimed)
                        return OriginalHook(MeikyoShisui);

                    // Mineuchi
                    if (IsEnabled(Preset.SAMPvP_Mineuchi) && IsOffCooldown(Mineuchi) && !HasBattleTarget() &&
                        (targetCurrentPercentHp < SAMPvP_Mineuchi_TargetHP || (SAMPvP_Mineuchi_SubOption && hasTendo && !hasKaiten)))
                        return OriginalHook(Mineuchi);
                }
            }

            // Tendo Kaeshi Setsugekka
            if (hasTendoKaeshi)
                return OriginalHook(MeikyoShisui);

            // Kaeshi Namikiri
            if (hasKaeshiNamikiri)
                return OriginalHook(OgiNamikiri);

            // Kaiten
            if (hasKaiten)
                return OriginalHook(actionID);

            if (!isMoving && isTargetPrimed)
            {
                // Tendo Setsugekka
                if (hasTendo)
                    return OriginalHook(MeikyoShisui);

                // Ogi Namikiri
                if (IsOffCooldown(OgiNamikiri))
                    return OriginalHook(OgiNamikiri);
            }
            return actionID;
        }
    }

    /// <summary>
    /// Aggressive one-button PvP burst that prepares the basic combo for
    /// Hyosetsu, enters burst safely, and consumes expiring follow-up actions.
    /// </summary>
    internal class SAMPvP_BurstModeV2 : CustomCombo
    {
        protected internal override Preset Preset => Preset.SAMPvP_BurstV2;

        protected override uint Invoke(uint actionID)
        {
            if (actionID is not (Yukikaze or Gekko or Kasha))
                return actionID;

            uint adjustedCombo = OriginalHook(actionID);
            bool hasBattleTarget = HasBattleTarget();
            bool targetIsPlayer = CurrentTarget is IPlayerCharacter;
            bool validTarget = hasBattleTarget && targetIsPlayer && !TargetIsDead();

            if (!validTarget)
                return adjustedCombo;

            bool isMoving = IsMoving();
            bool hasBind = HasStatusEffect(PvPCommon.Debuffs.Bind, anyOwner: true);
            bool hasKaiten = HasStatusEffect(Buffs.Kaiten);
            bool hasChiten = HasStatusEffect(Buffs.Chiten);
            bool hasZanshin = OriginalHook(Chiten) is Zanshin;
            bool hasKaeshiNamikiri = OriginalHook(OgiNamikiri) is Kaeshi;
            bool hasTendo = OriginalHook(MeikyoShisui) is TendoSetsugekka;
            bool hasTendoKaeshi = OriginalHook(MeikyoShisui) is TendoKaeshiSetsugekka;
            float zanshinRemaining = LocalPlayer?.Status(Buffs.ZanshinReady).RemainingTimeOrZero() ?? 0f;
            float playerHpPercent = PlayerHealthPercentageHp();
            bool isPlayerTargeted = IsPlayerTargeted();
            bool targetHasKuzushi = HasStatusEffect(Debuffs.Kuzushi, CurrentTarget);
            bool isYukikazePrimed = ComboTimer == 0 || ComboAction is Kasha;
            bool targetHasInvulnerability = PvPCommon.TargetImmuneToDamage(false);
            bool targetHasReduction = PvPCommon.TargetImmuneToDamage();

            uint sotenCharges = GetRemainingCharges(Soten);
            int ogiTargetCount = NumberOfEnemiesInRange(OgiNamikiri, CurrentTarget);
            bool ogiTargetModeAllowsUse = !SAMPvP_BurstV2_SingleTargetOgi || ogiTargetCount == 1;
            bool ogiReady = IsOffCooldown(OgiNamikiri) && !hasKaeshiNamikiri && ogiTargetModeAllowsUse;
            bool tendoReady = hasTendo || hasTendoKaeshi;

            int markedTargets = EnemiesInRange(Zantetsuken, CurrentTarget)
                .Count(target => target is IPlayerCharacter &&
                                 HasStatusEffect(Debuffs.Kuzushi, target));

            float targetEffectiveHpPercent = CurrentTarget is { } target
                ? GetTargetHPPercent(target) + target.ShieldPercentage()
                : 101f;

            bool zantetsukenReady = IsLB1Ready &&
                                     !hasBind &&
                                     targetHasKuzushi &&
                                     markedTargets >= SAMPvP_BurstV2_MinKuzushiTargets &&
                                     targetEffectiveHpPercent <= 100f &&
                                     InActionRange(Zantetsuken) &&
                                     !targetHasInvulnerability;

            // A lethal LB always wins. Guard is intentionally ignored because
            // Zantetsuken bypasses it; barriers are accounted for above.
            if (zantetsukenReady)
                return OriginalHook(Zantetsuken);

            // Hold Chiten's follow-up for almost its full ten-second window.
            // At two seconds remaining, Zanshin becomes urgent and takes
            // priority so the stored attack and heal are not lost.
            bool expiringZanshin = hasZanshin &&
                                   zanshinRemaining <= 2f &&
                                   InActionRange(Zanshin);

            if (expiringZanshin)
                return OriginalHook(Chiten);

            // Chiten is a defensive reserve, not an automatic pre-dive button.
            // Use it only once incoming attention has translated into damage;
            // at critical HP, use it even if the attacker changed targets.
            bool chitenNeeded = !hasChiten &&
                                !hasZanshin &&
                                IsOffCooldown(Chiten) &&
                                InCombat() &&
                                playerHpPercent <= SAMPvP_BurstV2_ChitenHP &&
                                (isPlayerTargeted || playerHpPercent <= 40);

            if (chitenNeeded)
                return OriginalHook(Chiten);

            // Smite also ignores Guard and should secure an execute before a
            // longer cast sequence gives the target time to recover.
            if (PvPMelee.CanSmite() &&
                !targetHasInvulnerability &&
                InActionRange(PvPMelee.Smite) &&
                GetTargetHPPercent() <= 25)
                return PvPMelee.Smite;

            // Never strand either instant follow-up after its opening cast.
            if (hasTendoKaeshi && InActionRange(TendoKaeshiSetsugekka))
                return OriginalHook(MeikyoShisui);

            if (hasKaeshiNamikiri && InActionRange(Kaeshi))
                return OriginalHook(OgiNamikiri);

            // Do not spend cooldowns into Guard or true invulnerability.
            if (targetHasReduction)
                return adjustedCombo;

            // Prepare the combo before starting timed buffs. This is the key
            // that makes every Soten lead to Hyosetsu rather than Mangetsu/Oka.
            if (!isYukikazePrimed && !hasKaiten && !tendoReady && !hasZanshin)
                return adjustedCombo;

            bool canEngage = InActionRange(Soten) ||
                             InActionRange(OgiNamikiri) ||
                             InActionRange(TendoSetsugekka);

            // Meikyo is the pre-buff: it supplies CC protection and arms Tendo.
            if (!hasTendo && !hasTendoKaeshi && IsOffCooldown(MeikyoShisui) && canEngage)
                return OriginalHook(MeikyoShisui);

            // Spend Soten only when Yukikaze is next. Keeping this invariant
            // prevents unwanted Mangetsu and Oka transformations.
            if (!hasKaiten &&
                isYukikazePrimed &&
                !hasBind &&
                sotenCharges > SAMPvP_BurstV2_SotenCharges &&
                InActionRange(Soten))
                return OriginalHook(Soten);

            // The prepared Soten can now only resolve to Hyosetsu. Use the
            // game's adjusted combo action so the normal combo state remains valid.
            if (hasKaiten)
                return adjustedCombo;

            // Mineuchi places its short damage window directly around the
            // strongest available pair and extends Kuzushi when present.
            if (IsOffCooldown(Mineuchi) &&
                InActionRange(Mineuchi) &&
                (ogiReady || tendoReady || targetHasKuzushi))
                return OriginalHook(Mineuchi);

            // Ogi is first because its single-target pair is the largest burst;
            // in a group it supplies AoE damage and the defensive barrier.
            if (ogiReady && !isMoving && InActionRange(OgiNamikiri))
                return OriginalHook(OgiNamikiri);

            if (hasTendo && !isMoving && InActionRange(TendoSetsugekka))
                return OriginalHook(MeikyoShisui);

            return adjustedCombo;
        }
    }
}
