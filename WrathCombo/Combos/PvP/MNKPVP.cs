using WrathCombo.CustomComboNS;
using WrathCombo.CustomComboNS.Functions;
using static WrathCombo.Window.Functions.UserConfig;
using static WrathCombo.Combos.PvP.MNKPvP.Config;

namespace WrathCombo.Combos.PvP;

internal static class MNKPvP
{
    #region IDS
    internal class Role : PvPMelee;

    public const uint
        PhantomRushCombo = 55,
        DragonKick = 29475,
        TwinSnakes = 29476,
        Demolish = 29477,
        PhantomRush = 29478,
        RisingPhoenix = 29481,
        RiddleOfEarth = 29482,
        Thunderclap = 29484,
        EarthsReply = 29483,
        Meteordrive = 29485,
        WindsReply = 41509,
        FiresReply = 41448,
        LeapingOpo = 41444,
        RisingRaptor = 41445,
        PouncingCoeurl = 41446;

    public static class Buffs
    {
        public const ushort
            FiresRumination = 4301,
            FireResonance = 3170,
            EarthResonance = 3171;
    }
    public static class Debuffs
    {
        public const ushort
            PressurePoint = 3172;
    }
    #endregion

    #region Config
    public static class Config
    {
        public static UserInt
            MNKPvP_SmiteThreshold = new("MNKPvP_SmiteThreshold");

        internal static void Draw(Preset preset)
        {
            switch (preset)
            {
                case Preset.MNKPvP_Smite:
                    DrawSliderInt(0, 100, MNKPvP_SmiteThreshold, "Target HP% to smite, Max damage below 25%");
                    break;
            }
        }
    }
    #endregion
       
    internal class MNKPvP_Burst : CustomCombo
    {
        protected internal override Preset Preset => Preset.MNKPvP_Burst;
        protected override uint Invoke(uint actionID)
        {
            if (actionID is not (DragonKick or TwinSnakes or Demolish or LeapingOpo or RisingRaptor or PouncingCoeurl or PhantomRush)) 
                return actionID;
            
            if (IsEnabled(Preset.MNKPvP_Burst_Meteodrive) && PvPCommon.TargetImmuneToDamage() && GetTargetCurrentHP() <= 20000 && IsLB1Ready)
                return Meteordrive;

            if (!PvPCommon.TargetImmuneToDamage())
            {
                if (IsEnabled(Preset.MNKPvP_Smite) && PvPMelee.CanSmite() && InActionRange(PvPMelee.Smite) && HasTarget() &&
                    GetTargetHPPercent() <= MNKPvP_SmiteThreshold)
                    return PvPMelee.Smite;
                
                if (HasStatusEffect(Buffs.FireResonance) && ComboAction is PouncingCoeurl)
                    return actionID;

                if (IsEnabled(Preset.MNKPvP_Burst_RisingPhoenix) && NumberOfEnemiesInRange(RisingPhoenix) >= 1 &&
                    (!HasStatusEffect(Buffs.FireResonance) && GetRemainingCharges(RisingPhoenix) > 1 || // capped on charges
                     ComboAction is PouncingCoeurl && GetRemainingCharges(RisingPhoenix) > 0)) // use last charge to buff phantom rush
                    return OriginalHook(RisingPhoenix);

                if (IsEnabled(Preset.MNKPvP_Burst_RiddleOfEarth) && !HasStatusEffect(Buffs.EarthResonance) && IsOffCooldown(RiddleOfEarth) && PlayerHealthPercentageHp() <= 95 || //Pop Riddle of earth
                    HasStatusEffect(Buffs.EarthResonance) && GetStatusEffectRemainingTime(Buffs.EarthResonance) <= 2) //Fire earths reply before it expires
                    return OriginalHook(RiddleOfEarth);

                if (IsEnabled(Preset.MNKPvP_Burst_Thunderclap) && GetRemainingCharges(Thunderclap) > 0 && !InMeleeRange())
                    return OriginalHook(Thunderclap);

                if (IsEnabled(Preset.MNKPvP_Burst_WindsReply) && InActionRange(WindsReply) && IsOffCooldown(WindsReply))
                    return WindsReply;

                if (IsEnabled(Preset.MNKPvP_Burst_FiresReply) && GetRemainingCharges(FiresReply) > 0 && ComboAction is not (PouncingCoeurl or LeapingOpo or RisingRaptor))
                    return OriginalHook(FiresReply);

            }
            return actionID;
        }
    }

    /// <summary>
    /// Cooldown-aware, single-target Monk PvP burst.
    /// </summary>
    internal class MNKPvP_BurstV2 : CustomCombo
    {
        protected internal override Preset Preset => Preset.MNKPvP_BurstV2;

        protected override uint Invoke(uint actionID)
        {
            if (actionID is not (DragonKick or TwinSnakes or Demolish or LeapingOpo or RisingRaptor or PouncingCoeurl or PhantomRush))
                return actionID;

            uint adjustedCombo = OriginalHook(actionID);
            if (!HasBattleTarget() || TargetIsDead())
                return adjustedCombo;

            bool hasBind = HasStatusEffect(PvPCommon.Debuffs.Bind, anyOwner: true);
            bool targetHasGuard = HasStatusEffect(PvPCommon.Buffs.Guard, CurrentTarget, true);
            bool targetHasInvulnerability = PvPCommon.TargetImmuneToDamage(false);
            bool targetHasReduction = PvPCommon.TargetImmuneToDamage();
            bool targetHasResilience = HasStatusEffect(PvPCommon.Buffs.Resilience, CurrentTarget, true);
            bool targetHasPressurePoint = HasStatusEffect(Debuffs.PressurePoint, CurrentTarget);
            bool hasFireResonance = HasStatusEffect(Buffs.FireResonance);
            bool hasEarthReply = OriginalHook(RiddleOfEarth) is EarthsReply;
            bool phantomRushReady = adjustedCombo is PhantomRush || ComboAction is PouncingCoeurl;
            bool phantomRushJustUsed = JustUsed(PhantomRush, 4f);
            bool isPlayerTargeted = IsPlayerTargeted();

            float earthRemaining = GetStatusEffectRemainingTime(Buffs.EarthResonance);
            float fireRemaining = GetStatusEffectRemainingTime(Buffs.FireResonance);
            uint targetMaxHp = GetTargetMaxHP();
            float targetEffectiveHp = targetMaxHp *
                                      GetTargetHPPercent(CurrentTarget, includeShield: true, forceUsePending: true) /
                                      100f;
            bool hasReliableTargetHp = targetMaxHp > 0 && targetEffectiveHp > 0;
            bool meteodriveIsLethal = hasReliableTargetHp &&
                                      targetEffectiveHp <= 24000f;
            uint thunderclapCharges = GetRemainingCharges(Thunderclap);
            uint risingPhoenixCharges = GetRemainingCharges(RisingPhoenix);
            uint firesReplyCharges = GetRemainingCharges(FiresReply);

            // Hold the full eight-second collection window. Earth's Reply only
            // becomes urgent in its final second, maximizing stored damage/healing.
            if (hasEarthReply && earthRemaining <= 1f)
                return OriginalHook(RiddleOfEarth);

            // Meteodrive removes Guard. Do this before ordinary immunity checks.
            if (IsLB1Ready &&
                targetHasGuard &&
                !targetHasInvulnerability &&
                !hasBind &&
                InActionRange(Meteordrive))
                return Meteordrive;

            if (targetHasInvulnerability)
                return adjustedCombo;

            if (PvPMelee.CanSmite() &&
                InActionRange(PvPMelee.Smite) &&
                GetTargetHPPercent() <= 25)
                return PvPMelee.Smite;

            // An early Wind execute has already committed Pressure Point. Finish
            // it at range rather than spending Thunderclap or waiting for Phantom.
            if (targetHasPressurePoint && !phantomRushReady)
            {
                if (firesReplyCharges > 0 && InActionRange(FiresReply))
                {
                    // Pressure Point plus an unbuffed Fire is already 20k.
                    // Do not spend the second Phoenix charge unless its 24k
                    // empowered package is actually required to finish.
                    if (hasFireResonance || targetEffectiveHp <= 20000f)
                        return OriginalHook(FiresReply);

                    float empoweredPressureFire = 24000f +
                                                    (GetTargetDistance() <= 6 ? 4000f : 0f);

                    if (!hasFireResonance &&
                        risingPhoenixCharges > 0 &&
                        targetEffectiveHp <= empoweredPressureFire)
                        return OriginalHook(RisingPhoenix);

                    // The prediction can shift as mitigation or healing lands;
                    // preserve Pressure Point with the strongest available hit.
                    if (!hasFireResonance && risingPhoenixCharges > 0)
                        return OriginalHook(RisingPhoenix);

                    return OriginalHook(FiresReply);
                }
            }

            // Choose the smallest Fire/Wind package that is already lethal
            // instead of continuing to build the full Phantom Rush burst.
            if (hasReliableTargetHp &&
                !targetHasReduction &&
                firesReplyCharges > 0 &&
                InActionRange(FiresReply))
            {
                float phoenixDamage = GetTargetDistance() <= 6 ? 4000f : 0f;

                // An unbuffed Fire's Reply is the fastest possible ranged kill.
                if (!hasFireResonance && targetEffectiveHp <= 8000f)
                    return OriginalHook(FiresReply);

                // One empowered Fire's Reply is the next-smallest package.
                if (!hasFireResonance &&
                    risingPhoenixCharges > 0 &&
                    targetEffectiveHp <= 12000f + phoenixDamage)
                    return OriginalHook(RisingPhoenix);

                if (hasFireResonance)
                {
                    if (targetEffectiveHp <= 12000f)
                        return OriginalHook(FiresReply);

                    // Fire Resonance is already armed. If Wind can make the
                    // remaining Wind/Pressure Point/Fire package lethal, use it.
                    float armedWindFireDamage = 32000f +
                                                (risingPhoenixCharges > 0 ? 4000f : 0f);

                    if (IsOffCooldown(WindsReply) &&
                        !targetHasResilience &&
                        InActionRange(WindsReply) &&
                        targetEffectiveHp <= armedWindFireDamage)
                        return WindsReply;

                    uint additionalPairs = System.Math.Min(firesReplyCharges - 1, risingPhoenixCharges);
                    uint additionalUnbuffedFires = firesReplyCharges - 1 - additionalPairs;
                    float remainingFirePackage = 12000f +
                                                 additionalPairs * (12000f + phoenixDamage) +
                                                 additionalUnbuffedFires * 8000f;

                    if (targetEffectiveHp <= remainingFirePackage)
                        return OriginalHook(FiresReply);
                }
                else
                {
                    // Prefer Wind over a second Fire charge: empowered Wind is
                    // 12k, then Pressure Point adds another 12k to the empowered
                    // Fire. A Phoenix hit before Wind contributes another 4k.
                    float windFireDamage = 28000f +
                                           System.Math.Min(risingPhoenixCharges, 2u) * 4000f +
                                           (risingPhoenixCharges > 0 ? phoenixDamage : 0f);

                    if (IsOffCooldown(WindsReply) &&
                        !targetHasResilience &&
                        InActionRange(WindsReply) &&
                        targetEffectiveHp <= windFireDamage)
                    {
                        if (risingPhoenixCharges > 0)
                            return OriginalHook(RisingPhoenix);

                        return WindsReply;
                    }

                    uint availablePairs = System.Math.Min(firesReplyCharges, risingPhoenixCharges);
                    uint availableUnbuffedFires = firesReplyCharges - availablePairs;
                    float availableFirePackage = availablePairs * (12000f + phoenixDamage) +
                                                 availableUnbuffedFires * 8000f;

                    if (targetEffectiveHp <= availableFirePackage)
                        return availablePairs > 0
                            ? OriginalHook(RisingPhoenix)
                            : OriginalHook(FiresReply);

                }
            }

            // Do not hold a lethal LB for the formal burst window. Pending HP
            // changes and the target's barrier are included in this calculation.
            if (IsLB1Ready &&
                meteodriveIsLethal &&
                !targetHasReduction &&
                !hasBind &&
                InActionRange(Meteordrive))
                return Meteordrive;

            // Pressure Point belongs on Phantom Rush. Thunderclap supplies its
            // defensive barrier while reconnecting without consuming either buff.
            if (targetHasPressurePoint)
            {
                if (!InMeleeRange() &&
                    !hasBind &&
                    thunderclapCharges > 0 &&
                    InActionRange(Thunderclap))
                    return OriginalHook(Thunderclap);

                if (!hasFireResonance &&
                    risingPhoenixCharges > 0 &&
                    GetTargetDistance() <= 6)
                    return OriginalHook(RisingPhoenix);

                if (phantomRushReady && InActionRange(adjustedCombo))
                    return adjustedCombo;

                // If reconnecting is impossible, at least cash out the short
                // Pressure Point window with a ranged weaponskill.
                if (firesReplyCharges > 0 && InActionRange(FiresReply))
                    return OriginalHook(FiresReply);
            }

            if (targetHasReduction)
                return adjustedCombo;

            // The target has already taken the compressed Wind/Phantom package.
            // Meteodrive is now a finisher instead of a telegraphed opener.
            if (phantomRushJustUsed)
            {
                if (IsLB1Ready && !hasBind && InActionRange(Meteordrive))
                    return Meteordrive;

                if (firesReplyCharges > 0 && InActionRange(FiresReply))
                    return OriginalHook(FiresReply);
            }

            // Start the defensive collection window after all immediate kill
            // checks and committed Pressure Point sequences, so a fresh Riddle
            // never delays a confirmed execute.
            if (!hasEarthReply &&
                IsOffCooldown(RiddleOfEarth) &&
                InCombat())
                return OriginalHook(RiddleOfEarth);

            // Avoid losing an existing Fire Resonance while preparing the combo.
            if (hasFireResonance && fireRemaining <= 2f && InActionRange(adjustedCombo))
                return adjustedCombo;

            if (phantomRushReady)
            {
                // With two Rising Phoenix charges, empower Wind's Reply first
                // while reserving the second 50% buff for Phantom Rush.
                if (IsOffCooldown(WindsReply) &&
                    !targetHasResilience)
                {
                    if (!hasFireResonance && risingPhoenixCharges > 1)
                    {
                        if (GetTargetDistance() > 6 &&
                            !hasBind &&
                            thunderclapCharges > 1 &&
                            InActionRange(Thunderclap))
                            return OriginalHook(Thunderclap);

                        if (GetTargetDistance() <= 6)
                            return OriginalHook(RisingPhoenix);
                    }

                    if ((hasFireResonance || risingPhoenixCharges <= 1) &&
                        InActionRange(WindsReply))
                        return WindsReply;
                }

                if (!InMeleeRange() &&
                    !hasBind &&
                    thunderclapCharges > 0 &&
                    InActionRange(Thunderclap))
                    return OriginalHook(Thunderclap);

                if (!hasFireResonance &&
                    risingPhoenixCharges > 0 &&
                    GetTargetDistance() <= 6)
                    return OriginalHook(RisingPhoenix);

                if (InActionRange(adjustedCombo))
                    return adjustedCombo;
            }

            // During the six-hit preparation, spend only surplus Thunderclap
            // charges. This refreshes an 8,000 barrier while reserving one charge
            // for the mandatory reconnect after Wind's Reply.
            if (!phantomRushReady &&
                !hasBind &&
                thunderclapCharges > 1 &&
                (isPlayerTargeted || !InMeleeRange()) &&
                InActionRange(Thunderclap))
                return OriginalHook(Thunderclap);

            return adjustedCombo;
        }
    }
}
