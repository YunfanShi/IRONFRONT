using System;
using UnityEngine;

namespace Ironfront.UnityPrototype
{
    // Matches the four web-game classes; the prototype can assign a different
    // primary weapon to a class without changing its gadget rules.
    public enum PrototypeInfantryClass { Assault, Medic, Recon, Engineer }
    public enum PrototypeWeaponId { Carbine, Marksman, Smg, Lmg, BattleRifle, Sniper, Pistol }

    public sealed class PrototypeWeaponDefinition
    {
        public readonly PrototypeWeaponId Id;
        public readonly string Name;
        public readonly string ChineseName;
        public readonly float Damage;
        public readonly float FireInterval;
        public readonly int Magazine;
        public readonly int Reserve;
        public readonly float ReloadSeconds;
        public readonly float EffectiveRange;
        public readonly float MaxRange;
        public readonly float HipSpread;
        public readonly float AdsSpread;
        public readonly bool Automatic;

        public PrototypeWeaponDefinition(PrototypeWeaponId id, string name, string chineseName,
            float damage, float fireInterval, int magazine, int reserve, float reloadSeconds,
            float effectiveRange, float maxRange, float hipSpread, float adsSpread, bool automatic)
        {
            Id = id;
            Name = name;
            ChineseName = chineseName;
            Damage = damage;
            FireInterval = fireInterval;
            Magazine = magazine;
            Reserve = reserve;
            ReloadSeconds = reloadSeconds;
            EffectiveRange = effectiveRange;
            MaxRange = maxRange;
            HipSpread = hipSpread;
            AdsSpread = adsSpread;
            Automatic = automatic;
        }

        public float DamageAtRange(float distance)
        {
            if (distance > MaxRange) return 0f;
            if (distance <= EffectiveRange) return Damage;
            float falloff = 1f - (distance - EffectiveRange) /
                (MaxRange - EffectiveRange + 1f);
            return Damage * Mathf.Clamp(falloff, 0.64f, 1f);
        }
    }

    public static class PrototypeInfantryRoles
    {
        // Numeric values mirror src/combat/Weapons.ts. The Unity prototype
        // currently uses one active primary weapon per soldier.
        private static readonly PrototypeWeaponDefinition[] Weapons =
        {
            new PrototypeWeaponDefinition(PrototypeWeaponId.Carbine, "IF-27 Carbine", "IF-27 卡宾枪",
                31f, 0.105f, 30, 150, 1.9f, 92f, 165f, 0.010f, 0.0022f, true),
            new PrototypeWeaponDefinition(PrototypeWeaponId.Marksman, "M89 Marksman", "M89 精确射手步枪",
                58f, 0.32f, 12, 60, 2.35f, 145f, 220f, 0.016f, 0.0011f, false),
            new PrototypeWeaponDefinition(PrototypeWeaponId.Smg, "VX-9 Wasp", "VX-9 黄蜂冲锋枪",
                23f, 0.066f, 36, 180, 1.65f, 38f, 105f, 0.008f, 0.003f, true),
            new PrototypeWeaponDefinition(PrototypeWeaponId.Lmg, "H60 Sentinel", "H60 哨兵轻机枪",
                34f, 0.12f, 80, 240, 4.2f, 110f, 190f, 0.022f, 0.0035f, true),
            new PrototypeWeaponDefinition(PrototypeWeaponId.BattleRifle, "BR-44 Atlas", "BR-44 阿特拉斯战斗步枪",
                43f, 0.17f, 20, 100, 2.5f, 105f, 185f, 0.014f, 0.002f, true),
            new PrototypeWeaponDefinition(PrototypeWeaponId.Sniper, "S12 Meridian", "S12 子午线狙击步枪",
                92f, 1.1f, 5, 30, 3.1f, 210f, 310f, 0.035f, 0.0006f, false),
            new PrototypeWeaponDefinition(PrototypeWeaponId.Pistol, "P8 Relay", "P8 接力手枪",
                29f, 0.22f, 15, 60, 1.3f, 28f, 75f, 0.009f, 0.004f, false)
        };

        public static PrototypeWeaponDefinition GetWeapon(PrototypeWeaponId id)
        {
            int index = (int)id;
            if (index < 0 || index >= Weapons.Length)
                throw new ArgumentOutOfRangeException(nameof(id));
            return Weapons[index];
        }

        public static PrototypeWeaponId DefaultWeapon(PrototypeInfantryClass role)
        {
            switch (role)
            {
                case PrototypeInfantryClass.Assault: return PrototypeWeaponId.Carbine;
                case PrototypeInfantryClass.Medic: return PrototypeWeaponId.Smg;
                case PrototypeInfantryClass.Recon: return PrototypeWeaponId.Marksman;
                case PrototypeInfantryClass.Engineer: return PrototypeWeaponId.BattleRifle;
                default: throw new ArgumentOutOfRangeException(nameof(role));
            }
        }

        public static string GetClassName(PrototypeInfantryClass role)
        {
            switch (role)
            {
                case PrototypeInfantryClass.Assault: return "突击兵";
                case PrototypeInfantryClass.Medic: return "医疗兵";
                case PrototypeInfantryClass.Recon: return "侦察兵";
                case PrototypeInfantryClass.Engineer: return "工程兵";
                default: throw new ArgumentOutOfRangeException(nameof(role));
            }
        }
    }

    // One instance per soldier. All times are Unity Time.time values supplied by
    // the caller, which keeps these rules usable from both Player and Bot.
    public sealed class PrototypeInfantryKit
    {
        private const float GadgetCooldown = 10f;
        private float nextFireAt;
        private float reloadEndsAt;
        private float nextGadgetAt;

        public PrototypeInfantryClass Role { get; private set; }
        public PrototypeWeaponId WeaponId { get; private set; }
        public PrototypeWeaponDefinition Weapon => PrototypeInfantryRoles.GetWeapon(WeaponId);
        public int Ammo { get; private set; }
        public int Reserve { get; private set; }
        public int GadgetCharges { get; private set; }
        public float Armor { get; private set; }
        public bool Reloading => reloadEndsAt > 0f;
        public float ReloadEndsAt => reloadEndsAt;
        public float GadgetReadyAt => nextGadgetAt;
        // Recon has a wider sighting radius; callers still enforce line of sight.
        public float DetectionRange => Role == PrototypeInfantryClass.Recon ? 110f : 85f;

        public PrototypeInfantryKit(PrototypeInfantryClass role)
            : this(role, PrototypeInfantryRoles.DefaultWeapon(role)) { }

        public PrototypeInfantryKit(PrototypeInfantryClass role, PrototypeWeaponId weaponId)
        {
            PrototypeInfantryRoles.DefaultWeapon(role); // Validate role once.
            PrototypeInfantryRoles.GetWeapon(weaponId);
            Role = role;
            WeaponId = weaponId;
            ResetForSpawn();
        }

        public void EquipWeapon(PrototypeWeaponId weaponId)
        {
            PrototypeInfantryRoles.GetWeapon(weaponId);
            WeaponId = weaponId;
            Ammo = Weapon.Magazine;
            Reserve = Weapon.Reserve;
            nextFireAt = 0f;
            reloadEndsAt = 0f;
        }

        public void ResetForSpawn()
        {
            Ammo = Weapon.Magazine;
            Reserve = Weapon.Reserve;
            GadgetCharges = 2;
            Armor = 0f;
            nextFireAt = 0f;
            reloadEndsAt = 0f;
            nextGadgetAt = 0f;
        }

        public void ApplyNetworkAmmo(int ammo, int reserve)
        {
            if (ammo < 0 || ammo > Weapon.Magazine ||
                reserve < 0 || reserve > Weapon.Reserve) return;
            Ammo = ammo;
            Reserve = reserve;
        }

        public void Tick(float now)
        {
            if (reloadEndsAt <= 0f || now < reloadEndsAt) return;
            int amount = Mathf.Min(Weapon.Magazine - Ammo, Reserve);
            Ammo += amount;
            Reserve -= amount;
            reloadEndsAt = 0f;
        }

        public bool CanFire(float now)
        {
            Tick(now);
            return Ammo > 0 && !Reloading && now >= nextFireAt;
        }

        public bool TryFire(float now)
        {
            if (!CanFire(now))
            {
                if (Ammo == 0) StartReload(now);
                return false;
            }
            Ammo--;
            nextFireAt = now + Weapon.FireInterval;
            if (Ammo == 0) StartReload(now);
            return true;
        }

        public bool StartReload(float now)
        {
            Tick(now);
            if (Reloading || Ammo >= Weapon.Magazine || Reserve <= 0) return false;
            reloadEndsAt = now + Weapon.ReloadSeconds;
            return true;
        }

        // Healing only applies to living, injured soldiers. A future downed
        // state can add revive handling without changing this combat rule.
        public bool TryHeal(float now, float targetHealth, float distance, out float newHealth)
        {
            newHealth = targetHealth;
            if (Role != PrototypeInfantryClass.Medic || !GadgetAvailable(now) ||
                targetHealth <= 0f || targetHealth >= 100f || distance < 0f || distance > 8f)
                return false;
            newHealth = Mathf.Min(100f, targetHealth + 45f);
            SpendGadget(now);
            return true;
        }

        // Call only after finding a visible enemy within DetectionRange. This
        // spends one recon report; the caller forwards the contact to command.
        public bool TrySpot(float now)
        {
            if (Role != PrototypeInfantryClass.Recon || !GadgetAvailable(now)) return false;
            SpendGadget(now);
            return true;
        }

        // The caller checks team membership and visibility before applying
        // support to another soldier. A charge is spent only if ammo was added.
        public bool TryResupply(float now, PrototypeInfantryKit target, float distance)
        {
            if (Role != PrototypeInfantryClass.Assault || !GadgetAvailable(now) ||
                target == null || distance < 0f || distance > 12f ||
                target.Reserve >= target.Weapon.Reserve) return false;
            int amount = Mathf.Min(target.Weapon.Reserve - target.Reserve,
                target.Weapon.Magazine * 2);
            if (amount <= 0) return false;
            target.Reserve += amount;
            SpendGadget(now);
            return true;
        }

        // Engineer armor plates support the current infantry-only prototype.
        // Repairing vehicles can be added when those entities are ported.
        public bool TryApplyArmor(float now, PrototypeInfantryKit target, float distance)
        {
            if (Role != PrototypeInfantryClass.Engineer || !GadgetAvailable(now) ||
                target == null || distance < 0f || distance > 8f || target.Armor >= 35f)
                return false;
            target.Armor = 35f;
            SpendGadget(now);
            return true;
        }

        // TakeDamage should subtract this returned value from Health. Armor is
        // consumed first and never heals an already wounded soldier.
        public float AbsorbDamage(float incomingDamage)
        {
            if (incomingDamage <= 0f) return 0f;
            float blocked = Mathf.Min(Armor, incomingDamage);
            Armor -= blocked;
            return incomingDamage - blocked;
        }

        private bool GadgetAvailable(float now)
        {
            return GadgetCharges > 0 && now >= nextGadgetAt;
        }

        private void SpendGadget(float now)
        {
            GadgetCharges--;
            nextGadgetAt = now + GadgetCooldown;
        }
    }
}
